namespace AuthService.Models.Configuration
{
    public class MongoDbSettings
    {
        public required string ConnectionString { get; set; }
        public required string DatabaseName { get; set; }
        public required string UsersCollectionName { get; set; }
    }
}