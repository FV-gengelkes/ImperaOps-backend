# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy build props and project files first for layer caching
COPY Directory.Build.props ./
COPY src/ImperaOps.Api/ImperaOps.Api.csproj src/ImperaOps.Api/
COPY src/ImperaOps.Application/ImperaOps.Application.csproj src/ImperaOps.Application/
COPY src/ImperaOps.Domain/ImperaOps.Domain.csproj src/ImperaOps.Domain/
COPY src/ImperaOps.Infrastructure/ImperaOps.Infrastructure.csproj src/ImperaOps.Infrastructure/

# Restore only the API project (pulls transitive deps)
RUN dotnet restore src/ImperaOps.Api/ImperaOps.Api.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/ImperaOps.Api/ImperaOps.Api.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Write a build ID for version detection (used by /health/version)
RUN date -u +"%Y-%m-%dT%H:%M:%SZ" > /app/build-id

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

ENTRYPOINT ["dotnet", "ImperaOps.Api.dll"]
