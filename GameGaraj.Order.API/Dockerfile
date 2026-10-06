FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.Order.API/GameGaraj.Order.API.csproj" -a $TARGETARCH
WORKDIR "/src/GameGaraj.Order.API"
RUN dotnet build "GameGaraj.Order.API.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "GameGaraj.Order.API.csproj" -c Release -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.Order.API.dll"]
