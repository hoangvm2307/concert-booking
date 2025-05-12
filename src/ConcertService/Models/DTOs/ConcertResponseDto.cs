namespace ConcertService.Models.DTOs
{
    public class ConcertResponseDto
    {
        public string? Id { get; set; }
        public required string Name { get; set; }
        public required string Venue { get; set; }
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public string? Description { get; set; }
        public bool IsBookingEnabled { get; set; }
        public List<SeatTypeDto> SeatTypes { get; set; } = new List<SeatTypeDto>();
        public DateTime CreatedAt { get; set; }
    }
}