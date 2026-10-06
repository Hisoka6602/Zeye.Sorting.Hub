FROM node:24-alpine AS web-build
WORKDIR /web
COPY Zeye.Sorting.Hub.Web/package*.json ./
RUN npm ci --include=optional --no-audit --no-fund
COPY Zeye.Sorting.Hub.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj
RUN dotnet publish Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false /p:BundleWebUi=false
COPY --from=web-build /web/dist /out/wwwroot

# Optional Linux regression stage; the normal runtime image skips this stage.
FROM build AS test
RUN dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj \
    --configuration Release --logger "console;verbosity=minimal"

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV TZ=Asia/Shanghai
# curl 及其依赖位于 main，减少构建时不需要的额外软件源索引下载。
RUN if [ -f /etc/apt/sources.list.d/ubuntu.sources ]; then sed -i 's/^Components:.*/Components: main/' /etc/apt/sources.list.d/ubuntu.sources; fi \
    && apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
# 预先建立配置与历史目录，使首次挂载的数据卷继承 app 用户写入权限。
RUN mkdir -p logs migration-scripts backup-artifacts backup-runbooks drill-records monthly-reports governance-artifacts fusion-images data/configuration data/business-history \
    && chown -R app:app /app
USER app
EXPOSE 5078
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 3 http://127.0.0.1:5078/health/live || exit 1
ENTRYPOINT ["dotnet", "Zeye.Sorting.Hub.Host.dll"]
