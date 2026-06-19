using Elastic.Clients.Elasticsearch;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class ProductIndexService : IProductIndexService
    {
        private const string IndexName = "products";

        private readonly ElasticsearchClient _elasticClient;
        private readonly CatalogDbContext _context;
        private readonly ILogger<ProductIndexService> _logger;
        private readonly string _elasticUri;

        public ProductIndexService(
            ElasticsearchClient elasticClient,
            CatalogDbContext context,
            ILogger<ProductIndexService> logger,
            IConfiguration configuration)
        {
            _elasticClient = elasticClient;
            _context = context;
            _logger = logger;
            _elasticUri = (configuration["ElasticSearchSettings:Uri"] ?? "http://localhost:9200").TrimEnd('/');
        }

        public async Task EnsureIndexAsync(bool recreate = false)
        {
            using var httpClient = new HttpClient();
            var indexUrl = $"{_elasticUri}/{IndexName}";

            if (recreate)
            {
                var deleteResponse = await httpClient.DeleteAsync(indexUrl);
                if (!deleteResponse.IsSuccessStatusCode && deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Elasticsearch index delete failed: {Error}", error);
                }
            }
            else
            {
                var existsResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, indexUrl));
                if (existsResponse.IsSuccessStatusCode)
                    return;
            }

            var createResponse = await httpClient.PutAsync(
                indexUrl,
                new StringContent(GetIndexDefinitionJson(), Encoding.UTF8, "application/json"));

            if (!createResponse.IsSuccessStatusCode)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                if (!error.Contains("resource_already_exists_exception", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Elasticsearch index create failed: {Error}", error);
                }
            }
        }

        public async Task IndexAsync(Product product)
        {
            await EnsureIndexAsync();

            var document = await CreateDocumentAsync(product);
            var request = new IndexRequest<ProductSearchDocument>(document, IndexName, product.Id);
            var response = await _elasticClient.IndexAsync(request);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch index failed for product {ProductId}: {Error}", product.Id, response.DebugInformation);
                throw new InvalidOperationException($"Elasticsearch index failed for product {product.Id}");
            }
            else
            {
                await RefreshIndexAsync();
            }
        }

        public async Task DeleteAsync(string productId)
        {
            var response = await _elasticClient.DeleteAsync(IndexName, productId);

            if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode != 404)
            {
                _logger.LogWarning("Elasticsearch delete failed for product {ProductId}: {Error}", productId, response.DebugInformation);
                throw new InvalidOperationException($"Elasticsearch delete failed for product {productId}");
            }
            else
            {
                await RefreshIndexAsync();
            }
        }

        public async Task<ReindexResultDto> ReindexAllAsync()
        {
            await EnsureIndexAsync(recreate: true);

            var products = await _context.Products.ToListAsync();
            var result = new ReindexResultDto { Total = products.Count };

            foreach (var product in products)
            {
                try
                {
                    var document = await CreateDocumentAsync(product);
                    var request = new IndexRequest<ProductSearchDocument>(document, IndexName, product.Id);
                    var response = await _elasticClient.IndexAsync(request);

                    if (response.IsValidResponse)
                    {
                        result.Succeeded++;
                    }
                    else
                    {
                        result.Failed++;
                        result.Errors.Add($"{product.Id}: {response.DebugInformation}");
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"{product.Id}: {ex.Message}");
                    _logger.LogError(ex, "Elasticsearch reindex failed for product {ProductId}", product.Id);
                }
            }

            result.Errors = result.Errors.Take(20).ToList();
            await RefreshIndexAsync();
            await WaitForIndexedDocumentCountAsync(result.Succeeded);
            return result;
        }

        public async Task<SearchIndexStatusDto> GetStatusAsync()
        {
            var status = new SearchIndexStatusDto
            {
                CheckedAt = DateTime.UtcNow,
                PendingIndexQueueCount = await _context.IndexingJobs.CountAsync(job =>
                    job.Status == IndexingJobStatus.Pending || job.Status == IndexingJobStatus.Processing),
                FailedIndexingCount = await _context.IndexingJobs.CountAsync(job => job.Status == IndexingJobStatus.Failed),
                LastFailedIndexingAt = await _context.IndexingJobs
                    .Where(job => job.Status == IndexingJobStatus.Failed)
                    .MaxAsync(job => (DateTime?)job.LastAttemptAt)
            };

            try
            {
                await EnsureIndexAsync();

                var searchResponse = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                    .Index(IndexName)
                    .Size(1000));

                if (!searchResponse.IsValidResponse)
                {
                    status.Status = "Offline";
                    status.ErrorMessage = searchResponse.DebugInformation;
                    return status;
                }

                status.IsConnected = true;
                status.Status = "Online";
                status.IndexedProductCount = await GetIndexedProductCountAsync(searchResponse.Documents.Count);
                status.LastIndexedAt = searchResponse.Documents.Any()
                    ? searchResponse.Documents.Select(product => product.IndexedAt ?? product.UpdatedAt ?? product.CreatedAt).Max()
                    : null;

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Elasticsearch status check failed");
                status.Status = "Offline";
                status.ErrorMessage = ex.Message;
                return status;
            }
        }

        public async Task<SearchIndexDocumentPageDto> GetDocumentPreviewsAsync(int page = 1, int pageSize = 100)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 10, 100);
            var from = (page - 1) * pageSize;

            var searchResponse = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index(IndexName)
                .From(from)
                .Size(pageSize));

            if (!searchResponse.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch document preview failed: {Error}", searchResponse.DebugInformation);
                return new SearchIndexDocumentPageDto
                {
                    Page = page,
                    PageSize = pageSize
                };
            }

            var totalCount = await GetIndexedProductCountAsync(searchResponse.Documents.Count);
            var items = searchResponse.Documents
                .Select(product => new SearchIndexDocumentPreviewDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Brand = product.Brand,
                    Slug = product.Slug,
                    CategoryId = product.CategoryId,
                    CategoryName = product.CategoryName,
                    CategorySlug = product.CategorySlug,
                    Price = product.Price,
                    Stock = product.Stock,
                    ReservedStock = product.ReservedStock,
                    AvailableStock = product.AvailableStock,
                    IsActive = product.IsActive,
                    IsFeatured = product.IsFeatured,
                    InStock = product.InStock,
                    ImageUrls = product.ImageUrls,
                    Specs = product.Specs,
                    SpecValues = product.SpecValues,
                    SearchText = product.SearchText,
                    CreatedAt = product.CreatedAt,
                    LastIndexedAt = product.IndexedAt ?? product.UpdatedAt ?? product.CreatedAt
                })
                .ToList();

            return new SearchIndexDocumentPageDto
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private async Task<long> GetIndexedProductCountAsync(int fallbackCount)
        {
            try
            {
                var countResponse = await _elasticClient.CountAsync(new CountRequest(IndexName));
                return countResponse.IsValidResponse ? countResponse.Count : fallbackCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Elasticsearch count check failed");
                return fallbackCount;
            }
        }

        private async Task WaitForIndexedDocumentCountAsync(int expectedCount)
        {
            if (expectedCount <= 0) return;

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var currentCount = await GetIndexedProductCountAsync(0);
                if (currentCount >= expectedCount) return;

                await Task.Delay(200);
                await RefreshIndexAsync();
            }
        }

        private async Task RefreshIndexAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.PostAsync($"{_elasticUri}/{IndexName}/_refresh", null);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Elasticsearch index refresh failed: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Elasticsearch index refresh failed");
            }
        }

        private static string GetIndexDefinitionJson()
        {
            var definition = new
            {
                settings = new
                {
                    analysis = new
                    {
                        normalizer = new
                        {
                            lowercase_normalizer = new
                            {
                                type = "custom",
                                filter = new[] { "lowercase", "asciifolding" }
                            }
                        },
                        tokenizer = new
                        {
                            autocomplete_tokenizer = new
                            {
                                type = "edge_ngram",
                                min_gram = 2,
                                max_gram = 20,
                                token_chars = new[] { "letter", "digit" }
                            }
                        },
                        analyzer = new
                        {
                            autocomplete_analyzer = new
                            {
                                type = "custom",
                                tokenizer = "autocomplete_tokenizer",
                                filter = new[] { "lowercase", "asciifolding" }
                            },
                            default_search = new
                            {
                                type = "custom",
                                tokenizer = "standard",
                                filter = new[] { "lowercase", "asciifolding" }
                            }
                        }
                    }
                },
                mappings = new
                {
                    dynamic = true,
                    properties = new Dictionary<string, object>
                    {
                        ["id"] = new { type = "keyword" },
                        ["name"] = new
                        {
                            type = "text",
                            analyzer = "autocomplete_analyzer",
                            search_analyzer = "default_search",
                            fields = new { keyword = new { type = "keyword", normalizer = "lowercase_normalizer" } }
                        },
                        ["brand"] = new
                        {
                            type = "text",
                            analyzer = "autocomplete_analyzer",
                            search_analyzer = "default_search",
                            fields = new { keyword = new { type = "keyword", normalizer = "lowercase_normalizer" } }
                        },
                        ["slug"] = new { type = "keyword" },
                        ["description"] = new { type = "text", analyzer = "default_search" },
                        ["price"] = new { type = "double" },
                        ["stock"] = new { type = "integer" },
                        ["reservedStock"] = new { type = "integer" },
                        ["availableStock"] = new { type = "integer" },
                        ["isActive"] = new { type = "boolean" },
                        ["isFeatured"] = new { type = "boolean" },
                        ["inStock"] = new { type = "boolean" },
                        ["imageUrls"] = new { type = "keyword", index = false },
                        ["createdAt"] = new { type = "date" },
                        ["updatedAt"] = new { type = "date" },
                        ["indexedAt"] = new { type = "date" },
                        ["categoryId"] = new { type = "keyword" },
                        ["categoryName"] = new
                        {
                            type = "text",
                            analyzer = "autocomplete_analyzer",
                            search_analyzer = "default_search",
                            fields = new { keyword = new { type = "keyword", normalizer = "lowercase_normalizer" } }
                        },
                        ["categorySlug"] = new { type = "keyword" },
                        ["specs"] = new { type = "flattened" },
                        ["specValues"] = new
                        {
                            type = "text",
                            analyzer = "autocomplete_analyzer",
                            search_analyzer = "default_search",
                            fields = new { keyword = new { type = "keyword", normalizer = "lowercase_normalizer" } }
                        },
                        ["searchText"] = new { type = "text", analyzer = "autocomplete_analyzer", search_analyzer = "default_search" }
                    }
                }
            };

            return JsonSerializer.Serialize(definition);
        }

        private async Task<ProductSearchDocument> CreateDocumentAsync(Product product)
        {
            var category = !string.IsNullOrWhiteSpace(product.CategoryId)
                ? await _context.Categories.FindAsync(product.CategoryId)
                : null;

            var specValues = product.Specs
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => x.Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categoryName = category?.Name ?? string.Empty;
            var searchParts = new[]
                {
                    product.Name,
                    product.Brand,
                    categoryName,
                    product.Description,
                    string.Join(' ', product.Specs.Keys),
                    string.Join(' ', specValues)
                }
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return new ProductSearchDocument
            {
                Id = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                ReservedStock = product.ReservedStock,
                AvailableStock = product.AvailableStock,
                IsActive = product.IsActive,
                IsFeatured = product.IsFeatured,
                InStock = product.AvailableStock > 0,
                ImageUrls = product.ImageUrls,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                IndexedAt = DateTime.UtcNow,
                CategoryId = product.CategoryId,
                CategoryName = categoryName,
                CategorySlug = category?.Slug ?? string.Empty,
                Specs = product.Specs,
                SpecValues = specValues,
                SearchText = string.Join(' ', searchParts)
            };
        }
    }
}
