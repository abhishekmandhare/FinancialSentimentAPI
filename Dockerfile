# ── Stage 1: build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first — Docker layer cache means restore only re-runs
# when a .csproj changes, not on every source file change.
COPY Domain/Domain.csproj           Domain/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY API/API.csproj                 API/
RUN dotnet restore API/API.csproj

# Copy source and publish
COPY Domain/       Domain/
COPY Application/  Application/
COPY Infrastructure/ Infrastructure/
COPY API/          API/
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user — principle of least privilege
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

# Port 8080 — Cloud Run and TrueNAS both expect a non-privileged port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API.dll"]
