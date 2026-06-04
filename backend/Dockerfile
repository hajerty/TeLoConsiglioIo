# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TeLoConsiglio.sln ./
COPY TeLoConsiglio.Domain/TeLoConsiglio.Domain.csproj TeLoConsiglio.Domain/
COPY TeLoConsiglio.Infrastructure/TeLoConsiglio.Infrastructure.csproj TeLoConsiglio.Infrastructure/
COPY TeLoConsiglio.Api/TeLoConsiglio.Api.csproj TeLoConsiglio.Api/
RUN dotnet restore TeLoConsiglio.sln

COPY . .
RUN dotnet publish TeLoConsiglio.Api/TeLoConsiglio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/uploads
ENTRYPOINT ["dotnet", "TeLoConsiglio.Api.dll"]
