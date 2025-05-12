using Microsoft.AspNetCore.Mvc;
using ConcertService.Models;
using ConcertService.Models.DTOs;
using ConcertService.Services;
using MongoDB.Bson;
using ConcertService.Models.Configuration;
using Microsoft.Extensions.Options;

namespace ConcertService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConcertsController : ControllerBase
    {
        private readonly ConcertManagementService _concertManagementService;
        private readonly ILogger<ConcertsController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServiceUrls _serviceUrls;

        public ConcertsController(
                ConcertManagementService concertManagementService,
                IHttpClientFactory httpClientFactory,
                IOptions<ServiceUrls> serviceUrlsOptions,
                ILogger<ConcertsController> logger)
        {
            _concertManagementService = concertManagementService;
            _httpClientFactory = httpClientFactory;
            _serviceUrls = serviceUrlsOptions.Value;
            _logger = logger;
        }


        private async Task<ConcertResponseDto> MapConcertToDtoAsync(Concert concert)
        {
            var seatTypeDtos = new List<SeatTypeDto>();
            var httpClient = _httpClientFactory.CreateClient();
            var bookingServiceUrl = _serviceUrls.BookingService ?? throw new InvalidOperationException("BookingService URL not configured.");

            foreach (var stModel in concert.SeatTypes)
            {
                int remainingSeats = stModel.TotalSeats; // Default to total seats
                if (!string.IsNullOrEmpty(concert.Id) && !string.IsNullOrEmpty(stModel.Id))
                {
                    try
                    {
                        // Call BookingService to get real-time remaining seats
                        var inventoryUrl = $"{bookingServiceUrl}/api/inventory/concert/{concert.Id}/seattype/{stModel.Id}";
                        _logger.LogDebug("Fetching inventory from {Url}", inventoryUrl);
                        var response = await httpClient.GetAsync(inventoryUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            var countJson = await response.Content.ReadAsStringAsync();
                            if (int.TryParse(countJson, out int realTimeCount))
                            {
                                remainingSeats = realTimeCount;
                            }
                            else
                            {
                                _logger.LogWarning("Could not parse remaining seats from BookingService for concert {ConcertId}, seat type {SeatTypeId}. Response: {ResponseJson}", concert.Id, stModel.Id, countJson);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to fetch remaining seats from BookingService for concert {ConcertId}, seat type {SeatTypeId}. Status: {StatusCode}", concert.Id, stModel.Id, response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching remaining seats for concert {ConcertId}, seat type {SeatTypeId}", concert.Id, stModel.Id);
                        // Fallback to TotalSeats or a predefined error value if needed
                    }
                }

                seatTypeDtos.Add(new SeatTypeDto
                {
                    Id = stModel.Id,
                    Name = stModel.Name,
                    Price = stModel.Price,
                    TotalSeats = stModel.TotalSeats,
                    RemainingSeats = remainingSeats
                });
            }

            return new ConcertResponseDto
            {
                Id = concert.Id,
                Name = concert.Name,
                Venue = concert.Venue,
                Date = concert.Date,
                StartTime = concert.StartTime,
                Description = concert.Description,
                IsBookingEnabled = concert.IsBookingEnabled,
                SeatTypes = seatTypeDtos,
                CreatedAt = concert.CreatedAt
            };
        }


        // GET /api/concerts
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ConcertResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllConcerts()
        {
            var concerts = await _concertManagementService.GetAllAsync();
            var concertDtos = new List<ConcertResponseDto>();
            foreach (var concert in concerts)
            {
                concertDtos.Add(await MapConcertToDtoAsync(concert));
            }
            return Ok(concertDtos);
        }

        // GET /api/concerts/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ConcertResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConcertById(string id)
        {
            if (!ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid concert ID format." });
            }
            var concert = await _concertManagementService.GetByIdAsync(id);
            if (concert == null)
            {
                return NotFound(new { message = $"Concert with ID '{id}' not found." });
            }
            return Ok(await MapConcertToDtoAsync(concert));
        }

        // POST /api/concerts (Admin endpoint)
        [HttpPost]
        [ProducesResponseType(typeof(ConcertResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateConcert([FromBody] CreateConcertRequestDto createConcertDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate StartTime is after Date (or combined Date and StartTime is in future)
            // For simplicity, we're assuming Date holds the date part and StartTime holds the full date-time for the start.
            // A better approach might be to have Date be just a DateOnly and StartTime be a TimeOnly,
            // then combine them or have StartTime be the definitive UTC DateTime.
            // Let's assume createConcertDto.StartTime is the actual event start DateTime.
            if (createConcertDto.StartTime <= DateTime.UtcNow) // Ensure StartTime is in the future
            {
                ModelState.AddModelError(nameof(createConcertDto.StartTime), "Concert start time must be in the future.");
                return BadRequest(ModelState);
            }
            if (createConcertDto.Date.Date != createConcertDto.StartTime.Date)
            {
                ModelState.AddModelError(nameof(createConcertDto.Date), "The date part of 'Date' and 'StartTime' must match.");
                return BadRequest(ModelState);
            }


            var newConcert = new Concert
            {
                Name = createConcertDto.Name,
                Venue = createConcertDto.Venue,
                Date = createConcertDto.Date.Date, // Store only the date part if that's the intent
                StartTime = createConcertDto.StartTime,
                Description = createConcertDto.Description,
                IsBookingEnabled = true, // Default when creating
                CreatedAt = DateTime.UtcNow,
                SeatTypes = createConcertDto.SeatTypes.Select(stDto => new SeatType
                {
                    // Id will be generated by MongoDB driver or ConcertManagementService
                    Name = stDto.Name,
                    Price = stDto.Price,
                    TotalSeats = stDto.TotalSeats,
                    RemainingSeats = stDto.TotalSeats // Initialize RemainingSeats
                }).ToList()
            };

            await _concertManagementService.CreateConcertAsync(newConcert);

            // newConcert.Id should be populated by the service/driver
            if (string.IsNullOrEmpty(newConcert.Id))
            {
                _logger.LogError("Concert ID was null after creation for concert named {ConcertName}", newConcert.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create concert due to missing ID after save.");
            }

            return CreatedAtAction(nameof(GetConcertById), new { id = newConcert.Id }, await MapConcertToDtoAsync(newConcert));
        }

        // POST /api/concerts/{concertId}/seattypes (Admin endpoint)
        [HttpPost("{concertId}/seattypes")]
        [ProducesResponseType(typeof(SeatTypeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddSeatTypeToConcert(string concertId, [FromBody] AddSeatTypeRequestDto addSeatTypeDto)
        {
            if (!ObjectId.TryParse(concertId, out _))
            {
                return BadRequest(new { message = "Invalid concert ID format." });
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var concert = await _concertManagementService.GetByIdAsync(concertId);
            if (concert == null)
            {
                return NotFound(new { message = $"Concert with ID '{concertId}' not found." });
            }

            // Check for duplicate seat type name for this concert
            if (concert.SeatTypes.Any(st => st.Name.Equals(addSeatTypeDto.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = $"Seat type with name '{addSeatTypeDto.Name}' already exists for this concert." });
            }

            var newSeatType = new SeatType
            {
                // Id will be generated by MongoDB driver or ConcertManagementService
                Name = addSeatTypeDto.Name,
                Price = addSeatTypeDto.Price,
                TotalSeats = addSeatTypeDto.TotalSeats,
                RemainingSeats = addSeatTypeDto.TotalSeats // Initialize RemainingSeats
            };

            await _concertManagementService.AddSeatTypeAsync(concertId, newSeatType);

            // newSeatType.Id should be populated by the service/driver
            if (string.IsNullOrEmpty(newSeatType.Id))
            {
                _logger.LogError("SeatType ID was null after adding to concert {ConcertId} for seat type {SeatTypeName}", concertId, newSeatType.Name);
                // To return the created seat type, we would need the service to ensure the ID is populated on the passed object
                // or refetch the concert. For now, we'll return a success message or the DTO if ID is populated.
                // The AddSeatTypeAsync in the service should ideally ensure the ID is set on the newSeatType object.
                // Let's assume it is for now, or adjust service.
                // As a fallback, you might refetch the concert to get the newly added seat type with its ID.
                var updatedConcert = await _concertManagementService.GetByIdAsync(concertId);
                var createdSeatType = updatedConcert?.SeatTypes.FirstOrDefault(st => st.Name == newSeatType.Name); // This is a bit heuristic
                if (createdSeatType == null || string.IsNullOrEmpty(createdSeatType.Id))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to add seat type or retrieve its ID.");
                }
                newSeatType.Id = createdSeatType.Id; // Ensure ID is set
            }


            var seatTypeDto = new SeatTypeDto
            {
                Id = newSeatType.Id,
                Name = newSeatType.Name,
                Price = newSeatType.Price,
                TotalSeats = newSeatType.TotalSeats,
                RemainingSeats = newSeatType.RemainingSeats
            };

            // The location header for a sub-resource can be tricky.
            // Returning 201 with the object is common.
            return StatusCode(StatusCodes.Status201Created, seatTypeDto);
        }
    }
}