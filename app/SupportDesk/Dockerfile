FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY app/SupportDesk/SupportDesk.csproj app/SupportDesk/packages.lock.json app/SupportDesk/
RUN dotnet restore app/SupportDesk/SupportDesk.csproj --locked-mode
COPY app/SupportDesk/ app/SupportDesk/
RUN dotnet publish app/SupportDesk/SupportDesk.csproj --no-restore -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
HEALTHCHECK --interval=2s --timeout=3s --start-period=10s --retries=30 CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SupportDesk.dll"]
