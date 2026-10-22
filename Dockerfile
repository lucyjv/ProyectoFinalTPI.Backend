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

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProyectoFinalTPI.Backend.dll"]
