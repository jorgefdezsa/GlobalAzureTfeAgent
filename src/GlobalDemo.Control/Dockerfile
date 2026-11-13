# Imagen del backend del panel (GlobalDemo.Control) para Azure Container Apps. Solo la API: no incluye el frontend React
# (wwwroot), porque el panel publicado lo consume la Canvas App a través de un conector personalizado.
#   az acr build -r <registro> -t control:v1 -f GlobalDemo.Control/Dockerfile .   (ejecutado desde src)
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY GlobalDemo.Control/GlobalDemo.Control.csproj GlobalDemo.Control/
RUN dotnet restore GlobalDemo.Control/GlobalDemo.Control.csproj
COPY GlobalDemo.Control/*.cs GlobalDemo.Control/
RUN dotnet publish GlobalDemo.Control/GlobalDemo.Control.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
# .NET 10 escucha en el 8080 dentro del contenedor (el despliegue fija ASPNETCORE_URLS).
EXPOSE 8080
ENTRYPOINT ["dotnet", "GlobalDemo.Control.dll"]
