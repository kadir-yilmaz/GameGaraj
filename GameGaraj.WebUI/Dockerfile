FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.WebUI/GameGaraj.WebUI.csproj"
WORKDIR "/src/GameGaraj.WebUI"
RUN dotnet build "GameGaraj.WebUI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameGaraj.WebUI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.WebUI.dll"]
