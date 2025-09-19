#!/usr/bin/env python3
"""
SDLC Diagram: Container Apps Deployment (main branch)
Generates a visual diagram showing the software development lifecycle for Container Apps deployment.
"""

from diagrams import Diagram, Cluster, Edge
from diagrams.onprem.vcs import Github
from diagrams.onprem.ci import GithubActions
from diagrams.azure.compute import ContainerApps, FunctionApps, ContainerRegistries
from diagrams.azure.database import CosmosDb
from diagrams.azure.storage import StorageAccounts
from diagrams.azure.network import CDNProfiles
from diagrams.azure.identity import ManagedIdentities
from diagrams.onprem.container import Docker
from diagrams.programming.language import Csharp

def create_diagram():
    with Diagram("SDLC: Container Apps Deployment", show=False, filename="sdlc-container-apps"):

        with Cluster("Development"):
            developer = Csharp("Developer")
            local_docker = Docker("Local Docker")

        with Cluster("Source Control"):
            github_repo = Github("GitHub Repository")
            main_branch = Github("main branch")

        with Cluster("Pipelines"):
            web_pipeline = GithubActions("web-app-cd.yml")
            function_pipeline = GithubActions("function-app-cd.yml")

        with Cluster("Azure tenant"):
            with Cluster("Federated Identities"):
                web_identity = ManagedIdentities("web-identity")
                function_identity = ManagedIdentities("function-identity")

            acr = ContainerRegistries("Azure Container Registry")

            with Cluster("Container Apps Environment"):
                web_app = ContainerApps("Web App")
                function_app = FunctionApps("Function App")

            cosmos = CosmosDb("Cosmos DB")
            storage = StorageAccounts("Storage Account")
            cdn = CDNProfiles("CDN")

        developer >> Edge(label="code & commit") >> github_repo
        github_repo >> Edge(label="push to main") >> main_branch

        main_branch >> Edge(label="trigger") >> web_pipeline
        main_branch >> Edge(label="trigger") >> function_pipeline

        web_pipeline >> Edge(label="authenticate") >> web_identity
        function_pipeline >> Edge(label="authenticate") >> function_identity

        web_identity >> Edge(label="build & push image") >> acr
        web_identity >> Edge(label="container app update") >> web_app
        web_app >> Edge(label="pull new image") >> acr

        function_identity >> Edge(label="deploy function") >> function_app

        web_app >> cosmos
        function_app >> cosmos
        web_app >> storage
        function_app >> storage
        storage >> cdn

        developer >> Edge(label="local dev") >> local_docker

if __name__ == "__main__":
    create_diagram()
    print("generated: sdlc-container-apps.png")
