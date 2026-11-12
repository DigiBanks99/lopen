# Runtime research

## Podman runtime feasibility (issue #50)

Question: is the runtime decided in #28 (one container per agent session over stdio, `--internal` network plus CONNECT proxy, rootless nested engine sidecar exposed as `DOCKER_HOST`, devcontainer build-only, `--userns=keep-id`) feasible on Podman machine (WSL2) on Windows as the primary engine, with Docker Desktop through the same Docker Engine API driver?

Researched 2026-11-12 against Podman v6.2.0 (latest release, 2026-11-10), Docker.DotNet.Enhanced v4.3.3, and the current docs listed in each section. Podman's GitHub org moved from `containers/*` to `podman-container-tools/*`; old links redirect.

### Summary

| # | Topic | Verdict |
| - | ----- | ------- |
| 1 | Docker.DotNet against Podman's compat API | Feasible with caveats |
| 2 | Proxy on an `--internal` and a normal network | Feasible |
| 3 | Rootless nested engine sidecar, Testcontainers, `aspire run` | Feasible with caveats; sidecar networking must change |
| 4 | `devcontainer build --docker-path podman` with features | Feasible with caveats |
| 5 | `--userns=keep-id` for a non-root `remoteUser` on a named volume at `/work` | Feasible on Podman; no Docker equivalent |

Findings that change the decided design:

