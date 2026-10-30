FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/EngineeringPlayground.Cqrs.Api/EngineeringPlayground.Cqrs.Api.csproj src/EngineeringPlayground.Cqrs.Api/
COPY src/EngineeringPlayground.Cqrs.Domain/EngineeringPlayground.Cqrs.Domain.csproj src/EngineeringPlayground.Cqrs.Domain/
COPY src/EngineeringPlayground.Cqrs.Infrastructure/EngineeringPlayground.Cqrs.Infrastructure.csproj src/EngineeringPlayground.Cqrs.Infrastructure/
RUN dotnet restore src/EngineeringPlayground.Cqrs.Api/EngineeringPlayground.Cqrs.Api.csproj
COPY src/ src/
RUN dotnet publish src/EngineeringPlayground.Cqrs.Api/EngineeringPlayground.Cqrs.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EngineeringPlayground.Cqrs.Api.dll"]
