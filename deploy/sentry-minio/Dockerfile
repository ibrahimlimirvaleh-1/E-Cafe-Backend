FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS build
WORKDIR /src
COPY global.json ./

COPY ECafe.sln ./
COPY src/ECafe.Api/ECafe.Api.csproj src/ECafe.Api/
COPY src/ECafe.Application/ECafe.Application.csproj src/ECafe.Application/
COPY src/ECafe.Infrastructure/ECafe.Infrastructure.csproj src/ECafe.Infrastructure/
COPY src/ECafe.Domain/ECafe.Domain.csproj src/ECafe.Domain/
COPY src/ECafe.Shared/ECafe.Shared.csproj src/ECafe.Shared/
RUN dotnet restore src/ECafe.Api/ECafe.Api.csproj

COPY . .
RUN dotnet publish src/ECafe.Api/ECafe.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ECafe.Api.dll"]
