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

# Npgsql requires libgssapi for Kerberos probing (even when not used)
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl && rm -rf /var/lib/apt/lists/*

# Non-root user — .NET 10 images ship a built-in 'app' user (UID 1654)
USER app

COPY --from=build /app/publish .

# Port 8080 — Cloud Run and TrueNAS both expect a non-privileged port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API.dll"]
