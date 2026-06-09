FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/HMC.Shared/HMC.Shared.csproj HMC.Shared/
COPY src/HMC.Server/HMC.Server.csproj HMC.Server/
RUN dotnet restore HMC.Server/HMC.Server.csproj

# Copy source
COPY src/HMC.Shared/ HMC.Shared/
COPY src/HMC.Server/ HMC.Server/

# Build frontend (requires Node.js)
# If you don't want the bundled frontend, remove the COPY for wwwroot below
WORKDIR /src/HMC.Server
RUN dotnet publish -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Install iperf3 for network speed tests
RUN apt-get update && apt-get install -y iperf3 && rm -rf /var/lib/apt/lists/*

# Create data directory for SQLite
RUN mkdir -p /app/data /app/wwwroot

EXPOSE 8080
EXPOSE 9556/udp

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "HMC.Server.dll"]
