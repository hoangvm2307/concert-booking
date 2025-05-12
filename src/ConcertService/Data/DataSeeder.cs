using ConcertService.Models;
using ConcertService.Services;

namespace ConcertService.Data
{
    public static class DataSeeder
    {
        public static async Task SeedConcertsAsync(ConcertManagementService concertManagementService, IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();

            if (await concertManagementService.IsConcertsCollectionEmptyAsync())
            {
                logger.LogInformation("No concerts found in database. Seeding sample data...");

                var concertsToSeed = GetSampleConcerts();
                foreach (var concert in concertsToSeed)
                {
                    try
                    {
                        await concertManagementService.CreateConcertAsync(concert);
                        logger.LogInformation("Seeded concert: {ConcertName}", concert.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error seeding concert: {ConcertName}", concert.Name);
                    }
                }
            }
            else
            {
                logger.LogInformation("Concerts collection is not empty. Skipping seed data.");
            }
        }

        private static List<Concert> GetSampleConcerts()
        {
            int currentYear = DateTime.UtcNow.Year;

            return new List<Concert>
            {
                new Concert
                {
                    Name = "The Galactic Grooves - Cosmos Tour",
                    Venue = "Nebula Arena, Alpha Centauri",
                    Date = new DateTime(currentYear + 1, 7, 20, 0, 0, 0, DateTimeKind.Utc).Date,
                    StartTime = new DateTime(currentYear + 1, 7, 20, 19, 0, 0, DateTimeKind.Utc),
                    Description = "Experience the out-of-this-world sound of The Galactic Grooves on their latest tour.",
                    IsBookingEnabled = true,
                    SeatTypes = new List<SeatType>
                    {
                        new SeatType { Name = "VIP Stardust Lounge", Price = 250.00m, TotalSeats = 100 },
                        new SeatType { Name = "General Admission Orbit", Price = 90.50m, TotalSeats = 500 },
                        new SeatType { Name = "Standing Meteor Pit", Price = 65.00m, TotalSeats = 1000 }
                    },
                    CreatedAt = DateTime.UtcNow
                },
                new Concert
                {
                    Name = "Echoes of Tomorrow - Synthesized Dreams",
                    Venue = "Cyber Hall, Neo-Tokyo",
                    Date = new DateTime(currentYear + 1, 9, 5, 0, 0, 0, DateTimeKind.Utc).Date,
                    StartTime = new DateTime(currentYear + 1, 9, 5, 20, 30, 0, DateTimeKind.Utc),
                    Description = "Dive into the future with the mesmerizing electronic soundscapes of Echoes of Tomorrow.",
                    IsBookingEnabled = true,
                    SeatTypes = new List<SeatType>
                    {
                        new SeatType { Name = "Premium Cyber Pod", Price = 180.00m, TotalSeats = 60 },
                        new SeatType { Name = "Standard Circuit Seat", Price = 75.00m, TotalSeats = 300 },
                        new SeatType { Name = "Data Stream Standing", Price = 50.00m, TotalSeats = 600 }
                    },
                    CreatedAt = DateTime.UtcNow
                },
                new Concert
                {
                    Name = "Acoustic Alchemy - Unplugged Serenity",
                    Venue = "The Whispering Pines Amphitheater",
                    Date = new DateTime(currentYear + 1, 11, 12, 0, 0, 0, DateTimeKind.Utc).Date,
                    StartTime = new DateTime(currentYear + 1, 11, 12, 18, 0, 0, DateTimeKind.Utc),
                    Description = "A peaceful evening with the pure, unplugged melodies of Acoustic Alchemy.",
                    IsBookingEnabled = true,
                    SeatTypes = new List<SeatType>
                    {
                        new SeatType { Name = "Front Row Harmony", Price = 120.00m, TotalSeats = 80 },
                        new SeatType { Name = "Meadow Seating", Price = 55.00m, TotalSeats = 400 }
                    },
                    CreatedAt = DateTime.UtcNow
                },
                new Concert
                {
                    Name = "Yesterday's Legends - Encore Performance (Past Test)", // Renamed slightly for clarity
                    Venue = "Historic Royal Hall",
                    // Set StartTime to be in the past relative to when the service runs
                    // For example, if current UTC is May 10, 2025, 14:00:00, set this to May 10, 2025, 10:00:00 UTC
                    Date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0,0,0, DateTimeKind.Utc).AddDays(-1).Date, // Example: Yesterday
                    StartTime = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0,0,0, DateTimeKind.Utc).AddDays(-1).AddHours(10), // Example: Yesterday 10 AM UTC
                    Description = "A replay of a legendary performance, set to be auto-disabled.",
                    IsBookingEnabled = true, // <<<< MODIFIED: Start as true so the service can disable it
                     SeatTypes = new List<SeatType>
                    {
                        new SeatType { Name = "Archive Seat", Price = 30.00m, TotalSeats = 200 }
                    },
                    CreatedAt = DateTime.UtcNow.AddDays(-2) // Ensure CreatedAt is also in the past
                }
            };
        }
    }
}
