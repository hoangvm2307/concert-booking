using ConcertService.Models.Configuration;
using ConcertService.Repositories;
using ConcertService.Services;
using Microsoft.Extensions.Options;

namespace ConcertService.BackgroundServices
{
    public class ConcertStatusUpdaterService : BackgroundService
    {
        private readonly ILogger<ConcertStatusUpdaterService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceUrls _serviceUrls;
        private Timer? _timer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public ConcertStatusUpdaterService(
            ILogger<ConcertStatusUpdaterService> logger,
            IServiceProvider serviceProvider,
            IOptions<ServiceUrls> serviceUrlsOptions)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _serviceUrls = serviceUrlsOptions.Value;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConcertStatusUpdaterService is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, _checkInterval);

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("ConcertStatusUpdaterService is working. Current time: {time}", DateTimeOffset.Now);

            using (var scope = _serviceProvider.CreateScope())
            {
                var concertRepository = scope.ServiceProvider.GetRequiredService<IConcertRepository>();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

                try
                {
                    var concertsToDisable = await concertRepository.GetConcertsToDisableBookingAsync(DateTime.UtcNow);

                    if (!concertsToDisable.Any())
                    {
                        _logger.LogInformation("No concerts found requiring booking status update.");
                        return;
                    }

                    foreach (var concert in concertsToDisable)
                    {
                        if (concert.Id == null) continue;

                        _logger.LogInformation("Disabling bookings for concert ID: {ConcertId}, Name: {ConcertName}, StartTime: {StartTime}",
                            concert.Id, concert.Name, concert.StartTime);

                        concert.IsBookingEnabled = false;
                        bool updateResult = await concertRepository.UpdateConcertAsync(concert.Id, concert);

                        if (updateResult)
                        {
                            _logger.LogInformation("Successfully updated IsBookingEnabled for concert ID: {ConcertId} in local DB.", concert.Id);

                            await NotifyBookingServiceToDisableInventoryAsync(httpClientFactory, concert.Id);
                        }
                        else
                        {
                            _logger.LogError("Failed to update IsBookingEnabled for concert ID: {ConcertId} in local DB.", concert.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while checking or updating concert statuses.");
                }
            }
        }

        private async Task NotifyBookingServiceToDisableInventoryAsync(IHttpClientFactory httpClientFactory, string concertId)
        {
            if (string.IsNullOrEmpty(_serviceUrls.BookingService))
            {
                _logger.LogError("BookingService URL is not configured. Cannot notify to disable inventory for concert {ConcertId}.", concertId);
                return;
            }

            try
            {
                var httpClient = httpClientFactory.CreateClient();
                string disableInventoryUrl = $"{_serviceUrls.BookingService}/api/internal/inventory/disable-concert/{concertId}";

                _logger.LogInformation("Notifying BookingService to disable inventory for ConcertID {ConcertId} at {Url}", concertId, disableInventoryUrl);

                var response = await httpClient.PostAsync(disableInventoryUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully notified BookingService to disable inventory for ConcertID: {ConcertId}", concertId);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to notify BookingService to disable inventory for ConcertID: {ConcertId}. Status: {StatusCode}. Response: {Response}",
                        concertId, response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while notifying BookingService to disable inventory for ConcertID: {ConcertId}", concertId);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConcertStatusUpdaterService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            await base.StopAsync(stoppingToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}