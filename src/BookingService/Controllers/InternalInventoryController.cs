using Microsoft.AspNetCore.Mvc;
using BookingService.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookingService.Controllers
{
    
    [ApiController]
    [Route("api/internal/inventory")] 
    public class InternalInventoryController : ControllerBase
    {
        private readonly TicketInventoryService _ticketInventoryService;
        private readonly ILogger<InternalInventoryController> _logger;

        public InternalInventoryController(TicketInventoryService ticketInventoryService, ILogger<InternalInventoryController> logger)
        {
            _ticketInventoryService = ticketInventoryService;
            _logger = logger;
        }

        public class InitializeInventoryRequest
        {
            [Required]
            public required string ConcertId { get; set; }
            [Required]
            public List<SeatTypeInventoryInfo> SeatTypes { get; set; } = new List<SeatTypeInventoryInfo>();
        }

        public class SeatTypeInventoryInfo
        {
            [Required]
            public required string SeatTypeId { get; set; }
            [Required]
            [Range(0, int.MaxValue)]
            public int Count { get; set; }
        }

        [HttpPost("initialize")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InitializeInventory([FromBody] InitializeInventoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received request to initialize inventory for ConcertID: {ConcertId}", request.ConcertId);
            foreach (var seatType in request.SeatTypes)
            {
                await _ticketInventoryService.InitializeTicketCountAsync(request.ConcertId, seatType.SeatTypeId, seatType.Count);
                _logger.LogInformation("Initialized inventory for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}, Count: {Count}",
                    request.ConcertId, seatType.SeatTypeId, seatType.Count);
            }
            return Ok(new { message = $"Inventory initialization process completed for ConcertID: {request.ConcertId}" });
        }

        [HttpPost("disable-concert/{concertId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If concertId itself is not meaningful to inventory
        public async Task<IActionResult> DisableConcertInventory(string concertId)
        {
            _logger.LogInformation("Received request to disable inventory for ConcertID: {ConcertId}", concertId);

            // This will attempt to delete all keys matching "tickets:concertId:*"
            // TicketInventoryService needs a new method for this.
            bool success = await _ticketInventoryService.ClearInventoryForConcertAsync(concertId);

            if (success)
            {
                _logger.LogInformation("Successfully cleared/disabled inventory for ConcertID: {ConcertId}", concertId);
                return Ok(new { message = $"Inventory successfully disabled for concert {concertId}." });
            }
            else
            {
                // This might happen if there were no keys to delete, which is not necessarily an error for this operation.
                _logger.LogWarning("No inventory keys found or cleared for ConcertID: {ConcertId}. This might be normal if already cleared or never initialized.", concertId);
                return Ok(new { message = $"No inventory keys found/cleared for concert {concertId} (might be normal)." });
            }
        }
    }
}