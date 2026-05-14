# ──────────────────────────────────────────────────────────────────────────────
# Stage 1: Build
# ──────────────────────────────────────────────────────────────────────────────
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
ENV TZ=UTC

# Copy csproj files first to leverage Docker layer caching for restore step.
# Restore is only re-run when .csproj files change.
COPY src/DotNetMcp.Backend/DotNetMcp.Backend.csproj src/DotNetMcp.Backend/
COPY src/DotNetMcp.Server/DotNetMcp.Server.csproj src/DotNetMcp.Server/

RUN dotnet restore src/DotNetMcp.Server/DotNetMcp.Server.csproj

# Copy remaining source and publish
COPY src/ src/

RUN dotnet publish src/DotNetMcp.Server/DotNetMcp.Server.csproj \
    -c Release \
    -o /app \
    --no-restore

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2: Runtime
# ──────────────────────────────────────────────────────────────────────────────
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV TZ=UTC \
    ASPNETCORE_URLS=http://+:5000

# Install curl for HEALTHCHECK.
# Trade-off: adds ~3 MB to image size but gives reliable HTTP liveness probes
# without requiring a separate healthcheck binary. wget is an alternative but
# curl is more idiomatic and available in most debugging workflows.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 5000

# /health endpoint is excluded from API Key auth (public path).
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

COPY --from=build /app .

# Run as non-root user (app user is built into the aspnet base image)
USER app

ENTRYPOINT ["dotnet", "DotNetMcp.Server.dll"]
