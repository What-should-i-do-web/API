# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/WhatShouldIDo.API/WhatShouldIDo.API.csproj", "src/WhatShouldIDo.API/"]
COPY ["src/WhatShouldIDo.Domain/WhatShouldIDo.Domain.csproj", "src/WhatShouldIDo.Domain/"]
COPY ["src/WhatShouldIDo.Infrastructure/WhatShouldIDo.Infrastructure.csproj", "src/WhatShouldIDo.Infrastructure/"]
COPY ["src/WhatShouldIDo.Application/WhatShouldIDo.Application.csproj", "src/WhatShouldIDo.Application/"]
RUN dotnet restore "./src/WhatShouldIDo.API/WhatShouldIDo.API.csproj"
COPY . .
WORKDIR "/src/src/WhatShouldIDo.API"
RUN dotnet build "./WhatShouldIDo.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WhatShouldIDo.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WhatShouldIDo.API.dll"]