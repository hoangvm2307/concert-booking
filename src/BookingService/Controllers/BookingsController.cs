using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BookingService.Models.DTOs;
using BookingService.Services;
using BookingService.Models.Results;
using BookingService.Models;

namespace BookingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingManagementService _bookingManagementService;
        private readonly BookingStorageService _bookingStorageService;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            IBookingManagementService bookingManagementService,
            BookingStorageService bookingStorageService,
            ILogger<BookingsController> logger)
        {
            _bookingManagementService = bookingManagementService;
            _bookingStorageService = bookingStorageService;
            _logger = logger;
        }

        private IActionResult HandleBookingOperationResult<T>(BookingOperationResult<T> result, string? createdActionName = null, object? routeValues = null) where T : class
        {
            if (result.IsSuccess)
            {
                if (createdActionName != null && result.Data != null)
                {
                    if (result.Data is Booking bookingData)
                    {
                        var responseDto = MapBookingToDto(bookingData);
                        return CreatedAtAction(createdActionName, routeValues, responseDto);
                    }
                    return CreatedAtAction(createdActionName, routeValues, result.Data);
                }
                if (result.HttpStatusCode == StatusCodes.Status204NoContent)
                {
                    return NoContent();
                }
                return Ok(result.Data);
            }

            // Handle failures
            _logger.LogWarning("Booking operation failed. ErrorType: {ErrorType}, Message: {ErrorMessage}", result.ErrorType, result.ErrorMessage);
            var errorResponse = new { message = result.ErrorMessage, type = result.ErrorType?.ToString() };

            return result.ErrorType switch
            {
                BookingOperationErrorType.ConcertNotFound => NotFound(errorResponse),
                BookingOperationErrorType.SeatTypeNotFound => NotFound(errorResponse),
                BookingOperationErrorType.BookingNotFound => NotFound(errorResponse),
                BookingOperationErrorType.ConcertNotBookable => Conflict(errorResponse),
                BookingOperationErrorType.ConcertAlreadyStarted => Conflict(errorResponse),
                BookingOperationErrorType.AlreadyBookedByUser => Conflict(errorResponse),
                BookingOperationErrorType.TicketsSoldOut => Conflict(errorResponse),
                BookingOperationErrorType.InventoryKeyNotFound => Conflict(errorResponse),
                BookingOperationErrorType.BookingNotCancellable => Conflict(errorResponse),
                BookingOperationErrorType.ForbiddenAccess => Forbid(),
                BookingOperationErrorType.BadRequest => BadRequest(errorResponse),
                _ => StatusCode(result.HttpStatusCode ?? StatusCodes.Status500InternalServerError, errorResponse)
            };
        }

        private BookingResponseDto MapBookingToDto(Booking booking)
        {
            return new BookingResponseDto
            {
                Id = booking.Id,
                UserId = booking.UserId,
                ConcertId = booking.ConcertId,
                ConcertName = booking.ConcertName,
                SeatTypeId = booking.SeatTypeId,
                SeatTypeName = booking.SeatTypeName,
                PricePaid = booking.PricePaid,
                BookingTime = booking.BookingTime,
                Status = booking.Status
            };
        }


        [HttpPost]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto bookingRequest)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("User ID or Email not found in token.");
                return Unauthorized(new { message = "User ID or Email not found in token." });
            }
            if (!ModelState.IsValid)
            {
                return HandleBookingOperationResult(BookingOperationResult<Booking>.Failure("Invalid request data.", BookingOperationErrorType.BadRequest, StatusCodes.Status400BadRequest));
            }

            var result = await _bookingManagementService.CreateBookingAsync(userId, userEmail, bookingRequest);

            return HandleBookingOperationResult(result, nameof(GetBookingById), new { id = result.Data?.Id });
        }

        [HttpDelete("{bookingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelBooking(string bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("CancelBooking: User ID or Email not found in token for booking ID {BookingId}.", bookingId);
                return Unauthorized(new { message = "User ID or Email not found in token." });
            }

            var result = await _bookingManagementService.CancelBookingAsync(userId, userEmail, bookingId);
            return HandleBookingOperationResult(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBookingById(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var booking = await _bookingStorageService.GetBookingByIdAsync(id);
            if (booking == null || booking.UserId != userId)
            {
                return NotFound(new { message = $"Booking with ID '{id}' not found or access denied." });
            }
            return Ok(MapBookingToDto(booking));
        }

        [HttpGet("mybookings")]
        [ProducesResponseType(typeof(IEnumerable<BookingResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetMyBookings: User ID not found in token.");
                return Unauthorized(new { message = "User ID not found in token." });
            }

            _logger.LogInformation("GetMyBookings: Fetching bookings for UserID {UserId}", userId);
            var bookings = await _bookingStorageService.GetBookingsByUserIdAsync(userId);

            if (bookings == null || !bookings.Any())
            {
                return Ok(new List<BookingResponseDto>());
            }

            var responseDtos = bookings.Select(MapBookingToDto).ToList();
            return Ok(responseDtos);
        }
    }
}
