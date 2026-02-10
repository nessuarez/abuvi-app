# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore dependencies first (cached layer)
COPY src/Abuvi.API/Abuvi.API.csproj ./Abuvi.API/
RUN dotnet restore Abuvi.API/Abuvi.API.csproj

# Copy source and publish
COPY src/Abuvi.API/ ./Abuvi.API/
WORKDIR /src/Abuvi.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage (smaller image, no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Abuvi.API.dll"]