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

            // Create necessary indexes if they don't exist
            // Index for checking if a user has already booked a specific concert
            var userConcertIndexKeys = Builders<Booking>.IndexKeys.Ascending(b => b.UserId).Ascending(b => b.ConcertId);
            var indexOptions = new CreateIndexOptions { Unique = false }; // Not unique because a user might cancel and rebook (though our rule is 1 booking per concert)
                                                                       // For "No duplicate bookings per user per concert", this check will be in application logic before insert.
                                                                       // If it were truly unique in DB, cancellation would be more complex.
            var indexModel = new CreateIndexModel<Booking>(userConcertIndexKeys, indexOptions);
            _bookingsCollection.Indexes.CreateOneAsync(indexModel);
        }

        public async Task<Booking> CreateBookingAsync(Booking newBooking)
        {
            await _bookingsCollection.InsertOneAsync(newBooking);
            return newBooking; // newBooking.Id will be populated by the driver
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
            // Consider only 'Confirmed' bookings if you have other statuses like 'Pending' or 'Cancelled'
            var existingBooking = await _bookingsCollection.Find(b =>
                b.UserId == userId &&
                b.ConcertId == concertId &&
                b.Status == BookingStatus.Confirmed) // Important: only count confirmed bookings as "already booked"
                .FirstOrDefaultAsync();
            return existingBooking != null;
        }

        public async Task<Booking?> UpdateBookingStatusAsync(string bookingId, BookingStatus newStatus)
        {
            var filter = Builders<Booking>.Filter.Eq(b => b.Id, bookingId);
            var update = Builders<Booking>.Update.Set(b => b.Status, newStatus);

            var options = new FindOneAndUpdateOptions<Booking>
            {
                ReturnDocument = ReturnDocument.After // Returns the document after the update
            };

            return await _bookingsCollection.FindOneAndUpdateAsync(filter, update, options);
        }
    }
}