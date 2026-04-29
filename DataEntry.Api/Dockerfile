# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DataEntry.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Create folder for optional persistent disk mount
RUN mkdir -p /data

EXPOSE 8080

# Render injects PORT env var; fall back to 8080
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} dotnet DataEntry.Api.dll"]
