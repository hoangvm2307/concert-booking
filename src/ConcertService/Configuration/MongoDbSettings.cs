namespace ConcertService.Models.Configuration
{
    public class MongoDbSettings
    {
        public required string ConnectionString { get; set; }
        public required string DatabaseName { get; set; }
        public required string ConcertsCollectionName { get; set; }
    }

    public class RedisSettings
    {
        public required string ConnectionString { get; set; }
    }
}