FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for Docker layer caching
COPY src/SmartPool.Domain/SmartPool.Domain.csproj src/SmartPool.Domain/
COPY src/SmartPool.Application/SmartPool.Application.csproj src/SmartPool.Application/
COPY src/SmartPool.Infrastructure/SmartPool.Infrastructure.csproj src/SmartPool.Infrastructure/
COPY src/SmartPool.API/SmartPool.API.csproj src/SmartPool.API/
RUN dotnet restore src/SmartPool.API/SmartPool.API.csproj

# Copy source and publish Release build
COPY src/ src/
RUN dotnet publish src/SmartPool.API/SmartPool.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet SmartPool.API.dll"]
