#!/bin/bash

$(dirname "$0")/CopyCopilotConfig.cs

cd $(dirname "$0")/..

podman-remote-static-linux_amd64 build -t lopen-agent-env .

podman-remote-static-linux_amd64 run -d localhost/lopen-agent-env:latest
