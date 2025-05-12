namespace BookingService.Models.Results
{
    public enum BookingOperationErrorType
    {
        // General Errors
        ServiceError,
        BadRequest,

        // Concert/SeatType Related Errors
        ConcertNotFound,
        SeatTypeNotFound,
        ConcertNotBookable,
        ConcertAlreadyStarted,

        // Inventory/Booking State Errors
        AlreadyBookedByUser,
        TicketsSoldOut,
        InventoryKeyNotFound,
        InventoryUpdateFailed,
        BookingNotFound,
        BookingNotCancellable,

        // Permission Errors
        ForbiddenAccess,

        // Communication Errors
        ConcertServiceCommunicationError
    }

    public class BookingOperationResult<T> where T : class
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string? ErrorMessage { get; private set; }
        public BookingOperationErrorType? ErrorType { get; private set; }
        public int? HttpStatusCode { get; private set; }

        private BookingOperationResult(bool isSuccess, T? data, string? errorMessage, BookingOperationErrorType? errorType, int? httpStatusCode = null)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorMessage = errorMessage;
            ErrorType = errorType;
            HttpStatusCode = httpStatusCode;
        }

        public static BookingOperationResult<T> Success(T data)
        {
            return new BookingOperationResult<T>(true, data, null, null, StatusCodes.Status200OK);
        }

        public static BookingOperationResult<T> SuccessNoContent()
        {
            return new BookingOperationResult<T>(true, null, null, null, StatusCodes.Status204NoContent);
        }


        public static BookingOperationResult<T> Failure(string message, BookingOperationErrorType errorType, int? httpStatusCode = null)
        {
            return new BookingOperationResult<T>(false, null, message, errorType, httpStatusCode ?? StatusCodes.Status500InternalServerError);
        }
    }
}
