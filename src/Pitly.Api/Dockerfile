FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Pitly.Core/Pitly.Core.csproj Pitly.Core/
COPY src/Pitly.Api/Pitly.Api.csproj Pitly.Api/
RUN dotnet restore Pitly.Api/Pitly.Api.csproj
COPY src/ .
RUN dotnet publish Pitly.Api/Pitly.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pitly.Api.dll"]
