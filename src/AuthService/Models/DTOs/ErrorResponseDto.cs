namespace AuthService.Models.DTOs
{
    public class ErrorResponseDto
    {
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }

        public ErrorResponseDto(string message, List<string>? errors = null)
        {
            Message = message;
            Errors = errors;
        }
    }
}