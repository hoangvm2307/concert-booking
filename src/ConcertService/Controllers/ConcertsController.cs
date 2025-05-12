using Microsoft.AspNetCore.Mvc;
using ConcertService.Models.DTOs;
using ConcertService.Services;
using ConcertService.Models.Results;

namespace ConcertService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConcertsController : ControllerBase
    {
        private readonly IConcertLogicService _concertService;
        private readonly ILogger<ConcertsController> _logger;

        public ConcertsController(IConcertLogicService concertService, ILogger<ConcertsController> logger)
        {
            _concertService = concertService;
            _logger = logger;
        }

        private IActionResult HandleConcertOperationResult<T>(ConcertOperationResult<T> result, string? createdActionName = null, object? routeValues = null) where T : class
        {
            if (result.IsSuccess)
            {
                // For POST success
                if (createdActionName != null && result.Data != null) return CreatedAtAction(createdActionName, routeValues, result.Data);

                // For GET All success
                if (result.DataList != null) return Ok(result.DataList);

                // For GET single success
                if (result.Data != null) return Ok(result.Data);

                // For NoContent
                if (result.HttpStatusCode == StatusCodes.Status204NoContent) return NoContent();

                // For Ok
                return Ok();
            }

            // Handle failures
            _logger.LogWarning("Concert operation failed in controller. ErrorType: {ErrorType}, Message: {ErrorMessage}", result.ErrorType, result.ErrorMessage);
            var errorResponse = new { message = result.ErrorMessage, type = result.ErrorType?.ToString() };

            return StatusCode(result.HttpStatusCode, errorResponse);
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ConcertResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllConcerts()
        {
            var result = await _concertService.GetAllConcertsAsync();
            return HandleConcertOperationResult(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ConcertResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConcertById(string id)
        {
            var result = await _concertService.GetConcertByIdAsync(id);
            return HandleConcertOperationResult(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ConcertResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateConcert([FromBody] CreateConcertRequestDto createConcertDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return HandleConcertOperationResult(
                    ConcertOperationResult<ConcertResponseDto>.Failure(string.Join("; ", errors), ConcertOperationErrorType.BadRequest, StatusCodes.Status400BadRequest)
                );
            }
            var result = await _concertService.CreateConcertAsync(createConcertDto);
            return HandleConcertOperationResult(result, nameof(GetConcertById), new { id = result.Data?.Id });
        }

        [HttpPost("{concertId}/seattypes")]
        [ProducesResponseType(typeof(SeatTypeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddSeatTypeToConcert(string concertId, [FromBody] AddSeatTypeRequestDto addSeatTypeDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return HandleConcertOperationResult(
                    ConcertOperationResult<SeatTypeDto>.Failure(string.Join("; ", errors), ConcertOperationErrorType.BadRequest, StatusCodes.Status400BadRequest)
                );
            }
            var result = await _concertService.AddSeatTypeToConcertAsync(concertId, addSeatTypeDto);

            if (result.IsSuccess && result.Data != null)
            {
                return StatusCode(StatusCodes.Status201Created, result.Data);
            }
            return HandleConcertOperationResult(result);
        }
    }
}
