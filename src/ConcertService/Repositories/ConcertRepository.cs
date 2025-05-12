using ConcertService.Models;
using ConcertService.Models.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;

namespace ConcertService.Repositories
{
    public class ConcertRepository : IConcertRepository
    {
        private readonly IMongoCollection<Concert> _concertsCollection;
        private readonly ILogger<ConcertRepository> _logger;

        public ConcertRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<ConcertRepository> logger)
        {
            var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
            _concertsCollection = mongoDatabase.GetCollection<Concert>(mongoDbSettings.Value.ConcertsCollectionName);
            _logger = logger;
        }

        public async Task<List<Concert>> GetAllAsync()
        {
            _logger.LogInformation("Fetching all concerts from database.");
            return await _concertsCollection.Find(_ => true).ToListAsync();
        }

        public async Task<Concert?> GetByIdAsync(string id)
        {
            _logger.LogInformation("Fetching concert by ID: {ConcertId} from database.", id);
            return await _concertsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Concert> CreateConcertAsync(Concert newConcert)
        {
            _logger.LogInformation("Creating new concert '{ConcertName}' in database.", newConcert.Name);

            if (string.IsNullOrEmpty(newConcert.Id))
            {
                newConcert.Id = ObjectId.GenerateNewId().ToString();
            }
            foreach (var seatType in newConcert.SeatTypes)
            {
                if (string.IsNullOrEmpty(seatType.Id))
                {
                    seatType.Id = ObjectId.GenerateNewId().ToString();
                }
            }
            await _concertsCollection.InsertOneAsync(newConcert);
            return newConcert;
        }

        public async Task<Concert?> AddSeatTypeAsync(string concertId, SeatType newSeatType)
        {
            _logger.LogInformation("Adding seat type '{SeatTypeName}' to concert ID: {ConcertId} in database.", newSeatType.Name, concertId);
            if (string.IsNullOrEmpty(newSeatType.Id))
            {
                newSeatType.Id = ObjectId.GenerateNewId().ToString();
            }
            var filter = Builders<Concert>.Filter.Eq(c => c.Id, concertId);
            var update = Builders<Concert>.Update.Push(c => c.SeatTypes, newSeatType);
            var result = await _concertsCollection.UpdateOneAsync(filter, update);

            if (result.IsAcknowledged && result.ModifiedCount > 0) return await GetByIdAsync(concertId);

            _logger.LogWarning("Failed to add seat type '{SeatTypeName}' to concert ID: {ConcertId}. Update not acknowledged or no document modified.", newSeatType.Name, concertId);
            return null;
        }

        public async Task<bool> UpdateConcertAsync(string id, Concert updatedConcert)
        {
            _logger.LogInformation("Updating concert ID: {ConcertId} in database.", id);
            var result = await _concertsCollection.ReplaceOneAsync(x => x.Id == id, updatedConcert);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<bool> IsConcertsCollectionEmptyAsync()
        {
            return await _concertsCollection.CountDocumentsAsync(FilterDefinition<Concert>.Empty) == 0;
        }

        public async Task<List<Concert>> GetConcertsToDisableBookingAsync(DateTime currentTimeUtc)
        {
            _logger.LogInformation("Fetching concerts to disable booking, started before or at {CurrentTimeUtc} and still enabled.", currentTimeUtc);
            var filter = Builders<Concert>.Filter.And(
                Builders<Concert>.Filter.Lte(c => c.StartTime, currentTimeUtc),
                Builders<Concert>.Filter.Eq(c => c.IsBookingEnabled, true)
            );
            return await _concertsCollection.Find(filter).ToListAsync();
        }
    }
}