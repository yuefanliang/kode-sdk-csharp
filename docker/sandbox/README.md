# Sandbox Docker Image

This repository provides a Docker image intended for `DockerSandbox` (`bash_run` execution inside a container).

The goal is a predictable, portable environment across:

- `linux/amd64`
- `linux/arm64`

## What's Included

The default image includes a minimal set of tools commonly required by agents:

- `bash` (required)
- `util-linux` (provides `setsid`, required by background job wrapper)
- `procps` (provides `ps`, useful for debugging)
- `git`, `curl`
- `jq`, `ripgrep`

Two build profiles are supported via `--build-arg PROFILE=...`:

- `PROFILE=base` (default): minimal utilities
- `PROFILE=dev`: adds `python3`, `pip`, `nodejs`, `npm`, `make`, `gcc/g++`

## Build (Single Platform, Local)

Build for your current platform only:

```bash
docker build -t kode-agent-sandbox:dev --build-arg PROFILE=dev -f docker/sandbox/Dockerfile .
```

Or use the helper script (auto-tags from repo `<Version>`):

```bash
MODE=local ./docker/sandbox/build.sh
```

Or use the repo root `Makefile`:

```bash
make sandbox-build
```

By default, the script also tags `:latest`. Disable it with:

```bash
TAG_LATEST=false MODE=local ./docker/sandbox/build.sh
```

## Build (Multi-Arch, Recommended)

Use Docker Buildx to build and push a multi-arch manifest:

```bash
# one-time setup (if you don't already have a buildx builder)
docker buildx create --name kode-builder --use
docker buildx inspect --bootstrap

# build + push multi-arch image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t your-org/kode-agent-sandbox:0.1.0 \
  --build-arg PROFILE=dev \
  -f docker/sandbox/Dockerfile \
  --push \
  .
```

Or use the helper script:

```bash
MODE=push IMAGE=your-org/kode-agent-sandbox ./docker/sandbox/build.sh
```

Or use the repo root `Makefile`:

```bash
make sandbox-push IMAGE=your-org/kode-agent-sandbox
```

By default, the script pushes both `:<Version>` and `:latest`. Disable `:latest` with:

```bash
TAG_LATEST=false MODE=push IMAGE=your-org/kode-agent-sandbox ./docker/sandbox/build.sh
```

Notes:

- `--push` is required for true multi-arch output (manifest list). `--load` only supports a single platform.
- If you only need a local multi-arch build on Apple Silicon, build for one platform at a time (e.g. `linux/arm64`).

## Configure WebApiAssistant

In `examples/Kode.Agent.WebApiAssistant/appsettings.json`:

```json
{
  "Kode": {
    "Sandbox": {
      "UseDocker": true,
      "DockerImage": "your-org/kode-agent-sandbox:0.1.0",
      "DockerNetworkMode": "none"
    }
  }
}
```

## Troubleshooting

- Image not found locally:
  - `DockerSandbox` does not pull images automatically. Ensure the image exists locally, or use a tag that is pullable.
- Missing tools:
  - Either switch to the `dev` profile, or create your own Dockerfile based on this one and install additional tools.
