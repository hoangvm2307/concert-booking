using System.ComponentModel.DataAnnotations;

namespace BookingService.Models.DTOs
{
    public class CreateBookingRequestDto
    {
        [Required]
        public required string ConcertId { get; set; }

        [Required]
        public required string SeatTypeId { get; set; }
    }
}