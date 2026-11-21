# syntax=docker/dockerfile:1

# ==============================================================================
# Stage 1: Restore and publish .NET 10 ASP.NET Core MVC application
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first to maximize Docker layer caching for NuGet restore
COPY TechStore.csproj ./
RUN dotnet restore TechStore.csproj

# Copy remaining source files and publish framework-dependent Release build
COPY . ./
RUN dotnet publish TechStore.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ==============================================================================
# Stage 2: Minimal production runtime image (non-root user)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

# Ensure runtime-writable directories exist and are owned by the non-root app user
RUN mkdir -p /app/wwwroot/uploads/products /app/Data /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID:$APP_UID" /app/wwwroot/uploads /app/Data /home/app/.aspnet

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TechStore.dll"]
