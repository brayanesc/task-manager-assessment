# ── Stage 1: build & publish ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Build.props first (affects all projects)
COPY Directory.Build.props ./

# Copy only .csproj files for a layer-cached restore step.
# Changes to source code won't invalidate the restore layer.
COPY src/TaskManager.Domain/TaskManager.Domain.csproj               src/TaskManager.Domain/
COPY src/TaskManager.Application/TaskManager.Application.csproj     src/TaskManager.Application/
COPY src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj src/TaskManager.Infrastructure/
COPY src/TaskManager.Api/TaskManager.Api.csproj                     src/TaskManager.Api/

RUN dotnet restore src/TaskManager.Api/TaskManager.Api.csproj

# Copy source and publish in Release mode
COPY src/ src/
RUN dotnet publish src/TaskManager.Api/TaskManager.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for the HEALTHCHECK
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Directory for the SQLite volume mount
RUN mkdir -p /data

COPY --from=build /app/publish .

# Ping the OpenAPI spec endpoint to verify the API is responsive
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:5111/openapi/v1.json || exit 1

ENV ASPNETCORE_URLS=http://+:5111
EXPOSE 5111

ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
