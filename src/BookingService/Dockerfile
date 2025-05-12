FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BookingService.csproj", "./"]
RUN dotnet restore "./BookingService.csproj"

COPY . .
WORKDIR "/src/."
RUN dotnet build "BookingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BookingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .


EXPOSE 8080

ENTRYPOINT ["dotnet", "BookingService.dll"]