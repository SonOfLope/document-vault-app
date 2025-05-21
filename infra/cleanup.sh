#!/bin/bash
set -e

RESOURCE_GROUP_NAME="documentvault-rg"

read -p "Are you sure you want to delete all resources in $RESOURCE_GROUP_NAME? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
  echo "Deleting resource group $RESOURCE_GROUP_NAME..."
  az group delete --name $RESOURCE_GROUP_NAME --yes --no-wait
  echo "Deletion initiated. This may take several minutes to complete."
else
  echo "Deletion cancelled."
fi