#!/bin/bash




IMAGE_NAME="slt.api"
CONTAINER_NAME="slt.paytomoon.com"


echo "Building and publishing the project..."
dotnet build SLT.Api/SLT.Api.csproj -c Release
dotnet publish SLT.Api/SLT.Api.csproj -c Release -o publish

echo " Building Docker image..."
docker build -t $IMAGE_NAME .

echo "Stopping old container if exists..."
docker-compose down


echo "Starting container..."
docker-compose up -d


echo "Container status:"
docker-compose ps

echo "List of all containers:"
docker ps -a

echo "Showing logs (press Ctrl+C to exit)..."
docker-compose logs -f