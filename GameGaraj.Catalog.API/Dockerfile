FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.Catalog.API/GameGaraj.Catalog.API.csproj"
WORKDIR "/src/GameGaraj.Catalog.API"
RUN dotnet build "GameGaraj.Catalog.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameGaraj.Catalog.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.Catalog.API.dll"]
