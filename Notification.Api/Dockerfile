FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Notification.Api/Itm.Notification.Api.csproj", "Notification.Api/"]
COPY ["Itm.Shared.Events/Itm.Shared.Events.csproj", "Itm.Shared.Events/"]
RUN dotnet restore "Notification.Api/Itm.Notification.Api.csproj"
COPY . .
WORKDIR "/src/Notification.Api"
RUN dotnet build "Itm.Notification.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Itm.Notification.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Itm.Notification.Api.dll"]
