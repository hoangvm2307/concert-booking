namespace ConcertService.Models.Results
{
    public enum ConcertOperationErrorType
    {
        // General Errors
        ServiceError, // Generic internal error
        BadRequest,   // Invalid input from client

        // Concert/SeatType Related Errors
        ConcertNotFound,
        SeatTypeAlreadyExists,
        InvalidConcertData, // For issues like start time in past

        // Communication Errors
        BookingServiceCommunicationError // For when fetching seat counts
    }

    public class ConcertOperationResult<T> where T : class
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public List<T>? DataList { get; private set; } // For list results
        public string? ErrorMessage { get; private set; }
        public ConcertOperationErrorType? ErrorType { get; private set; }
        public int HttpStatusCode { get; private set; } // To help controller map to HTTP status

        private ConcertOperationResult(bool isSuccess, T? data, List<T>? dataList, string? errorMessage, ConcertOperationErrorType? errorType, int httpStatusCode)
        {
            IsSuccess = isSuccess;
            Data = data;
            DataList = dataList;
            ErrorMessage = errorMessage;
            ErrorType = errorType;
            HttpStatusCode = httpStatusCode;
        }

        // Success for a single item (e.g., GetById, Create, Update)
        public static ConcertOperationResult<T> Success(T data, int httpStatusCode = StatusCodes.Status200OK)
        {
            return new ConcertOperationResult<T>(true, data, null, null, null, httpStatusCode);
        }
        
        // Success for a list of items (e.g., GetAll)
        public static ConcertOperationResult<T> Success(List<T> dataList)
        {
            return new ConcertOperationResult<T>(true, null, dataList, null, null, StatusCodes.Status200OK);
        }
        
        // Success for creation (HTTP 201)
        public static ConcertOperationResult<T> SuccessCreated(T data)
        {
            return new ConcertOperationResult<T>(true, data, null, null, null, StatusCodes.Status201Created);
        }
        
        // Success with no content to return (e.g., for DELETE if we had one)
        public static ConcertOperationResult<T> SuccessNoContent()
        {
            return new ConcertOperationResult<T>(true, null, null, null, null, StatusCodes.Status204NoContent);
        }

        // Failure result
        public static ConcertOperationResult<T> Failure(string message, ConcertOperationErrorType errorType, int httpStatusCode = StatusCodes.Status500InternalServerError)
        {
            return new ConcertOperationResult<T>(false, null, null, message, errorType, httpStatusCode);
        }
    }
}
