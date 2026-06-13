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
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pos.Api.dll"]
