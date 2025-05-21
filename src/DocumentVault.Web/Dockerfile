FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["DocumentVault.Web.csproj", "."]
RUN dotnet restore "./DocumentVault.Web.csproj"
COPY . .
RUN dotnet build "DocumentVault.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DocumentVault.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DocumentVault.Web.dll"]