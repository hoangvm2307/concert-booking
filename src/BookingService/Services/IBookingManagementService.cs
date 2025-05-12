using BookingService.Models;
using BookingService.Models.DTOs;
using BookingService.Models.Results;

namespace BookingService.Services
{
    public interface IBookingManagementService
    {
        Task<BookingOperationResult<Booking>> CreateBookingAsync(string userId, string userEmail, CreateBookingRequestDto bookingRequest);
        Task<BookingOperationResult<Booking>> CancelBookingAsync(string userId, string userEmail, string bookingId);
    }
}