using ConcertService.Models;
using ConcertService.Models.Configuration;
using ConcertService.Models.DTOs;
using ConcertService.Models.Results;
using ConcertService.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Text;
using System.Text.Json;

namespace ConcertService.Services
{
    public class ConcertLogicService : IConcertLogicService
    {
        private readonly IConcertRepository _concertRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServiceUrls _serviceUrls;
        private readonly ILogger<ConcertLogicService> _logger;

        public ConcertLogicService(
            IConcertRepository concertRepository,
            IHttpClientFactory httpClientFactory,
            IOptions<ServiceUrls> serviceUrlsOptions,
            ILogger<ConcertLogicService> logger)
        {
            _concertRepository = concertRepository;
            _httpClientFactory = httpClientFactory;
            _serviceUrls = serviceUrlsOptions.Value;
            _logger = logger;
        }

        private async Task NotifyBookingServiceOfInventoryAsync(Concert concert)
        {
            if (string.IsNullOrEmpty(_serviceUrls.BookingService) || concert.Id == null || !concert.SeatTypes.Any())
            {
                _logger.LogWarning("BookingService URL not configured, concert ID is null, or no seat types for concert {ConcertId}. Skipping inventory notification.", concert?.Id ?? "N/A");
                return;
            }

            var inventoryData = new InitializeInventoryRequestInternalDto
            {
                ConcertId = concert.Id,
                SeatTypes = concert.SeatTypes.Select(st => new SeatTypeInventoryInfoInternalDto
                {
                    SeatTypeId = st.Id,
                    Count = st.TotalSeats
                }).ToList()
            };

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var jsonPayload = JsonSerializer.Serialize(inventoryData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                string bookingServiceInventoryUrl = $"{_serviceUrls.BookingService}/api/internal/inventory/initialize";
                _logger.LogInformation("Notifying BookingService to initialize inventory for ConcertID {ConcertId} at {Url}", concert.Id, bookingServiceInventoryUrl);

                var response = await httpClient.PostAsync(bookingServiceInventoryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully notified BookingService for ConcertID: {ConcertId}", concert.Id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to notify BookingService for ConcertID: {ConcertId}. Status: {StatusCode}. Response: {Response}",
                        concert.Id, response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while notifying BookingService for ConcertID: {ConcertId}", concert.Id);
            }
        }

        private async Task<ConcertResponseDto> MapConcertToDtoWithRealtimeInventoryAsync(Concert concert)
        {
            var seatTypeDtos = new List<SeatTypeDto>();
            var httpClient = _httpClientFactory.CreateClient();
            var bookingServiceUrl = _serviceUrls.BookingService ?? throw new InvalidOperationException("BookingService URL not configured.");

            foreach (var stModel in concert.SeatTypes)
            {
                int remainingSeats = stModel.TotalSeats;
                if (!string.IsNullOrEmpty(concert.Id) && !string.IsNullOrEmpty(stModel.Id) && concert.IsBookingEnabled && concert.StartTime > DateTime.UtcNow)
                {
                    try
                    {
                        var inventoryUrl = $"{bookingServiceUrl}/api/inventory/concert/{concert.Id}/seattype/{stModel.Id}";
                        _logger.LogDebug("Fetching inventory from {Url} for concert {ConcertId}, seat type {SeatTypeId}", inventoryUrl, concert.Id, stModel.Id);
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
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning("Inventory not found in BookingService for concert {ConcertId}, seat type {SeatTypeId}. Assuming 0 available.", concert.Id, stModel.Id);
                            remainingSeats = 0;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to fetch remaining seats from BookingService for concert {ConcertId}, seat type {SeatTypeId}. Status: {StatusCode}. Content: {ErrorContent}", concert.Id, stModel.Id, response.StatusCode, errorContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching remaining seats for concert {ConcertId}, seat type {SeatTypeId}", concert.Id, stModel.Id);
                    }
                }
                else if (!concert.IsBookingEnabled || concert.StartTime <= DateTime.UtcNow)
                {
                    remainingSeats = 0;
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

        public async Task<ConcertOperationResult<ConcertResponseDto>> GetAllConcertsAsync()
        {
            _logger.LogInformation("Fetching all concerts via ConcertLogicService.");
            var concerts = await _concertRepository.GetAllAsync();
            var concertResponseDtos = new List<ConcertResponseDto>();
            foreach (var concert in concerts)
            {
                concertResponseDtos.Add(await MapConcertToDtoWithRealtimeInventoryAsync(concert));
            }
            return ConcertOperationResult<ConcertResponseDto>.Success(concertResponseDtos);
        }

        public async Task<ConcertOperationResult<ConcertResponseDto>> GetConcertByIdAsync(string id)
        {
            _logger.LogInformation("Fetching concert by ID: {ConcertId} via ConcertLogicService.", id);
            if (!ObjectId.TryParse(id, out _))
            {
                return ConcertOperationResult<ConcertResponseDto>.Failure("Invalid concert ID format.", ConcertOperationErrorType.BadRequest, StatusCodes.Status400BadRequest);
            }

            var concert = await _concertRepository.GetByIdAsync(id);
            if (concert == null)
            {
                return ConcertOperationResult<ConcertResponseDto>.Failure($"Concert with ID '{id}' not found.", ConcertOperationErrorType.ConcertNotFound, StatusCodes.Status404NotFound);
            }
            var dto = await MapConcertToDtoWithRealtimeInventoryAsync(concert);
            return ConcertOperationResult<ConcertResponseDto>.Success(dto);
        }

        public async Task<ConcertOperationResult<ConcertResponseDto>> CreateConcertAsync(CreateConcertRequestDto createConcertDto)
        {
            _logger.LogInformation("Attempting to create concert: {ConcertName} via ConcertLogicService.", createConcertDto.Name);
            if (createConcertDto.StartTime <= DateTime.UtcNow)
            {
                return ConcertOperationResult<ConcertResponseDto>.Failure("Concert start time must be in the future.", ConcertOperationErrorType.InvalidConcertData, StatusCodes.Status400BadRequest);
            }
            if (createConcertDto.Date.Date != createConcertDto.StartTime.Date)
            {
                return ConcertOperationResult<ConcertResponseDto>.Failure("The date part of 'Date' and 'StartTime' must match.", ConcertOperationErrorType.InvalidConcertData, StatusCodes.Status400BadRequest);
            }

            var newConcert = new Concert
            {
                Name = createConcertDto.Name,
                Venue = createConcertDto.Venue,
                Date = createConcertDto.Date.Date,
                StartTime = createConcertDto.StartTime,
                Description = createConcertDto.Description,
                IsBookingEnabled = true,
                CreatedAt = DateTime.UtcNow,
                SeatTypes = createConcertDto.SeatTypes.Select(stDto => new SeatType
                {
                    Name = stDto.Name,
                    Price = stDto.Price,
                    TotalSeats = stDto.TotalSeats,
                    RemainingSeats = stDto.TotalSeats
                }).ToList()
            };
            foreach (var seatType in newConcert.SeatTypes)
            {
                if (string.IsNullOrEmpty(seatType.Id)) seatType.Id = ObjectId.GenerateNewId().ToString();
            }

            var createdConcert = await _concertRepository.CreateConcertAsync(newConcert);

            if (createdConcert == null || string.IsNullOrEmpty(createdConcert.Id))
            {
                _logger.LogError("Concert ID was null after repository creation for concert named {ConcertName}", newConcert.Name);
                return ConcertOperationResult<ConcertResponseDto>.Failure("Failed to create concert or retrieve its ID after save.", ConcertOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            await NotifyBookingServiceOfInventoryAsync(createdConcert);

            var createdDto = await MapConcertToDtoWithRealtimeInventoryAsync(createdConcert);
            return ConcertOperationResult<ConcertResponseDto>.SuccessCreated(createdDto);
        }

        public async Task<ConcertOperationResult<SeatTypeDto>> AddSeatTypeToConcertAsync(string concertId, AddSeatTypeRequestDto addSeatTypeDto)
        {
            _logger.LogInformation("Attempting to add seat type {SeatTypeName} to concert ID: {ConcertId} via ConcertLogicService.", addSeatTypeDto.Name, concertId);
            if (!ObjectId.TryParse(concertId, out _))
            {
                return ConcertOperationResult<SeatTypeDto>.Failure("Invalid concert ID format.", ConcertOperationErrorType.BadRequest, StatusCodes.Status400BadRequest);
            }

            var concert = await _concertRepository.GetByIdAsync(concertId);
            if (concert == null)
            {
                return ConcertOperationResult<SeatTypeDto>.Failure($"Concert with ID '{concertId}' not found.", ConcertOperationErrorType.ConcertNotFound, StatusCodes.Status404NotFound);
            }

            if (concert.SeatTypes.Any(st => st.Name.Equals(addSeatTypeDto.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return ConcertOperationResult<SeatTypeDto>.Failure($"Seat type with name '{addSeatTypeDto.Name}' already exists for this concert.", ConcertOperationErrorType.SeatTypeAlreadyExists, StatusCodes.Status409Conflict);
            }

            var newSeatType = new SeatType
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = addSeatTypeDto.Name,
                Price = addSeatTypeDto.Price,
                TotalSeats = addSeatTypeDto.TotalSeats,
                RemainingSeats = addSeatTypeDto.TotalSeats
            };

            var updatedConcert = await _concertRepository.AddSeatTypeAsync(concertId, newSeatType);

            if (updatedConcert == null)
            {
                _logger.LogError("Failed to add seat type to concert {ConcertId} in repository.", concertId);
                return ConcertOperationResult<SeatTypeDto>.Failure("Failed to add seat type to the concert.", ConcertOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            await NotifyBookingServiceOfInventoryAsync(updatedConcert);

            var addedSeatTypeFromConcert = updatedConcert.SeatTypes.FirstOrDefault(st => st.Id == newSeatType.Id);
            if (addedSeatTypeFromConcert == null)
            {
                _logger.LogError("Newly added seat type with ID {SeatTypeId} not found in concert {ConcertId} after repository update.", newSeatType.Id, concertId);
                return ConcertOperationResult<SeatTypeDto>.Failure("Failed to retrieve details of added seat type.", ConcertOperationErrorType.ServiceError, StatusCodes.Status500InternalServerError);
            }

            var seatTypeDto = new SeatTypeDto
            {
                Id = addedSeatTypeFromConcert.Id,
                Name = addedSeatTypeFromConcert.Name,
                Price = addedSeatTypeFromConcert.Price,
                TotalSeats = addedSeatTypeFromConcert.TotalSeats,
                RemainingSeats = addedSeatTypeFromConcert.RemainingSeats
            };
            return ConcertOperationResult<SeatTypeDto>.SuccessCreated(seatTypeDto);
        }
    }
}
