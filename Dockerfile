FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ECafe.sln ./
COPY src/ECafe.Api/ECafe.Api.csproj src/ECafe.Api/
COPY src/ECafe.Application/ECafe.Application.csproj src/ECafe.Application/
COPY src/ECafe.Domain/ECafe.Domain.csproj src/ECafe.Domain/
COPY src/ECafe.Infrastructure/ECafe.Infrastructure.csproj src/ECafe.Infrastructure/
COPY src/ECafe.Migrator/ECafe.Migrator.csproj src/ECafe.Migrator/
COPY src/ECafe.Shared/ECafe.Shared.csproj src/ECafe.Shared/

RUN dotnet restore src/ECafe.Api/ECafe.Api.csproj
RUN dotnet restore src/ECafe.Migrator/ECafe.Migrator.csproj

COPY . .
RUN dotnet publish src/ECafe.Api/ECafe.Api.csproj -c Release -o /app/api /p:UseAppHost=false
RUN dotnet publish src/ECafe.Migrator/ECafe.Migrator.csproj -c Release -o /app/migrator /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "ECafe.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrator
WORKDIR /app
COPY --from=build /app/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "ECafe.Migrator.dll"]
