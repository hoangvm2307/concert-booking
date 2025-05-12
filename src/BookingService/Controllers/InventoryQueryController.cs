using Microsoft.AspNetCore.Mvc;
using BookingService.Services;
using System.Threading.Tasks;

namespace BookingService.Controllers
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryQueryController : ControllerBase
    {
        private readonly TicketInventoryService _ticketInventoryService;
        private readonly ILogger<InventoryQueryController> _logger;

        public InventoryQueryController(TicketInventoryService ticketInventoryService, ILogger<InventoryQueryController> logger)
        {
            _ticketInventoryService = ticketInventoryService;
            _logger = logger;
        }

        [HttpGet("concert/{concertId}/seattype/{seatTypeId}")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRemainingTickets(string concertId, string seatTypeId)
        {
            _logger.LogInformation("InventoryQueryController: Received GET request for ConcertID: {ConcertId}, SeatTypeID: {SeatTypeId}", concertId, seatTypeId);
            var count = await _ticketInventoryService.GetRemainingTicketsAsync(concertId, seatTypeId);
            if (count.HasValue)
            {
                return Ok(count.Value);
            }
            _logger.LogWarning("Inventory count not found for ConcertID {ConcertId}, SeatTypeID {SeatTypeId}", concertId, seatTypeId);
            return NotFound(new { message = "Ticket inventory not found or not initialized for this seat type." });
        }
    }
}