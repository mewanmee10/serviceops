# Build context: backend/ServiceOps.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ServiceOps.Api.csproj ./
RUN dotnet restore ServiceOps.Api.csproj
COPY . ./
RUN dotnet publish ServiceOps.Api.csproj --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /var/serviceops/uploads && chown -R ${APP_UID}:${APP_UID} /var/serviceops
COPY --from=build /out ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Attachments__Root=/var/serviceops/uploads \
    SeedAdmin__Enabled=false \
    SeedDemo__Enabled=false
USER ${APP_UID}
EXPOSE 8080
ENTRYPOINT ["dotnet", "ServiceOps.Api.dll"]
