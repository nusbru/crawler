FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CrawlerCli/CrawlerCli.csproj src/CrawlerCli/
RUN dotnet restore src/CrawlerCli/CrawlerCli.csproj --nologo

COPY . .
RUN dotnet publish src/CrawlerCli/CrawlerCli.csproj --configuration Release --nologo --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CrawlerCli.dll"]
