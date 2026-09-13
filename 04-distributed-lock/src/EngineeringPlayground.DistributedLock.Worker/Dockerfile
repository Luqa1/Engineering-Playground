FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["src/EngineeringPlayground.DistributedLock.Domain/EngineeringPlayground.DistributedLock.Domain.csproj", "src/EngineeringPlayground.DistributedLock.Domain/"]
COPY ["src/EngineeringPlayground.DistributedLock.Infrastructure/EngineeringPlayground.DistributedLock.Infrastructure.csproj", "src/EngineeringPlayground.DistributedLock.Infrastructure/"]
COPY ["src/EngineeringPlayground.DistributedLock.Worker/EngineeringPlayground.DistributedLock.Worker.csproj", "src/EngineeringPlayground.DistributedLock.Worker/"]
RUN dotnet restore "src/EngineeringPlayground.DistributedLock.Worker/EngineeringPlayground.DistributedLock.Worker.csproj"

COPY . .
RUN dotnet publish "src/EngineeringPlayground.DistributedLock.Worker/EngineeringPlayground.DistributedLock.Worker.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache krb5-libs
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EngineeringPlayground.DistributedLock.Worker.dll"]
