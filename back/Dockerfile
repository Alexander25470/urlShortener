FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/UrlShortener.Api/UrlShortener.Api.csproj .
RUN dotnet restore

COPY src/UrlShortener.Api/ .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
USER app
COPY --from=build /app .

HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
