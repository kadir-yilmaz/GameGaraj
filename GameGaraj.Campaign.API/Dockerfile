FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.Campaign.API/GameGaraj.Campaign.API.csproj" -a $TARGETARCH
WORKDIR "/src/GameGaraj.Campaign.API"
RUN dotnet build "GameGaraj.Campaign.API.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "GameGaraj.Campaign.API.csproj" -c Release -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.Campaign.API.dll"]