- **The agent and its engine sidecar need to share a network namespace.** Aspire publishes container ports to `127.0.0.1` on the engine host by default ([microsoft/aspire#10754](https://github.com/microsoft/aspire/issues/10754), maintainer comment). With the sidecar in its own network namespace, that loopback is the sidecar's, and the agent container can't reach it. The fix is to run the agent with `NetworkMode: container:<sidecar>`, or to join the sidecar to the agent's namespace. Then `DOCKER_HOST=tcp://127.0.0.1:<port>`, or a unix socket on a shared volume, works, and `localhost` is the same for Aspire, Testcontainers and the agent. As a bonus, the engine API is never exposed on a network.
- **On Docker Desktop, the nested engine needs `--privileged` on the outer container.** `docker:dind-rootless` runs as UID 1000, but `--privileged` is still required ([Docker rootless tips](https://docs.docker.com/engine/security/rootless/tips/)). On Podman, rootless-in-rootless needs no `--privileged`. The two engines therefore have different security postures.
- **`--userns=keep-id` is Podman-only.** Docker's `UsernsMode` has no `keep-id`. The driver needs per-engine create options and can't send one identical request to both engines.

### 1. Docker.DotNet against Podman's Docker-compatible API

Verdict: **feasible with caveats.**

- **API level.** Podman 6.x reports compat API `1.44` current and `1.24` minimum ([version/version.go](https://github.com/podman-container-tools/podman/blob/main/version/version.go)). Podman 6.1 notes more 1.44 work and "preparations" for 1.45 ([RELEASE_NOTES](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)). The `podman-system-service` man page still says v1.40, which is out of date. It also says the server "does not reject requests with an unsupported version set" ([podman-system-service](https://docs.podman.io/en/latest/markdown/podman-system-service.1.html)). Testcontainers defaults to `DOCKER_API_VERSION=1.44` ([custom configuration](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/docs/custom_configuration/index.md)), which matches.
- **Client library.** Use `Docker.DotNet.Enhanced` (testcontainers/Docker.DotNet, v4.3.3 at 2026-06), not `Docker.DotNet`. The upstream dotnet/Docker.DotNet repo has had no pushes since 2025-08. The fork fixed "cannot hijack chunked or content length stream with podman" in 3.128.3 ([testcontainers/Docker.DotNet#25](https://github.com/testcontainers/Docker.DotNet/issues/25), [PR #26](https://github.com/testcontainers/Docker.DotNet/pull/26)). The fix sends the correct `Connection: Upgrade` headers for hijacked attach and exec streams. Lopen's stdio channel depends on this.
- **Endpoint on Windows.** `podman machine start` forwards the API to `\\.\pipe\docker_engine` if that pipe is free. The machine-specific pipe `npipe:////./pipe/podman-machine-default` is always available ([Podman for Windows tutorial](https://github.com/podman-container-tools/podman/blob/main/docs/tutorials/podman-for-windows.md), [Podman Desktop: DOCKER_HOST](https://podman-desktop.io/docs/migrating-from-docker/using-the-docker_host-environment-variable)). If Docker Desktop and Podman both run, whichever starts first owns `docker_engine`. Lopen should select the endpoint explicitly per engine and not rely on `docker_engine`.
- **Rootless by default.** `podman machine init` defaults to rootless, and "API forwarding ... follows this setting" ([podman-machine-init](https://docs.podman.io/en/latest/markdown/podman-machine-init.1.html)). Keep the default, because `keep-id` (Q5) requires rootless.
- **Attach (stdio, `run -i` equivalent).** The compat attach handler supports `stdin`, `stdout`, `stderr`, `stream` and `logs`, and upgrades the connection with 101 ([containers_attach.go](https://github.com/podman-container-tools/podman/blob/main/pkg/api/handlers/compat/containers_attach.go)). Note that stdin defaults to `true` unless the `stdin` query parameter is sent. Attach before start, with `OpenStdin=true`, `StdinOnce=false`, `Tty=false`, so output is the 8-byte-framed multiplexed stream.
- **Networks.** Compat network create maps `Internal` straight through ([networks.go](https://github.com/podman-container-tools/podman/blob/main/pkg/api/handlers/compat/networks.go)). Container create accepts several `EndpointsConfig` entries (bridge mode only) with aliases and static IPs ([containers_create.go](https://github.com/podman-container-tools/podman/blob/main/pkg/api/handlers/compat/containers_create.go)).
- **Create options.** `Devices`, `CapAdd`, `Privileged`, `SecurityOpt`, `Init` and `UsernsMode` are passed into the same option parser the CLI uses ([containers_create.go](https://github.com/podman-container-tools/podman/blob/main/pkg/api/handlers/compat/containers_create.go)).
- **Volumes, labels, exec, wait, kill.** Supported compat endpoints. Podman 6.0 changed `volume prune` to anonymous-only and `label!=` filters to logical AND. These changes are CLI-facing, but cleanup code should filter by positive labels ([RELEASE_NOTES 6.0.0](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)).
- **Build.** Compat `/build` is backed by Buildah. Docker.DotNet's classic tar-context build works. BuildKit session features (`version=2`, session secrets) are unavailable ([devcontainers/cli#900](https://github.com/devcontainers/cli/pull/900) cites Podman defaulting to `DOCKER_BUILDKIT=0`). Not an issue, because building goes through the devcontainer CLI (Q4).
- **Caveat: follow streams.** Compat `/events` and `/containers/{id}/logs?follow=true` may send no response headers until the first data arrives. Clients with a header timeout then fail ([Red Hat RUN-2327](https://redhat.atlassian.net/browse/RUN-2327), reported on Podman 5.2.1). Don't apply a response-header timeout to follow or attach calls.
- **Caveat: named-pipe concurrency.** Many parallel API calls over the named pipe to Podman on Windows have timed out. Docker.DotNet recreates pipe connections instead of reusing them ([testcontainers-dotnet#1438](https://github.com/testcontainers/testcontainers-dotnet/issues/1438), open [testcontainers/Docker.DotNet#30](https://github.com/testcontainers/Docker.DotNet/issues/30)). Bound the control plane's API concurrency.
- **Podman 6.0 network isolation.** Network isolation is now on by default, for Docker compatibility ([RELEASE_NOTES 6.0.0](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)). This fits the design.
- **Podman 6.0 platform support.** Podman 6.0 drops Windows 10 support. Podman machine requires Windows 11.

### 2. Proxy container on an `--internal` network and a normal network

Verdict: **feasible.**

- Podman: `--internal` restricts external access on bridge networks. "In all cases, aardvark-dns will only resolve container names with this option enabled", and other queries get `NXDOMAIN` ([podman-network-create](https://docs.podman.io/en/latest/markdown/podman-network-create.1.html)). Session containers therefore resolve the proxy by name and can't resolve external names. Connections must go through the CONNECT proxy. Don't combine this with `--disable-dns`.
- Since Podman 4.0 with Netavark, a container resolves names on every network it joins, not only the first ([Red Hat: Podman's new network stack](https://www.redhat.com/en/blog/podman-new-network-stack)). Podman 6.0 removed CNI, so Netavark is the only backend ([RELEASE_NOTES 6.0.0](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)). The old CNI behaviour, where internal networks had no DNS (Podman 3.0 notes), no longer applies.
- Known edge case: internal networks with a non-default `dns_port` don't get the DNAT rule, so DNS fails ([podman mailing list](https://lists.podman.io/archives/list/podman@lists.podman.io/thread/NKVFO4JQO5JLYKWXHHODC2WHQRG7A2KO/)). Keep the default `dns_port`.
- Docker: internal-network containers "may communicate between each other, but not with any other network". However, "communication with the gateway IP address (and thus appropriately configured host services) is possible" ([docker network create](https://docs.docker.com/reference/cli/docker/network/create/)). On Docker Desktop, anything listening on the bridge gateway inside the VM can be reached without going through the proxy. Bind nothing sensitive there. I didn't find whether Podman/Netavark internal networks also leave the gateway reachable. Check this in the spike.
- Required config: create the session network with `Internal=true`. Attach the proxy to that network and to a normal bridge, with an alias such as `proxy`. Session containers join only the internal network and get `HTTPS_PROXY`/`HTTP_PROXY=http://proxy:<port>`.

### 3. Rootless nested engine sidecar per session

Verdict: **feasible with caveats.** Sidecar networking must change (see the design findings above).

Podman-in-Podman (rootless inside the rootless Podman machine):

- Minimum documented flags are `--user podman --security-opt label=disable --device /dev/fuse` on `quay.io/podman/stable`. The article says "we don't have to add the dangerous security capabilities" `SYS_ADMIN` and `MKNOD`, so no `--privileged` is needed ([Red Hat: How to use Podman inside of a container](https://www.redhat.com/en/blog/podman-inside-container)).
- The image ([Containerfile](https://github.com/podman-container-tools/image_build/blob/main/podman/Containerfile)) has:
  - user `podman` with UID 1000
  - `/etc/subuid` entries `podman:1:999` and `podman:1001:64535`, which fit inside the 65536 IDs a rootless outer container gets
  - fuse-overlayfs as the storage `mount_program`
  - `VOLUME /home/podman/.local/share/containers`. Mount a per-session named volume there so cleanup is deterministic.
- The image's `/etc/containers/containers.conf` sets `netns="host"`, `userns="host"` and `cgroups="disabled"` ([containers.conf](https://github.com/podman-container-tools/image_build/blob/main/podman/containers.conf)). With `netns=host`, inner containers share the sidecar's namespace and Podman discards port mappings. That breaks Testcontainers and Aspire port publishing. Mount an override that sets `netns` to a private or bridge network. Podman 6.0 removed slirp4netns in favour of pasta ([RELEASE_NOTES 6.0.0](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)). The inner rootless networking is therefore expected to need `--device /dev/net/tun` on the sidecar as well. This is inferred and needs a spike.
- Serve the API with `podman system service --time=0 <uri>`. The default rootless socket is `unix:///run/user/1000/podman/podman.sock`. Upstream "strongly" recommends against exposing it on a network ([podman-system-service](https://docs.podman.io/en/latest/markdown/podman-system-service.1.html)). A shared network namespace (bind `tcp://127.0.0.1:2375`) or a socket on a shared volume avoids that.
- Inner image pulls leave through the sidecar's network, which is the `--internal` session network. Give the sidecar `HTTP(S)_PROXY` pointing at the CONNECT proxy.

Docker Desktop:

- `docker:dind-rootless` runs as UID 1000. "`--privileged` is required for disabling seccomp, AppArmor, and mount masks" ([Docker rootless tips](https://docs.docker.com/engine/security/rootless/tips/), [Docker Hub: docker](https://hub.docker.com/_/docker)). The API listens on `/run/user/1000/docker.sock`. With TLS on, it listens on 2376 using certificates in `DOCKER_TLS_CERTDIR`. The image is marked experimental.

Testcontainers for .NET against the sidecar:

- Set `DOCKER_HOST` to the sidecar endpoint ([custom configuration](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/docs/custom_configuration/index.md)).
- Ryuk mounts the engine socket from `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE`, which defaults to `/var/run/docker.sock`, and runs privileged by default (`TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED=true`). Either point the override at the inner socket path or set `TESTCONTAINERS_RYUK_DISABLED=true`. Disabling is safe because the whole sidecar is torn down per session.
- With a shared network namespace, mapped ports are on `localhost` and `TESTCONTAINERS_HOST_OVERRIDE` isn't needed. Without one, it must name the sidecar host ([Running inside a container](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/docs/dind/index.md)).
- The Testcontainers for .NET docs don't mention Podman explicitly. Podman support is tracked in [testcontainers-dotnet#876](https://github.com/testcontainers/testcontainers-dotnet/issues/876).

`aspire run` against the sidecar:

- Aspire finds the runtime by looking for the `docker` or `podman` executable on `PATH`. It doesn't use the API directly, and aliases don't count ([Aspire: podman not found](https://github.com/dotnet/docs-aspire/blob/main/docs/troubleshooting/podman-wsl-not-found.md)). Select it with `ASPIRE_CONTAINER_RUNTIME=docker|podman` ([Aspire prerequisites](https://aspire.dev/get-started/prerequisites/), [AppHost configuration](https://aspire.dev/app-host/configuration/)). Aspire requires Podman 5.0 or later.
- The session image therefore needs a container CLI that targets the sidecar: the `docker` CLI with `DOCKER_HOST`, or `podman --remote` with `CONTAINER_HOST`.
- Aspire binds published ports to `127.0.0.1` on the engine host ([microsoft/aspire#10754](https://github.com/microsoft/aspire/issues/10754)). That's why the agent must share the sidecar's network namespace. Changing it per endpoint (`TargetHost = "0.0.0.0"`) would mean editing every user's AppHost.

### 4. `devcontainer build --docker-path podman` with features

Verdict: **feasible with caveats.**

- The devcontainers CLI has a Podman CI job, last fixed on 2026-09-29 ([devcontainers/cli#1312](https://github.com/devcontainers/cli/pull/1312)), and Podman options were added in [#985](https://github.com/devcontainers/cli/pull/985).
- Features are built with `podman buildx build --load --build-context dev_containers_feature_content_source=<dir> ...` and `RUN --mount=type=bind,from=...` (log in [devcontainers/cli#548](https://github.com/devcontainers/cli/issues/548)). From Windows this uses the remote client:
  - `--build-context` has been supported by the remote client since Podman 5.6.0.
  - A Windows-specific `--build-context` bug that broke Dev Containers was fixed in 5.3.0 ([RELEASE_NOTES](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)).
- Feature layers are rebuilt on every build when the bundled Buildah is older than 1.41.1 ([devcontainers/cli#1262](https://github.com/devcontainers/cli/issues/1262) open, fix PR [#1350](https://github.com/devcontainers/cli/pull/1350) open). Podman 6.1 bundles Buildah 1.45.0, so it isn't affected. Check with `podman info --format '{{.Host.BuildahVersion}}'`.
- A `Permission denied` on the feature bind mount was reported again in 2025-11 on macOS rootless and rootful Podman ([#548](https://github.com/devcontainers/cli/issues/548)). Earlier variants were caused by SELinux or xattr on the build context ([#776](https://github.com/devcontainers/cli/issues/776)). This is unverified on the WSL machine; include it in the spike.
- The open Compose and BuildKit issues ([#863](https://github.com/devcontainers/cli/issues/863), [#900](https://github.com/devcontainers/cli/pull/900)) don't apply, because Lopen uses build only and no Compose.

### 5. `--userns=keep-id` for a non-root `remoteUser` writing a named volume at `/work`

Verdict: **feasible on Podman (rootless). Docker needs a different mechanism.**

- `keep-id` maps the current user's UID:GID to the same values in the container. `uid=` and `gid=` override the target IDs, and `size=` overrides the namespace size ([podman-run](https://docs.podman.io/en/latest/markdown/podman-run.1.html)). Use `UsernsMode: "keep-id:uid=<remoteUser uid>,gid=<remoteUser gid>"`.
- The release history of these options ([RELEASE_NOTES](https://github.com/podman-container-tools/podman/blob/main/RELEASE_NOTES.md)):
  - `uid` and `gid` were added in Podman 4.3.
  - `size=` was added in 5.4.
  - A `keep-id:uid=0` mapping bug was fixed in 5.x.
  - Rootless containers using `keep-id` can have ports forwarded to them.
- The compat API passes `HostConfig.UsernsMode` verbatim to the CLI parser ([containers_create.go](https://github.com/podman-container-tools/podman/blob/main/pkg/api/handlers/compat/containers_create.go)). That parser accepts `keep-id` and `keep-id:<opts>` ([namespaces.go](https://github.com/podman-container-tools/podman/blob/main/pkg/specgen/namespaces.go)), so Docker.DotNet can send it.
- Named volumes: "When a named volume is first mounted to a container, Podman automatically adjusts the ownership" of the mount point if the volume is empty and not yet copied up ([podman-run](https://docs.podman.io/en/latest/markdown/podman-run.1.html)). `:U` or `chown=true` recursively chowns but changes the host filesystem. Avoid it except for a pre-populated volume.
- Rootful Podman rejects `keep-id`, so the machine must stay rootless (the default). Each `keep-id` container uses all the user's subordinate IDs (v4.4 docs), so `size=` may be needed when nesting.
- Docker: `UsernsMode` only supports host mode or the daemon's `--userns-remap`. There's no `keep-id`. On Docker Desktop, the daemon is rootful in the VM, and an empty named volume is populated from the image's content at the mount path ([Docker volumes](https://docs.docker.com/engine/storage/volumes/)). The devcontainer image should create `/work` owned by `remoteUser`. The Docker docs don't say that ownership is copied, so check it in the spike or use a root init step that chowns `/work`.
- The devcontainers CLI's own keep-id handling for `devcontainer up` is still being fixed ([#1285](https://github.com/devcontainers/cli/pull/1285)). That doesn't matter, because Lopen creates the container itself.

### Spike checklist

Things the docs don't settle:

1. Hijacked attach over `npipe:////./pipe/podman-machine-default` with Docker.DotNet.Enhanced 4.3.x: stdin close and EOF semantics, and long-lived streams.
2. Whether a Netavark `--internal` network lets containers reach the gateway IP.
3. Podman-in-Podman on the WSL machine with `--device /dev/fuse --device /dev/net/tun --user podman --security-opt label=disable`, with `netns` overridden, publishing a port to `127.0.0.1` that the agent reaches through a shared network namespace.
4. `NetworkMode: container:<sidecar>` through the compat API, and Testcontainers plus `aspire run` from the agent container.
5. `devcontainer build --docker-path podman` with a feature on the WSL machine (bind-mount permission issue).
6. `/work` ownership on first mount under keep-id (Podman) and image copy-up (Docker Desktop).
