FROM mcr.microsoft.com/dotnet/sdk:10.0 AS validation
WORKDIR /src
COPY . .
RUN dotnet test tools/BusinessDataSimulator.Tests/BusinessDataSimulator.Tests.csproj -c Release --nologo -v quiet -clp:ErrorsOnly
RUN dotnet publish tools/BusinessDataSimulator/BusinessDataSimulator.csproj -c Release --no-restore --nologo -v quiet -clp:ErrorsOnly -o /out
RUN sh deploy/seed-business-data.sh 10000 30 --preview

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV TZ=Asia/Shanghai
COPY --from=validation /out .
ENTRYPOINT ["dotnet", "BusinessDataSimulator.dll"]
