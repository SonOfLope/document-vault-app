#!/bin/bash
set -e

echo "Setting up Document Vault local development environment..."

# Make sure Docker and Docker Compose are installed
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed. Please install Docker first."
    exit 1
fi

if ! docker compose version &> /dev/null; then
    echo "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Create the necessary Azure Storage containers
echo "Starting Azurite container temporarily to create initial blob container..."
docker compose up azurite -d

# Wait for Azurite to be ready
echo "Waiting for Azurite to be ready..."
for i in {1..30}; do
    if docker compose exec azurite nc -z 127.0.0.1 10000; then
        break
    fi
    echo "Waiting for Azurite to be ready... $i/30"
    sleep 2
    if [ $i -eq 30 ]; then
        echo "Azurite failed to start in time."
        docker compose down
        exit 1
    fi
done

echo "Creating 'documents' blob container..."
docker run --rm --network container:azurite mcr.microsoft.com/azure-cli az storage container create --name documents --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"

# Stop Azurite container
docker compose stop azurite


# If you get evaluation errors from cosmosdb emulator, run : 
# docker rmi -f mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
# docker volume rm -f arctiq_document-vault-app_cosmosdb-data

# Start all services
echo "Starting all services..."
docker compose up -d --build

echo "Waiting for Cosmos DB to be ready (this may take a few minutes)..."
for i in {1..120}; do
    if curl -s -k https://localhost:8081/_explorer/emulator.pem > /dev/null; then
        break
    fi
    echo "Waiting for Cosmos DB to be ready... $i/120"
    sleep 5
    if [ $i -eq 120 ]; then
        echo "CosmosDB Emulator failed to start in time."
        docker compose down
        exit 1
    fi
done

# Get the certificate from CosmosDB Emulator
echo "Downloading CosmosDB Emulator certificate..."
curl -k https://localhost:8081/_explorer/emulator.pem > cosmos-cert.pem

# Copy the certificate to the containers
echo "Copying certificate to containers..."
docker cp cosmos-cert.pem document-vault-function:/tmp/cosmos-cert.pem
docker cp cosmos-cert.pem document-vault-web:/tmp/cosmos-cert.pem

echo "All services are up and running!"
echo "Web app: http://localhost:8080"
echo "Function app: http://localhost:7071"
echo "Cosmos DB Emulator: https://localhost:8081/_explorer/index.html"
echo "Azurite Blob Storage: http://localhost:10000"
echo ""
echo "To download CosmosDB Emulator certificate for local development:"
echo "curl -k https://localhost:8081/_explorer/emulator.pem > cosmosdb.pem"
echo ""
echo "To stop all services: docker compose down"