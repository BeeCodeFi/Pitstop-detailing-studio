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

# Disable inotify file watcher (not available on Render free tier - causes SIGSEGV)
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

# Create folder for optional persistent disk mount
RUN mkdir -p /data

EXPOSE 8080

CMD ["dotnet", "DataEntry.Api.dll"]
