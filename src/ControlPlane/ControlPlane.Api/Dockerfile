FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY src/ControlPlane/ControlPlane.Api/ControlPlane.Api.csproj src/ControlPlane/ControlPlane.Api/
COPY src/ControlPlane/ControlPlane.Core/ControlPlane.Core.csproj src/ControlPlane/ControlPlane.Core/
COPY src/ControlPlane/ControlPlane.Infrastructure/ControlPlane.Infrastructure.csproj src/ControlPlane/ControlPlane.Infrastructure/
COPY src/ProxyConfig/ProxyConfig.Core/ProxyConfig.Core.csproj src/ProxyConfig/ProxyConfig.Core/
COPY src/ProxyConfig/ProxyConfig.Infrastructure/ProxyConfig.Infrastructure.csproj src/ProxyConfig/ProxyConfig.Infrastructure/
COPY src/ServiceBroker/ServiceBroker.Core/ServiceBroker.Core.csproj src/ServiceBroker/ServiceBroker.Core/
COPY src/Provisioning/Provisioning.Core/Provisioning.Core.csproj src/Provisioning/Provisioning.Core/
RUN dotnet restore src/ControlPlane/ControlPlane.Api/ControlPlane.Api.csproj

COPY src/ProxyConfig src/ProxyConfig
COPY src/ServiceBroker src/ServiceBroker
COPY src/Provisioning src/Provisioning
COPY src/ControlPlane src/ControlPlane
RUN dotnet publish src/ControlPlane/ControlPlane.Api/ControlPlane.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8081 18000
ENV ASPNETCORE_URLS=http://0.0.0.0:8081
ENTRYPOINT ["dotnet", "OpenEdgePlatform.ControlPlane.Api.dll"]
