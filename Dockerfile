FROM mcr.microsoft.com/dotnet/sdk:10.0

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
    curl -fsSL https://pyenv.run | bash && \
    mkdir source && \
    cd source && \
    git clone https://github.com/digibanks99/lopen.git && \
    cd lopen && \
    dotnet build && \
    touch /root/.bashrc && \
    echo 'export PYENV_ROOT="$HOME/.pyenv"' >> /root/.bashrc && \
    echo '[[ -d $PYENV_ROOT/bin ]] && export PATH="$PYENV_ROOT/bin:$PATH"' >> /root/.bashrc && \
    echo 'eval "$(pyenv init - bash)"' >> /root/.bashrc

ENTRYPOINT [ "tail", "-f", "/dev/null" ]