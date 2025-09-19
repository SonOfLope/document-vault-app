#!/usr/bin/env python3

from diagrams import Cluster, Diagram, Edge
from diagrams.azure.compute import FunctionApps, AppServices
from diagrams.azure.storage import StorageAccounts
from diagrams.azure.database import CosmosDb
from diagrams.onprem.client import Users
from diagrams.azure.network import FrontDoors

def create_diagram():
    with Diagram("app-service-with-env", show=False, direction="LR"):
        user = Users("User")

        with Cluster("documentvault-rg-as"):
            web = AppServices("Web App")
            webTest = AppServices("Web App Test")
            webStaging = AppServices("Web App Staging")
            func = FunctionApps("Function App")
            blobstorage = StorageAccounts("Blob storage")
            cosmosdb = CosmosDb("Cosmos DB")
            frontdoor = FrontDoors("Front Door & CDN")

            user >> Edge(label="accesses") >> [web, webTest, webStaging]
            web >> Edge(label="generates download link") >> func
            webTest >> Edge(label="generates download link") >> func
            webStaging >> Edge(label="generates download link") >> func
            web >> Edge(label="uploads to") >> blobstorage
            webTest >> Edge(label="uploads to") >> blobstorage
            webStaging >> Edge(label="uploads to") >> blobstorage
            func >> Edge(label="manages links in") >> cosmosdb
            func >> Edge(label="generates SAS tokens for") >> blobstorage
            web >> Edge(label="reads metadata from") >> cosmosdb
            blobstorage >> Edge(label="served via") >> frontdoor

if __name__ == "__main__":
    create_diagram()
    print("generated: app-service-with-env.png")