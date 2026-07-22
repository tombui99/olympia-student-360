FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OlympiaStudent360.sln", "./"]
COPY ["OlympiaStudent360.Api/OlympiaStudent360.Api.csproj", "OlympiaStudent360.Api/"]
RUN dotnet restore "OlympiaStudent360.Api/OlympiaStudent360.Api.csproj"

COPY . .
WORKDIR /src/OlympiaStudent360.Api
RUN dotnet publish "OlympiaStudent360.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "OlympiaStudent360.Api.dll"]
