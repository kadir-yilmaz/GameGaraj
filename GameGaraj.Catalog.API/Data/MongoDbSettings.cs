namespace GameGaraj.Catalog.API.Data
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        
        // Collection names
        public string ProductsCollection { get; set; } = "products";
        public string CategoriesCollection { get; set; } = "categories";
        public string CategoryAttributesCollection { get; set; } = "categoryAttributes";
    }
}
