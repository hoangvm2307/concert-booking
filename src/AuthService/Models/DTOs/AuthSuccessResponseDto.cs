namespace AuthService.Models.DTOs
{
    public class AuthSuccessResponseDto
    {
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Token { get; set; }
        public DateTime Expiration { get; set; }
    }
}