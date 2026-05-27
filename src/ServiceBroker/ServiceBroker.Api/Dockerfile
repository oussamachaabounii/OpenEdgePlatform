FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY src/ServiceBroker/ServiceBroker.Api/ServiceBroker.Api.csproj src/ServiceBroker/ServiceBroker.Api/
COPY src/ServiceBroker/ServiceBroker.Core/ServiceBroker.Core.csproj src/ServiceBroker/ServiceBroker.Core/
COPY src/ServiceBroker/ServiceBroker.Infrastructure/ServiceBroker.Infrastructure.csproj src/ServiceBroker/ServiceBroker.Infrastructure/
COPY src/ProxyConfig/ProxyConfig.Core/ProxyConfig.Core.csproj src/ProxyConfig/ProxyConfig.Core/
RUN dotnet restore src/ServiceBroker/ServiceBroker.Api/ServiceBroker.Api.csproj

COPY src/ProxyConfig/ProxyConfig.Core src/ProxyConfig/ProxyConfig.Core
COPY src/ServiceBroker src/ServiceBroker
RUN dotnet publish src/ServiceBroker/ServiceBroker.Api/ServiceBroker.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "OpenEdgePlatform.ServiceBroker.Api.dll"]
