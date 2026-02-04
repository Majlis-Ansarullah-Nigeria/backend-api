# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Create logs directory and set permissions for the non-root 'app' user
RUN mkdir -p /app/logs && chown -R app:app /app/logs
USER app
EXPOSE 5001

# ... (build and publish stages remain the same) ...

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Default environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

# Ensure the logs directory exists for the app user
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "ManagementApi.Host.dll"]

