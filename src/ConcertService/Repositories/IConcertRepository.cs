using ConcertService.Models;

namespace ConcertService.Repositories
{
    public interface IConcertRepository
    {
        Task<List<Concert>> GetAllAsync();
        Task<Concert?> GetByIdAsync(string id);
        Task<Concert> CreateConcertAsync(Concert newConcert);
        Task<Concert?> AddSeatTypeAsync(string concertId, SeatType newSeatType);
        Task<bool> UpdateConcertAsync(string id, Concert updatedConcert);
        Task<bool> IsConcertsCollectionEmptyAsync();
        Task<List<Concert>> GetConcertsToDisableBookingAsync(System.DateTime currentTimeUtc);
    }
}
