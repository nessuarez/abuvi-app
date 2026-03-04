# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore dependencies first (cached layer)
COPY src/Abuvi.API/Abuvi.API.csproj ./Abuvi.API/
RUN dotnet restore Abuvi.API/Abuvi.API.csproj

# Copy source and publish
COPY src/Abuvi.API/ ./Abuvi.API/
WORKDIR /src/Abuvi.API
RUN dotnet publish -c Release -o /app/publish --no-restore /p:ConcurrencyLevel=1 /maxcpucount:1


# Runtime stage (smaller image, no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS final
WORKDIR /app

# Install Python 3.12 and curl (health checks) and necessary dependencies
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    python3.12 \
    python3.12-dev \
    python3-pip \
    python3.12-venv \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Config Csnakes to use Python 3.12
COPY src/Abuvi.Analysis ./PythonScripts

# Opcional: Instalar dependencias de Python si tienes un requirements.txt
# RUN pip3 install --no-cache-dir -r ./PythonScripts/requirements.txt --break-system-packages

# Set PYTHONPATH to include the PythonScripts directory
ENV PYTHONPATH="/app/PythonScripts"

# Sometimes is needed to specify where is libpython
ENV CSNAKES_PYTHON_LIBRARY="/usr/lib/x86_64-linux-gnu/libpython3.12.so"

# Copy the published output from the build stage
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Abuvi.API.dll"]