from diagrams import Cluster, Diagram, Edge
from diagrams.azure.compute import FunctionApps, ContainerApps
from diagrams.azure.storage import StorageAccounts
from diagrams.azure.database import CosmosDb
from diagrams.onprem.client import Users
from diagrams.azure.network import FrontDoors

with Diagram("Document vault app", show=True, direction="LR"):
    user = Users("User")

    with Cluster("documentvault-rg"):
        web = ContainerApps("Web App")
        func = FunctionApps("Function App")
        blobstorage = StorageAccounts("Blob storage")
        cosmosdb = CosmosDb("Cosmos DB")
        frontdoor = FrontDoors("Front Door & CDN")

        user >> Edge(label="accesses") >> web
        web >> Edge(label="generates download link") >> func
        web >> Edge(label="uploads to") >> blobstorage
        func >> Edge(label="manages links in") >> cosmosdb
        func >> Edge(label="generates SAS tokens for") >> blobstorage
        web >> Edge(label="reads metadata from") >> cosmosdb
        blobstorage >> Edge(label="served via") >> frontdoor
