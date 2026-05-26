FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Itm.Gateway.Api/Itm.Gateway.Api.csproj", "Itm.Gateway.Api/"]
RUN dotnet restore "Itm.Gateway.Api/Itm.Gateway.Api.csproj"
COPY . .
WORKDIR "/src/Itm.Gateway.Api"
RUN dotnet build "Itm.Gateway.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Itm.Gateway.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Itm.Gateway.Api.dll"]