namespace BookingService.Models.DTOs
{
    public class BookingResponseDto
    {
        public string? Id { get; set; }
        public required string UserId { get; set; }
        public required string ConcertId { get; set; }
        public string? ConcertName { get; set; }
        public required string SeatTypeId { get; set; }
        public string? SeatTypeName { get; set; }
        public decimal PricePaid { get; set; }
        public DateTime BookingTime { get; set; }
        public BookingStatus Status { get; set; }
    }
}