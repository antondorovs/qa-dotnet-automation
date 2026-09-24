FROM node:22-bookworm-slim AS report-tools
WORKDIR /tools
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS sdk

FROM mcr.microsoft.com/playwright/dotnet:v1.62.0-noble
COPY --from=sdk /usr/share/dotnet/ /usr/share/dotnet/
COPY --from=report-tools /usr/local/bin/node /usr/local/bin/node
WORKDIR /work
COPY --from=report-tools /tools/node_modules/ node_modules/
COPY . .
RUN dotnet restore --locked-mode && dotnet build --no-restore -c Release
ENV DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
ENTRYPOINT ["bash", "scripts/container-check.sh"]
