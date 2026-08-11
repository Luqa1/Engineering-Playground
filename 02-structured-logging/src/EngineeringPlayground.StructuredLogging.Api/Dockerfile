FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/EngineeringPlayground.StructuredLogging.Api/EngineeringPlayground.StructuredLogging.Api.csproj", "src/EngineeringPlayground.StructuredLogging.Api/"]
COPY ["src/EngineeringPlayground.StructuredLogging.Domain/EngineeringPlayground.StructuredLogging.Domain.csproj", "src/EngineeringPlayground.StructuredLogging.Domain/"]
COPY ["src/EngineeringPlayground.StructuredLogging.Infrastructure/EngineeringPlayground.StructuredLogging.Infrastructure.csproj", "src/EngineeringPlayground.StructuredLogging.Infrastructure/"]
RUN dotnet restore "src/EngineeringPlayground.StructuredLogging.Api/EngineeringPlayground.StructuredLogging.Api.csproj"

COPY . .
WORKDIR "/src/src/EngineeringPlayground.StructuredLogging.Api"
RUN dotnet publish "EngineeringPlayground.StructuredLogging.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EngineeringPlayground.StructuredLogging.Api.dll"]
