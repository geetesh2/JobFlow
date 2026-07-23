FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Development

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/JobFlow.Api/JobFlow.Api.csproj", "src/JobFlow.Api/"]
COPY ["src/JobFlow.Application/JobFlow.Application.csproj", "src/JobFlow.Application/"]
COPY ["src/JobFlow.Domain/JobFlow.Domain.csproj", "src/JobFlow.Domain/"]
COPY ["src/JobFlow.Infrastructure/JobFlow.Infrastructure.csproj", "src/JobFlow.Infrastructure/"]
COPY ["src/JobFlow.Contracts/JobFlow.Contracts.csproj", "src/JobFlow.Contracts/"]
COPY ["src/JobFlow.Shared/JobFlow.Shared.csproj", "src/JobFlow.Shared/"]
RUN dotnet restore "src/JobFlow.Api/JobFlow.Api.csproj"
COPY . .
RUN dotnet publish "src/JobFlow.Api/JobFlow.Api.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobFlow.Api.dll"]
