using ConcertService.Models;
using ConcertService.Models.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.Json;
using System.Text;

namespace ConcertService.Services
{
    public class ConcertManagementService
    {
        private readonly IMongoCollection<Concert> _concertsCollection;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServiceUrls _serviceUrls;
        private readonly ILogger<ConcertManagementService> _logger;
        public ConcertManagementService(
            IOptions<MongoDbSettings> mongoDbSettings,
            IHttpClientFactory httpClientFactory,
            IOptions<ServiceUrls> serviceUrlsOptions,
            ILogger<ConcertManagementService> logger)
        {
            var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
            _concertsCollection = mongoDatabase.GetCollection<Concert>(mongoDbSettings.Value.ConcertsCollectionName);
            _httpClientFactory = httpClientFactory;
            _serviceUrls = serviceUrlsOptions.Value;
            _logger = logger;
        }
        private class InitializeInventoryRequestInternalDto
        {
            public string? ConcertId { get; set; }
            public List<SeatTypeInventoryInfoInternalDto> SeatTypes { get; set; } = new();
        }
        private class SeatTypeInventoryInfoInternalDto
        {
            public string? SeatTypeId { get; set; }
            public int Count { get; set; }
        }
        public async Task<List<Concert>> GetAllAsync() =>
            await _concertsCollection.Find(_ => true).ToListAsync();

        public async Task<Concert?> GetByIdAsync(string id) =>
            await _concertsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateConcertAsync(Concert newConcert)
        {
            foreach (var seatType in newConcert.SeatTypes)
            {
                if (string.IsNullOrEmpty(seatType.Id))
                {
                    seatType.Id = ObjectId.GenerateNewId().ToString();
                }
                seatType.RemainingSeats = seatType.TotalSeats; // Still useful for ConcertService's own data
            }
            await _concertsCollection.InsertOneAsync(newConcert);

            // After successfully creating the concert, notify BookingService
            if (!string.IsNullOrEmpty(newConcert.Id))
            {
                await NotifyBookingServiceOfInventoryAsync(newConcert);
            }
        }

        public async Task AddSeatTypeAsync(string concertId, SeatType newSeatType)
        {
            if (string.IsNullOrEmpty(newSeatType.Id))
            {
                newSeatType.Id = ObjectId.GenerateNewId().ToString();
            }
            newSeatType.RemainingSeats = newSeatType.TotalSeats;

            var filter = Builders<Concert>.Filter.Eq(c => c.Id, concertId);
            var update = Builders<Concert>.Update.Push(c => c.SeatTypes, newSeatType);
            var result = await _concertsCollection.UpdateOneAsync(filter, update);

            if (result.IsAcknowledged && result.ModifiedCount > 0)
            {
                var concert = await GetByIdAsync(concertId);
                if (concert != null) await NotifyBookingServiceOfInventoryAsync(concert); // Or just the new seat type
            }
        }
        private async Task NotifyBookingServiceOfInventoryAsync(Concert concert)
        {
            if (string.IsNullOrEmpty(_serviceUrls.BookingService) || concert.Id == null) return;

            var inventoryData = new InitializeInventoryRequestInternalDto
            {
                ConcertId = concert.Id,
                SeatTypes = concert.SeatTypes.Select(st => new SeatTypeInventoryInfoInternalDto
                {
                    SeatTypeId = st.Id,
                    Count = st.TotalSeats
                }).ToList()
            };

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var jsonPayload = JsonSerializer.Serialize(inventoryData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                string bookingServiceInventoryUrl = $"{_serviceUrls.BookingService}/api/internal/inventory/initialize";
                _logger.LogInformation("Notifying BookingService to initialize inventory for ConcertID {ConcertId} at {Url}", concert.Id, bookingServiceInventoryUrl);

                var response = await httpClient.PostAsync(bookingServiceInventoryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully notified BookingService for ConcertID: {ConcertId}", concert.Id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to notify BookingService for ConcertID: {ConcertId}. Status: {StatusCode}. Response: {Response}",
                        concert.Id, response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while notifying BookingService for ConcertID: {ConcertId}", concert.Id);
            }
        }
        public async Task<bool> UpdateConcertAsync(string id, Concert updatedConcert)
        {
            var result = await _concertsCollection.ReplaceOneAsync(x => x.Id == id, updatedConcert);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }
        public async Task<bool> IsConcertsCollectionEmptyAsync()
        {
            return await _concertsCollection.CountDocumentsAsync(FilterDefinition<Concert>.Empty) == 0;
        }
        public async Task<List<Concert>> GetConcertsToDisableBookingAsync(DateTime currentTimeUtc)
        {
            var filter = Builders<Concert>.Filter.And(
                Builders<Concert>.Filter.Lte(c => c.StartTime, currentTimeUtc),
                Builders<Concert>.Filter.Eq(c => c.IsBookingEnabled, true)
            );
            return await _concertsCollection.Find(filter).ToListAsync();
        }
    }
}