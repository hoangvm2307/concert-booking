
using ConcertService.Models.DTOs;
using ConcertService.Models.Results;

namespace ConcertService.Services
{
    public interface IConcertLogicService
    {
        Task<ConcertOperationResult<ConcertResponseDto>> GetAllConcertsAsync();
        Task<ConcertOperationResult<ConcertResponseDto>> GetConcertByIdAsync(string id);
        Task<ConcertOperationResult<ConcertResponseDto>> CreateConcertAsync(CreateConcertRequestDto createConcertDto);
        Task<ConcertOperationResult<SeatTypeDto>> AddSeatTypeToConcertAsync(string concertId, AddSeatTypeRequestDto addSeatTypeDto);
    }
}
