using ConcertService.Models.Configuration;
using ConcertService.Services;
using ConcertService.Data;
using ConcertService.BackgroundServices;
var builder = WebApplication.CreateBuilder(args);

// 1. Configure MongoDB Settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// 2. Register ConcertManagementService
builder.Services.AddSingleton<ConcertManagementService>(); // Or AddScoped
builder.Services.Configure<ServiceUrls>(builder.Configuration.GetSection("ServiceUrls"));
builder.Services.AddHttpClient();
// Configure Redis settings
builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection("RedisSettings"));
builder.Services.AddHostedService<ConcertStatusUpdaterService>();
// --- Crucial for Controllers ---
builder.Services.AddControllers(); // This discovers and registers your controllers
// --- End Crucial for Controllers ---

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ConcertService API", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var concertManagementService = services.GetRequiredService<ConcertManagementService>();
        await DataSeeder.SeedConcertsAsync(concertManagementService, services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during data seeding.");
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConcertService API v1"));
}

// app.UseHttpsRedirection(); // Usually commented out for Docker non-HTTPS internal traffic

app.UseRouting(); // Good to have explicitly before UseAuthorization and MapControllers

app.UseAuthorization(); // Even if not actively used yet, good practice

// --- Crucial for mapping controller routes ---
app.MapControllers(); // This tells the app to use the routes defined in your controllers
// --- End Crucial for mapping controller routes ---

app.Run();