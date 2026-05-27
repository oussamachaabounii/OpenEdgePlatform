FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY src/Provisioning/Provisioning.Worker/Provisioning.Worker.csproj src/Provisioning/Provisioning.Worker/
COPY src/Provisioning/Provisioning.Core/Provisioning.Core.csproj src/Provisioning/Provisioning.Core/
COPY src/ServiceBroker/ServiceBroker.Core/ServiceBroker.Core.csproj src/ServiceBroker/ServiceBroker.Core/
COPY src/ServiceBroker/ServiceBroker.Infrastructure/ServiceBroker.Infrastructure.csproj src/ServiceBroker/ServiceBroker.Infrastructure/
COPY src/ProxyConfig/ProxyConfig.Core/ProxyConfig.Core.csproj src/ProxyConfig/ProxyConfig.Core/
RUN dotnet restore src/Provisioning/Provisioning.Worker/Provisioning.Worker.csproj

COPY src/ProxyConfig src/ProxyConfig
COPY src/ServiceBroker src/ServiceBroker
COPY src/Provisioning src/Provisioning
RUN dotnet publish src/Provisioning/Provisioning.Worker/Provisioning.Worker.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "OpenEdgePlatform.Provisioning.Worker.dll"]
