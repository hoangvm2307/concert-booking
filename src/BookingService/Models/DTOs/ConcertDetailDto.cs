
namespace BookingService.Models.DTOs.External
{

    public class ConcertDetailDto
    {
        public string? Id { get; set; }
        public required string Name { get; set; }
        // public string Venue { get; set; } // Not strictly needed for booking logic
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        // public string? Description { get; set; } // Not strictly needed
        public bool IsBookingEnabled { get; set; } = true;
        public List<SeatTypeDetailDto> SeatTypes { get; set; } = new List<SeatTypeDetailDto>();
    }

    public class SeatTypeDetailDto
    {
        public string? Id { get; set; }
        public required string Name { get; set; }
        public decimal Price { get; set; }
        public int TotalSeats { get; set; } // We might not need TotalSeats here for booking logic,
                                            // but price and name are essential.
                                            // public int RemainingSeats { get; set; } // This would come from Redis, ConcertService GET might provide it
    }
}