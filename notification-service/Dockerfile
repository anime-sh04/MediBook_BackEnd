# ── Stage 1: Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore — layer-cached unless .csproj changes
COPY src/MediBook.Notification.API/MediBook.Notification.API.csproj ./src/MediBook.Notification.API/
RUN dotnet restore src/MediBook.Notification.API/MediBook.Notification.API.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/MediBook.Notification.API/MediBook.Notification.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 5006
ENTRYPOINT ["dotnet", "MediBook.Notification.API.dll"]
