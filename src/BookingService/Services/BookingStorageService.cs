using BookingService.Models;
using BookingService.Models.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BookingService.Services
{
    public class BookingStorageService
    {
        private readonly IMongoCollection<Booking> _bookingsCollection;

        public BookingStorageService(IOptions<MongoDbSettings> mongoDbSettings)
        {
            var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
            _bookingsCollection = mongoDatabase.GetCollection<Booking>(mongoDbSettings.Value.BookingsCollectionName);

            var userConcertIndexKeys = Builders<Booking>.IndexKeys.Ascending(b => b.UserId).Ascending(b => b.ConcertId);
            var indexOptions = new CreateIndexOptions { Unique = false };
            var indexModel = new CreateIndexModel<Booking>(userConcertIndexKeys, indexOptions);
            _bookingsCollection.Indexes.CreateOneAsync(indexModel);
        }

        public async Task<Booking> CreateBookingAsync(Booking newBooking)
        {
            await _bookingsCollection.InsertOneAsync(newBooking);
            return newBooking;
        }

        public async Task<List<Booking>> GetBookingsByUserIdAsync(string userId)
        {
            return await _bookingsCollection.Find(b => b.UserId == userId).ToListAsync();
        }

        public async Task<Booking?> GetBookingByIdAsync(string bookingId)
        {
            return await _bookingsCollection.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
        }

        public async Task<bool> HasUserBookedConcertAsync(string userId, string concertId)
        {
            var existingBooking = await _bookingsCollection.Find(b =>
                b.UserId == userId &&
                b.ConcertId == concertId &&
                b.Status == BookingStatus.Confirmed)
                .FirstOrDefaultAsync();
            return existingBooking != null;
        }

        public async Task<Booking?> UpdateBookingStatusAsync(string bookingId, BookingStatus newStatus)
        {
            var filter = Builders<Booking>.Filter.Eq(b => b.Id, bookingId);
            var update = Builders<Booking>.Update.Set(b => b.Status, newStatus);

            var options = new FindOneAndUpdateOptions<Booking>
            {
                ReturnDocument = ReturnDocument.After
            };

            return await _bookingsCollection.FindOneAndUpdateAsync(filter, update, options);
        }
    }
}