FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Order.Api/Order.Api.csproj", "Order.Api/"]
COPY ["Itm.Shared.Events/Itm.Shared.Events.csproj", "Itm.Shared.Events/"]
RUN dotnet restore "Order.Api/Order.Api.csproj"
COPY . .
WORKDIR "/src/Order.Api"
RUN dotnet build "Order.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Order.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Order.Api.dll"]