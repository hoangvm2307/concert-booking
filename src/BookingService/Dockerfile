# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["BookingService.csproj", "./"]
RUN dotnet restore "./BookingService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/."
RUN dotnet build "BookingService.csproj" -c Release -o /app/build

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish "BookingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the port the app will run on (default for ASP.NET Core is 8080 or 80)
# We'll use 8080 for HTTP and 8081 for HTTPS internally if Kestrel is configured for both.
# Docker compose will map these to host ports.
EXPOSE 8080
# EXPOSE 8081 # If you plan to use HTTPS internally as well

# Define the entry point for the application
ENTRYPOINT ["dotnet", "BookingService.dll"]