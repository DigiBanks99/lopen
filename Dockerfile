FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        git \
        wget \
        unzip \
        vim && \
    rm -rf /var/lib/apt/lists/* && \
    curl -fsSL https://gh.io/copilot-install | bash && \
    mkdir source && \
    cd source && \
    git clone https://github.com/digibanks99/lopen.git && \
    cd lopen && \
    dotnet build

ENTRYPOINT [ "tail", "-f", "/dev/null" ]