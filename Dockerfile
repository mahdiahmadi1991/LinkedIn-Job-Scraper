FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY LinkedIn.JobScraper.sln ./
COPY src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj src/LinkedIn.JobScraper.Web/

RUN dotnet restore src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj

COPY . .

RUN dotnet publish src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://127.0.0.1:5180

EXPOSE 5180

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LinkedIn.JobScraper.Web.dll"]
