FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj
RUN dotnet publish ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "ProyectoFinalTPI.Backend.dll"]
