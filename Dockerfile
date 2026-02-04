# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Create logs directory and set permissions for the non-root 'app' user
RUN mkdir -p /app/logs && chown -R app:app /app/logs
USER app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files for efficient caching
COPY ["ManagementApi.sln", "./"]
COPY ["src/Core/Domain/Domain.csproj", "src/Core/Domain/"]
COPY ["src/Core/Application/Application.csproj", "src/Core/Application/"]
COPY ["src/Core/Shared/Shared.csproj", "src/Core/Shared/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Host/Host.csproj", "src/Host/"]

# Restore dependencies
RUN dotnet restore "ManagementApi.sln"

# Copy the remaining source code
COPY . .
WORKDIR "/src/src/Host"

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Environment variables for production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUN_LEVEL=Production

ENTRYPOINT ["dotnet", "ManagementApi.Host.dll"]