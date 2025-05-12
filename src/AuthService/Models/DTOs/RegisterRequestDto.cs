using System.ComponentModel.DataAnnotations;

namespace AuthService.Models.DTOs
{
    public class RegisterRequestDto
    {
        [Required]
        [MinLength(3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)] // Enforce a minimum password length
        public required string Password { get; set; }
    }
}