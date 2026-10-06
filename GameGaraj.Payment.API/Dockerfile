FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore "GameGaraj.Payment.API/GameGaraj.Payment.API.csproj" -a $TARGETARCH
WORKDIR "/src/GameGaraj.Payment.API"
RUN dotnet build "GameGaraj.Payment.API.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "GameGaraj.Payment.API.csproj" -c Release -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameGaraj.Payment.API.dll"]
