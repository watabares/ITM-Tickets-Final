FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Itm.Product.Api/Itm.Product.Api.csproj", "Itm.Product.Api/"]
RUN dotnet restore "Itm.Product.Api/Itm.Product.Api.csproj"
COPY . .
WORKDIR "/src/Itm.Product.Api"
RUN dotnet build "Itm.Product.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Itm.Product.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Itm.Product.Api.dll"]
