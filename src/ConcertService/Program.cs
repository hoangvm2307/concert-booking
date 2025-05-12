using ConcertService.Models.Configuration;
using ConcertService.Services;
using ConcertService.Data;
using ConcertService.BackgroundServices;
using ConcertService.Repositories;
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<ServiceUrls>(builder.Configuration.GetSection("ServiceUrls"));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("RedisSettings"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IConcertRepository, ConcertRepository>();
builder.Services.AddScoped<IConcertLogicService, ConcertLogicService>();

builder.Services.AddHostedService<ConcertStatusUpdaterService>();

builder.Services.AddControllers();
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

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();