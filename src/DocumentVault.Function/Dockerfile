FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj \
        --configuration Release \
        --output /home/site/wwwroot \
        --runtime linux-x64 \
        --self-contained true \
        /p:PublishTrimmed=false

FROM mcr.microsoft.com/azure-functions/base:4

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=build /home/site/wwwroot /home/site/wwwroot
