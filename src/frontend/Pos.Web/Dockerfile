# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PosSystem.slnx ./
COPY src/frontend/Pos.Web/Pos.Web.csproj src/frontend/Pos.Web/
RUN dotnet restore src/frontend/Pos.Web/Pos.Web.csproj
COPY src/frontend/ ./src/frontend/
RUN dotnet publish src/frontend/Pos.Web/Pos.Web.csproj -c Release -o /app

FROM nginx:alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/frontend/Pos.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
