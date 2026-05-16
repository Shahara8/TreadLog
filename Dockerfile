FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TreadLog.Shared/TreadLog.Shared.csproj TreadLog.Shared/
COPY TreadLog.Api/TreadLog.Api.csproj TreadLog.Api/
RUN dotnet restore TreadLog.Api/TreadLog.Api.csproj
COPY TreadLog.Shared/ TreadLog.Shared/
COPY TreadLog.Api/ TreadLog.Api/
RUN dotnet publish TreadLog.Api/TreadLog.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TreadLog.Api.dll"]
