# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PosSystem.slnx ./
COPY src/backend/Pos.Domain/Pos.Domain.csproj src/backend/Pos.Domain/
COPY src/backend/Pos.Application/Pos.Application.csproj src/backend/Pos.Application/
COPY src/backend/Pos.Infrastructure/Pos.Infrastructure.csproj src/backend/Pos.Infrastructure/
COPY src/backend/Pos.Api/Pos.Api.csproj src/backend/Pos.Api/
RUN dotnet restore src/backend/Pos.Api/Pos.Api.csproj
COPY src/ ./src/
RUN dotnet publish src/backend/Pos.Api/Pos.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Npgsql loads libgssapi_krb5.so.2 (Kerberos/GSSAPI) when opening a Postgres connection; the slim
# aspnet image no longer ships it, so install it or the API crashes on its first DB call at startup.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pos.Api.dll"]
