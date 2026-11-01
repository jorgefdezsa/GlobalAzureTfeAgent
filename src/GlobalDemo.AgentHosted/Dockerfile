# Imagen del agente hospedado en Foundry. La plataforma exige linux/amd64 y sirve en el puerto 8088.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GlobalDemo.AgentHosted.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish GlobalDemo.AgentHosted.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8088
ENTRYPOINT ["dotnet", "GlobalDemo.AgentHosted.dll"]
