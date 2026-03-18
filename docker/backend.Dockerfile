FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/Pitly.Core/Pitly.Core.csproj Pitly.Core/
COPY backend/src/Pitly.Broker.InteractiveBrokers/Pitly.Broker.InteractiveBrokers.csproj Pitly.Broker.InteractiveBrokers/
COPY backend/src/Pitly.Api/Pitly.Api.csproj Pitly.Api/
RUN dotnet restore Pitly.Api/Pitly.Api.csproj

COPY backend/src/ .
RUN dotnet publish Pitly.Api/Pitly.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -sf http://localhost:8080/api/health || exit 1

CMD ["dotnet", "Pitly.Api.dll"]
