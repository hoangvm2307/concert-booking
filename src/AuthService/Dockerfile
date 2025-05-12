FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["AuthService.csproj", "./"]
RUN dotnet restore "./AuthService.csproj"

COPY . .
WORKDIR "/src/."
RUN dotnet build "AuthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .


EXPOSE 8080

ENTRYPOINT ["dotnet", "AuthService.dll"]