# syntax=docker/dockerfile:1.7
# Build once, produce three images via targets: api, worker, migrator.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json* HRS.sln ./
COPY src/HRS.Domain/HRS.Domain.csproj src/HRS.Domain/
COPY src/HRS.Application/HRS.Application.csproj src/HRS.Application/
COPY src/HRS.Infrastructure/HRS.Infrastructure.csproj src/HRS.Infrastructure/
COPY src/HRS.Api/HRS.Api.csproj src/HRS.Api/
COPY src/HRS.Worker/HRS.Worker.csproj src/HRS.Worker/
RUN dotnet restore src/HRS.Api/HRS.Api.csproj && dotnet restore src/HRS.Worker/HRS.Worker.csproj
COPY src/ src/
RUN dotnet publish src/HRS.Api/HRS.Api.csproj -c Release -o /out/api --no-restore \
 && dotnet publish src/HRS.Worker/HRS.Worker.csproj -c Release -o /out/worker --no-restore
RUN dotnet tool install --global dotnet-ef --version 10.* \
 && /root/.dotnet/tools/dotnet-ef migrations bundle \
      -p src/HRS.Infrastructure -s src/HRS.Api \
      --self-contained -r linux-x64 -o /out/migrator/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HRS.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "HRS.Worker.dll"]

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=build /out/migrator/efbundle .
USER $APP_UID
ENTRYPOINT ["/bin/sh", "-c", "./efbundle --connection \"$ConnectionStrings__Default\""]
