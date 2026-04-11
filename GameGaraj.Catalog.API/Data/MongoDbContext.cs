using GameGaraj.Catalog.API.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly MongoDbSettings _settings;

        static MongoDbContext()
        {
            // Fluent MongoDB Mapping (Decoupling domain models)
            if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
            {
                BsonClassMap.RegisterClassMap<Product>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(p => p.Id)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));
                    
                    cm.MapMember(p => p.CategoryId)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Category)))
            {
                BsonClassMap.RegisterClassMap<Category>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(c => c.Id)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));

                    cm.MapMember(c => c.ParentId)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(CategoryAttribute)))
            {
                BsonClassMap.RegisterClassMap<CategoryAttribute>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(a => a.Id)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));

                    cm.MapMember(a => a.CategoryId)
                      .SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
            }
        }

        public MongoDbContext(IOptions<MongoDbSettings> settings, IMongoClient mongoClient)
        {
            _settings = settings.Value;
            _database = mongoClient.GetDatabase(_settings.DatabaseName);
        }

        public IMongoCollection<Product> Products => 
            _database.GetCollection<Product>(_settings.ProductsCollection);

        public IMongoCollection<Category> Categories => 
            _database.GetCollection<Category>(_settings.CategoriesCollection);

        public IMongoCollection<CategoryAttribute> CategoryAttributes => 
            _database.GetCollection<CategoryAttribute>(_settings.CategoryAttributesCollection);

        public IMongoDatabase Database => _database;
    }
}
