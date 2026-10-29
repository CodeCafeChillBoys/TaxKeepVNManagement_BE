# Multi-stage build for .NET 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching
COPY TaxKeepVNManagement.sln ./
COPY TaxKeepDomain/TaxKeepVN.Domain.csproj TaxKeepDomain/
COPY TaxKeepVN.Application/TaxKeepVN.Application.csproj TaxKeepVN.Application/
COPY TaxKeepVN.Infrastructure/TaxKeepVN.Infrastructure.csproj TaxKeepVN.Infrastructure/
COPY TaxKeepVNManagementSystem/TaxKeepVNManagementSystem.csproj TaxKeepVNManagementSystem/
COPY TaxKeepVN.ApiGateway/TaxKeepVN.ApiGateway.csproj TaxKeepVN.ApiGateway/

# Restore dependencies
RUN dotnet restore TaxKeepVNManagement.sln

# Copy the remaining source code
COPY . .

# Build and publish the main API service
WORKDIR /src/TaxKeepVNManagementSystem
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Expose port (HTTP 8080 default in .NET 8)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Staging

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaxKeepVNManagementSystem.dll"]
