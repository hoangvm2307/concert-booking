using BookingService.Models;
using BookingService.Models.Configuration;
using BookingService.Models.DTOs;
using BookingService.Models.DTOs.External;
using BookingService.Models.Results;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BookingService.Services
{
    public class BookingManagementService : IBookingManagementService
    {
        private readonly BookingStorageService _bookingStorageService;
        private readonly TicketInventoryService _ticketInventoryService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServiceUrls _serviceUrls;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingManagementService> _logger;

        public BookingManagementService(
            BookingStorageService bookingStorageService,
            TicketInventoryService ticketInventoryService,
            IHttpClientFactory httpClientFactory,
            IOptions<ServiceUrls> serviceUrlsOptions,
            IEmailService emailService,
            ILogger<BookingManagementService> logger)
        {
            _bookingStorageService = bookingStorageService;
            _ticketInventoryService = ticketInventoryService;
            _httpClientFactory = httpClientFactory;
            _serviceUrls = serviceUrlsOptions.Value;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<BookingOperationResult<Booking>> CreateBookingAsync(string userId, string userEmail, CreateBookingRequestDto bookingRequest)
        {
            _logger.LogInformation("CreateBookingAsync started for UserID: {UserId}, ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}",
                userId, bookingRequest.ConcertId, bookingRequest.SeatTypeId);

            // 1. Fetch Concert and SeatType details from ConcertService
            ConcertDetailDto? concertDetail;
            SeatTypeDetailDto? selectedSeatType;

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var concertServiceUrl = _serviceUrls.ConcertService ?? throw new InvalidOperationException("ConcertService URL not configured.");

                _logger.LogInformation("Fetching concert details from: {Url}", $"{concertServiceUrl}/api/concerts/{bookingRequest.ConcertId}");
                var response = await httpClient.GetAsync($"{concertServiceUrl}/api/concerts/{bookingRequest.ConcertId}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return BookingOperationResult<Booking>.Failure($"Concert with ID '{bookingRequest.ConcertId}' not found.", BookingOperationErrorType.ConcertNotFound, StatusCodes.Status404NotFound);
                    }
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch concert details. Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                    return BookingOperationResult<Booking>.Failure("Error fetching concert details from ConcertService.", BookingOperationErrorType.ConcertServiceCommunicationError, (int)response.StatusCode);
                }

                var concertJson = await response.Content.ReadAsStringAsync();
                concertDetail = JsonSerializer.Deserialize<ConcertDetailDto>(concertJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (concertDetail == null)
                {
                    return BookingOperationResult<Booking>.Failure($"Could not parse concert details for ID '{bookingRequest.ConcertId}'.", BookingOperationErrorType.ConcertServiceCommunicationError, StatusCodes.Status404NotFound);
                }

                selectedSeatType = concertDetail.SeatTypes?.FirstOrDefault(st => st.Id == bookingRequest.SeatTypeId);
                if (selectedSeatType == null)
                {
                    return BookingOperationResult<Booking>.Failure($"Seat type with ID '{bookingRequest.SeatTypeId}' not found for concert '{bookingRequest.ConcertId}'.", BookingOperationErrorType.SeatTypeNotFound, StatusCodes.Status404NotFound);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching concert details for ConcertID: {ConcertId}", bookingRequest.ConcertId);
                return BookingOperationResult<Booking>.Failure("An error occurred while fetching concert information.", BookingOperationErrorType.ConcertServiceCommunicationError, StatusCodes.Status500InternalServerError);
            }

            // 2. Check if concert is bookable
            if (!concertDetail.IsBookingEnabled)
            {
                return BookingOperationResult<Booking>.Failure("Bookings for this concert are currently disabled.", BookingOperationErrorType.ConcertNotBookable, StatusCodes.Status409Conflict);
            }
            if (concertDetail.StartTime <= DateTime.UtcNow)
            {
                return BookingOperationResult<Booking>.Failure("This concert has already started or is in the past. Booking is not allowed.", BookingOperationErrorType.ConcertAlreadyStarted, StatusCodes.Status409Conflict);
            }

            // 3. Check if user has already booked this concert
            if (await _bookingStorageService.HasUserBookedConcertAsync(userId, bookingRequest.ConcertId))
            {
                return BookingOperationResult<Booking>.Failure("You have already booked a ticket for this concert.", BookingOperationErrorType.AlreadyBookedByUser, StatusCodes.Status409Conflict);
            }

            // 4. Attempt to decrement ticket count in Redis
            var decrementResult = await _ticketInventoryService.TryDecrementTicketCountAsync(bookingRequest.ConcertId, bookingRequest.SeatTypeId);
            switch (decrementResult)
            {
                case DecrementResult.SoldOut:
                    return BookingOperationResult<Booking>.Failure("Tickets for this seat type are sold out.", BookingOperationErrorType.TicketsSoldOut, StatusCodes.Status409Conflict);
                case DecrementResult.KeyNotFound:
                    _logger.LogWarning("Ticket inventory key not found for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}. Assuming sold out or not initialized.", bookingRequest.ConcertId, bookingRequest.SeatTypeId);
                    return BookingOperationResult<Booking>.Failure("This seat type is currently unavailable or not initialized.", BookingOperationErrorType.InventoryKeyNotFound, StatusCodes.Status409Conflict);
                case DecrementResult.Error:
                    return BookingOperationResult<Booking>.Failure("An error occurred while updating ticket inventory.", BookingOperationErrorType.InventoryUpdateFailed, StatusCodes.Status500InternalServerError);
                case DecrementResult.Success:
                    _logger.LogInformation("Ticket successfully reserved in Redis for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId} by UserID: {UserId}", bookingRequest.ConcertId, bookingRequest.SeatTypeId, userId);
                    break;
                default:
                    return BookingOperationResult<Booking>.Failure("Unknown error with ticket inventory.", BookingOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            // 5. Create booking record in MongoDB
            var newBooking = new Booking
            {
                UserId = userId,
                ConcertId = bookingRequest.ConcertId,
                ConcertName = concertDetail.Name,
                SeatTypeId = bookingRequest.SeatTypeId,
                SeatTypeName = selectedSeatType.Name,
                PricePaid = selectedSeatType.Price,
                BookingTime = DateTime.UtcNow,
                Status = BookingStatus.Confirmed
            };

            Booking createdBooking;
            try
            {
                createdBooking = await _bookingStorageService.CreateBookingAsync(newBooking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking in database after reserving ticket. Attempting to roll back inventory for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}", bookingRequest.ConcertId, bookingRequest.SeatTypeId);
                var incrementResult = await _ticketInventoryService.TryIncrementTicketCountAsync(bookingRequest.ConcertId, bookingRequest.SeatTypeId);
                if (incrementResult != IncrementResult.Success)
                {
                    _logger.LogError("Failed to roll back ticket inventory for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}. Manual correction needed.", bookingRequest.ConcertId, bookingRequest.SeatTypeId);
                }
                return BookingOperationResult<Booking>.Failure("An error occurred while saving your booking. Ticket reservation was rolled back if possible.", BookingOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            // 6. Send Confirmation Email
            try
            {
                var emailSubject = $"Booking Confirmation - {createdBooking.ConcertName}";
                var emailHtmlBody = $@"<h1>Booking Confirmed!</h1><p>Dear Customer,</p><p>Your booking for the concert '<strong>{createdBooking.ConcertName}</strong>' ({createdBooking.SeatTypeName}) is confirmed!</p><ul><li><strong>Booking ID:</strong> {createdBooking.Id}</li><li><strong>Concert:</strong> {createdBooking.ConcertName}</li><li><strong>Seat Type:</strong> {createdBooking.SeatTypeName}</li><li><strong>Date:</strong> {concertDetail.StartTime.ToLocalTime():yyyy-MM-dd}</li><li><strong>Time:</strong> {concertDetail.StartTime.ToLocalTime():HH:mm}</li><li><strong>Price Paid:</strong> {createdBooking.PricePaid:C}</li></ul><p>Thank you for booking with us!</p>";
                var emailTextBody = $"Your booking for {createdBooking.ConcertName} ({createdBooking.SeatTypeName}) is confirmed! Booking ID: {createdBooking.Id}. Date: {concertDetail.StartTime.ToLocalTime():yyyy-MM-dd HH:mm}. Price: {createdBooking.PricePaid:C}.";

                await _emailService.SendEmailAsync(userEmail, emailSubject, emailHtmlBody, emailTextBody);
                _logger.LogInformation("Booking confirmation email process initiated for BookingID {BookingId} to {UserEmail}", createdBooking.Id, userEmail);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send booking confirmation email for BookingID {BookingId} to {UserEmail}, but booking was successful.", createdBooking.Id, userEmail);
            }

            return BookingOperationResult<Booking>.Success(createdBooking);
        }

        public async Task<BookingOperationResult<Booking>> CancelBookingAsync(string userId, string userEmail, string bookingId)
        {
            _logger.LogInformation("CancelBookingAsync started for UserID {UserId}, BookingID {BookingId}", userId, bookingId);

            var booking = await _bookingStorageService.GetBookingByIdAsync(bookingId);
            if (booking == null)
            {
                return BookingOperationResult<Booking>.Failure($"Booking with ID '{bookingId}' not found.", BookingOperationErrorType.BookingNotFound, StatusCodes.Status404NotFound);
            }

            if (booking.UserId != userId)
            {
                return BookingOperationResult<Booking>.Failure("User is not authorized to cancel this booking.", BookingOperationErrorType.ForbiddenAccess, StatusCodes.Status403Forbidden);
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                return BookingOperationResult<Booking>.Failure($"Booking cannot be cancelled. Current status: {booking.Status}.", BookingOperationErrorType.BookingNotCancellable, StatusCodes.Status409Conflict);
            }
 
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var concertServiceUrl = _serviceUrls.ConcertService ?? throw new InvalidOperationException("ConcertService URL not configured.");
                var response = await httpClient.GetAsync($"{concertServiceUrl}/api/concerts/{booking.ConcertId}");
                if (response.IsSuccessStatusCode)
                {
                    var concertJson = await response.Content.ReadAsStringAsync();
                    var concertDetail = JsonSerializer.Deserialize<ConcertDetailDto>(concertJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (concertDetail != null && concertDetail.StartTime <= DateTime.UtcNow)
                    {
                        return BookingOperationResult<Booking>.Failure("Cannot cancel booking: the concert has already started.", BookingOperationErrorType.ConcertAlreadyStarted, StatusCodes.Status409Conflict);
                    }
                }
                else
                {
                    _logger.LogWarning("CancelBooking: Failed to fetch concert details for ConcertID {ConcertId} (booking {BookingId}) to check start time. Status: {StatusCode}. Proceeding with cancellation with caution.", booking.ConcertId, bookingId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelBooking: Exception while fetching concert details for ConcertID {ConcertId} (booking {BookingId}). Proceeding with cancellation with caution.", booking.ConcertId, bookingId);
            }

            var incrementResult = await _ticketInventoryService.TryIncrementTicketCountAsync(booking.ConcertId, booking.SeatTypeId);
            if (incrementResult == IncrementResult.Error || incrementResult == IncrementResult.KeyNotFound)
            {
                _logger.LogError("CancelBooking: Failed to increment ticket inventory for ConcertID {ConcertId}, SeatTypeID {SeatTypeId} for booking {BookingId}. Result: {IncrementResult}. Manual intervention might be needed.", booking.ConcertId, booking.SeatTypeId, bookingId, incrementResult);
                if (incrementResult == IncrementResult.Error)
                {
                    return BookingOperationResult<Booking>.Failure("Failed to update ticket inventory. Booking not cancelled.", BookingOperationErrorType.InventoryUpdateFailed, StatusCodes.Status500InternalServerError);
                }
                _logger.LogWarning("CancelBooking: Ticket inventory key was not found for ConcertID {ConcertId}, SeatTypeID {SeatTypeId} during cancellation of booking {BookingId}. This is unexpected.", booking.ConcertId, booking.SeatTypeId, bookingId);
                // Decide if this is a hard stop or if we proceed to cancel DB record
            }
            else
            {
                _logger.LogInformation("CancelBooking: Ticket inventory successfully incremented for ConcertID {ConcertId}, SeatTypeID {SeatTypeId}", booking.ConcertId, booking.SeatTypeId);
            }


            var updatedBooking = await _bookingStorageService.UpdateBookingStatusAsync(bookingId, BookingStatus.Cancelled);
            if (updatedBooking == null || updatedBooking.Status != BookingStatus.Cancelled)
            {
                _logger.LogError("CancelBooking: Failed to update booking status to Cancelled in DB for BookingID {BookingId} after inventory increment.", bookingId);
                // Attempt to roll back Redis increment if DB update failed
                // This is complex and needs careful consideration for atomicity
                return BookingOperationResult<Booking>.Failure("Failed to finalize booking cancellation after inventory update. Please contact support.", BookingOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            try
            {
                var emailSubject = $"Booking Cancellation Confirmation - {updatedBooking.ConcertName}";
                var emailHtmlBody = $@"<h1>Booking Cancelled</h1><p>Dear Customer,</p><p>Your booking (ID: <strong>{updatedBooking.Id}</strong>) for the concert '<strong>{updatedBooking.ConcertName}</strong>' has been successfully cancelled.</p><p>If you did not request this cancellation, please contact support immediately.</p><p>Thank you.</p>";
                var emailTextBody = $"Your booking (ID: {updatedBooking.Id}) for {updatedBooking.ConcertName} has been cancelled.";
                await _emailService.SendEmailAsync(userEmail, emailSubject, emailHtmlBody, emailTextBody);
                _logger.LogInformation("Booking cancellation email process initiated for BookingID {BookingId} to {UserEmail}", updatedBooking.Id, userEmail);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send booking cancellation email for BookingID {BookingId} to {UserEmail}, but cancellation was successful.", updatedBooking.Id, userEmail);
            }

            return BookingOperationResult<Booking>.SuccessNoContent();
        }
    }
}
