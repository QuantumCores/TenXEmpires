# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage (no Node.js needed)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy and restore server project
COPY ["TenXEmpires.Server/TenXEmpires.Server.csproj", "TenXEmpires.Server/"]
RUN dotnet restore "./TenXEmpires.Server/TenXEmpires.Server.csproj"

# Copy domain and infrastructure projects
COPY ["TenXEmpires.Server.Domain/TenXEmpires.Server.Domain.csproj", "TenXEmpires.Server.Domain/"]
COPY ["TenXEmpires.Server.Infrastructure/TenXEmpires.Server.Infrastructure.csproj", "TenXEmpires.Server.Infrastructure/"]
RUN dotnet restore "./TenXEmpires.Server/TenXEmpires.Server.csproj"

# Copy all source and build
COPY . .
WORKDIR "/src/TenXEmpires.Server"
RUN dotnet build "./TenXEmpires.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TenXEmpires.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TenXEmpires.Server.dll"]
