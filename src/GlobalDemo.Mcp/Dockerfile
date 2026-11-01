# Imagen del servidor MCP. El contexto de build es la carpeta src (depende de Aviation y ServiceDefaults):
#   az acr build -r <registro> -t mcp:v1 -f GlobalDemo.Mcp/Dockerfile .   (ejecutado desde src)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GlobalDemo.Aviation/GlobalDemo.Aviation.csproj GlobalDemo.Aviation/
COPY GlobalDemo.ServiceDefaults/GlobalDemo.ServiceDefaults.csproj GlobalDemo.ServiceDefaults/
COPY GlobalDemo.Mcp/GlobalDemo.Mcp.csproj GlobalDemo.Mcp/
RUN dotnet restore GlobalDemo.Mcp/GlobalDemo.Mcp.csproj
COPY GlobalDemo.Aviation/ GlobalDemo.Aviation/
COPY GlobalDemo.ServiceDefaults/ GlobalDemo.ServiceDefaults/
COPY GlobalDemo.Mcp/ GlobalDemo.Mcp/
RUN dotnet publish GlobalDemo.Mcp/GlobalDemo.Mcp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# .NET 10 escucha en el 8080 dentro del contenedor.
EXPOSE 8080
ENTRYPOINT ["dotnet", "GlobalDemo.Mcp.dll"]
