# Shared SDK stage: the migration service uses the local dotnet-ef tool from this stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/OnlineLearningPlatform.Domain/OnlineLearningPlatform.Domain.csproj src/OnlineLearningPlatform.Domain/
COPY src/OnlineLearningPlatform.Application/OnlineLearningPlatform.Application.csproj src/OnlineLearningPlatform.Application/
COPY src/OnlineLearningPlatform.Infrastructure/OnlineLearningPlatform.Infrastructure.csproj src/OnlineLearningPlatform.Infrastructure/
COPY src/OnlineLearningPlatform.Api/OnlineLearningPlatform.Api.csproj src/OnlineLearningPlatform.Api/

RUN dotnet restore src/OnlineLearningPlatform.Api/OnlineLearningPlatform.Api.csproj

COPY src/ src/
RUN dotnet tool restore
RUN dotnet publish src/OnlineLearningPlatform.Api/OnlineLearningPlatform.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Runtime image stays small. curl is used only by the container health check.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "OnlineLearningPlatform.Api.dll"]
