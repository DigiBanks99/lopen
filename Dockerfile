FROM mcr.microsoft.com/dotnet/sdk:10.0 as base

COPY out/.copilot /root/.copilot

RUN (type -p wget >/dev/null || (apt update && apt install wget -y)) && \
    mkdir -p -m 755 /etc/apt/keyrings && \
    out=$(mktemp) && wget -nv -O$out https://cli.github.com/packages/githubcli-archive-keyring.gpg && \
    cat $out | tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null && \
    chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg && \
    mkdir -p -m 755 /etc/apt/sources.list.d && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        git \
        wget \
        unzip \
        vim \
        gh && \
    rm -rf /var/lib/apt/lists/* && \
    curl -fsSL https://gh.io/copilot-install | bash && \
    mkdir source && \
    cd source && \
    git clone https://github.com/DigiBanks99/lopen.git && \
    cd lopen && \
    git checkout feat/next-job && \
    dotnet build && \
    touch /root/.bashrc

COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

RUN uv python install && \
    echo "PATH=\"/root/.local/bin:\$PATH\"" >> /root/.bashrc

ENTRYPOINT [ "tail", "-f", "/dev/null" ]
