using System.ComponentModel.DataAnnotations;

namespace ConcertService.Models.DTOs
{
    public class AddSeatTypeRequestDto
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        [Range(0.01, (double)decimal.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int TotalSeats { get; set; }
    }
}