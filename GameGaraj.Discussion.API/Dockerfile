FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.Discussion.API/GameGaraj.Discussion.API.csproj"
WORKDIR "/src/GameGaraj.Discussion.API"
RUN dotnet build "GameGaraj.Discussion.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameGaraj.Discussion.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.Discussion.API.dll"]
