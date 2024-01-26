FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

USER root

RUN apt-get update -y \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER app

ARG HOMEASSISTANT__HOST
ARG HOMEASSISTANT__PORT
ARG HOMEASSISTANT__SSL
ARG HOMEASSISTANT__TOKEN

ENV PATH="/root/.dotnet/tools:${PATH}"

COPY . /app

WORKDIR /app

RUN apt-get update 
RUN apt-get --yes install curl

RUN dotnet restore ./NuttyTree.NetDaemon.sln && \
dotnet build --no-restore -c Release /p:SelfContained=false ./NuttyTree.NetDaemon.sln


FROM builder AS publisher

RUN dotnet publish --no-build --self-contained false -c Release -o out ./src/NuttyTree.NetDaemon/NuttyTree.NetDaemon.csproj


FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=publisher /app/out .
ENTRYPOINT ["dotnet", "NuttyTree.NetDaemon.dll"]
