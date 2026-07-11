# ── Build stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first for layer caching, then restore
COPY FitForge.csproj .
RUN dotnet restore FitForge.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish FitForge.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Container listens on port 8080 (works well with Render/Railway/Fly.io)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# .NET 8's Write-XOR-Execute JIT memory protection is known to trigger spurious
# SIGSEGV crashes (exit code 139) on some virtualized cloud containers, including
# Render. Disabling it trades a tiny amount of JIT hardening for actually starting up.
ENV DOTNET_EnableWriteXorExecute=0
EXPOSE 8080

ENTRYPOINT ["dotnet", "FitForge.dll"]
