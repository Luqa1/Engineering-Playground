FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/EngineeringPlayground.OptimisticConcurrency.Api/EngineeringPlayground.OptimisticConcurrency.Api.csproj", "src/EngineeringPlayground.OptimisticConcurrency.Api/"]
COPY ["src/EngineeringPlayground.OptimisticConcurrency.Domain/EngineeringPlayground.OptimisticConcurrency.Domain.csproj", "src/EngineeringPlayground.OptimisticConcurrency.Domain/"]
COPY ["src/EngineeringPlayground.OptimisticConcurrency.Infrastructure/EngineeringPlayground.OptimisticConcurrency.Infrastructure.csproj", "src/EngineeringPlayground.OptimisticConcurrency.Infrastructure/"]
RUN dotnet restore "src/EngineeringPlayground.OptimisticConcurrency.Api/EngineeringPlayground.OptimisticConcurrency.Api.csproj"

COPY . .
WORKDIR "/src/src/EngineeringPlayground.OptimisticConcurrency.Api"
RUN dotnet publish "EngineeringPlayground.OptimisticConcurrency.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EngineeringPlayground.OptimisticConcurrency.Api.dll"]
