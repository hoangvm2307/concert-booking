using BookingService.Models.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using BookingService.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("RedisSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<ServiceUrls>(builder.Configuration.GetSection("ServiceUrls"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// 2. Register MongoDB related services (BookingStorageService will be created next)
builder.Services.AddSingleton<BookingStorageService>();
// 3. Register Redis ConnectionMultiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>().Value;
    // Ensure exceptions during connect are handled or allowed to propagate for startup failure
    var configurationOptions = ConfigurationOptions.Parse(redisSettings.ConnectionString);
    configurationOptions.AbortOnConnectFail = true; // Fail fast if Redis is not available on startup
    return ConnectionMultiplexer.Connect(configurationOptions);
});


builder.Services.AddScoped<TicketInventoryService>();

builder.Services.AddScoped<IEmailService, MailKitEmailService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
if (string.IsNullOrEmpty(jwtSettings?.Issuer) || string.IsNullOrEmpty(jwtSettings.Audience))
{
    throw new InvalidOperationException("JWT Authority or Audience is not configured.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Audience = jwtSettings.Audience;
        options.RequireHttpsMetadata = false;

        var keyString = jwtSettings.Key;
        if (string.IsNullOrEmpty(keyString))
        {
            throw new InvalidOperationException("JWT Key is not configured in BookingService.");
        }
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            IssuerSigningKey = securityKey,
            ValidateIssuerSigningKey = true
            // ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BookingService API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\""
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookingService API v1"));
}

// app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();