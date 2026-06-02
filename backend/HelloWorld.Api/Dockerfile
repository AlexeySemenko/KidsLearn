FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/HelloWorld.Api/HelloWorld.Api.csproj backend/HelloWorld.Api/
RUN dotnet restore backend/HelloWorld.Api/HelloWorld.Api.csproj
COPY backend/HelloWorld.Api/ backend/HelloWorld.Api/
RUN dotnet publish backend/HelloWorld.Api/HelloWorld.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=frontend-build /frontend/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HelloWorld.Api.dll"]
