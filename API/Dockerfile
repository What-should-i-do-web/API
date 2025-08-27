# Multi-stage Docker build for .NET 9 API
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
RUN addgroup -g 1001 -S appgroup && \
    adduser -S appuser -u 1001 -G appgroup

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies (for better Docker layer caching)
COPY ["src/WhatShouldIDo.API/WhatShouldIDo.API.csproj", "WhatShouldIDo.API/"]
COPY ["src/WhatShouldIDo.Application/WhatShouldIDo.Application.csproj", "WhatShouldIDo.Application/"]
COPY ["src/WhatShouldIDo.Infrastructure/WhatShouldIDo.Infrastructure.csproj", "WhatShouldIDo.Infrastructure/"]
COPY ["src/WhatShouldIDo.Domain/WhatShouldIDo.Domain.csproj", "WhatShouldIDo.Domain/"]

RUN dotnet restore "WhatShouldIDo.API/WhatShouldIDo.API.csproj"

# Copy all source code and build
COPY src/ .
WORKDIR "/src/WhatShouldIDo.API"
RUN dotnet build "WhatShouldIDo.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "WhatShouldIDo.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --self-contained false \
    --verbosity minimal

# Final runtime stage
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# Environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "WhatShouldIDo.API.dll"]