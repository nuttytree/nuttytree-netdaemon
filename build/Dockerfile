FROM mcr.microsoft.com/dotnet/sdk:6.0 AS builder

COPY . /app

WORKDIR /app

RUN dotnet restore ./NuttyTree.NetDaemon.sln && \
dotnet build --no-restore -c Release /p:SelfContained=false ./NuttyTree.NetDaemon.sln


FROM builder AS publisher

RUN dotnet publish --no-build --self-contained false -c Release -o out ./src/NuttyTree.NetDaemon/NuttyTree.NetDaemon.csproj


FROM mcr.microsoft.com/dotnet/aspnet:6.0

WORKDIR /app

COPY --from=publisher /app/out .
ENTRYPOINT ["dotnet", "NuttyTree.NetDaemon.dll"]
