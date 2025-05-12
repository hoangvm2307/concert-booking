using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConcertService.Models.DTOs
{
    public class CreateConcertRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Name { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Venue { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public List<CreateSeatTypeForConcertDto> SeatTypes { get; set; } = new List<CreateSeatTypeForConcertDto>();
    }

    public class CreateSeatTypeForConcertDto
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