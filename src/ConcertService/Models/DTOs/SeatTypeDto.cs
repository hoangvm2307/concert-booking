using System.ComponentModel.DataAnnotations;

namespace ConcertService.Models.DTOs
{
    public class SeatTypeDto
    {
        public string? Id { get; set; }

        [Required]
        public required string Name { get; set; }

        [Required]
        [Range(0.01, (double)decimal.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int TotalSeats { get; set; }

        public int RemainingSeats { get; set; }
    }
}