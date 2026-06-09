FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY src/HMC.Shared/HMC.Shared.csproj HMC.Shared/
COPY src/HMC.Server/HMC.Server.csproj HMC.Server/
RUN dotnet restore HMC.Server/HMC.Server.csproj

# Copy source
COPY src/HMC.Shared/ HMC.Shared/
COPY src/HMC.Server/ HMC.Server/

# Build
WORKDIR /src/HMC.Server
RUN dotnet publish -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Create data directory for SQLite
RUN mkdir -p /app/data

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "HMC.Server.dll"]
