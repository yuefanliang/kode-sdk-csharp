#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

VERSION_FILE="$ROOT_DIR/Directory.Build.props"
DEFAULT_VERSION="dev"

if [[ -f "$VERSION_FILE" ]]; then
  VERSION="$(grep -oE "<Version>[^<]+</Version>" "$VERSION_FILE" | head -n1 | sed -E "s#</?Version>##g")"
  VERSION="${VERSION:-$DEFAULT_VERSION}"
else
  VERSION="$DEFAULT_VERSION"
fi

IMAGE="${IMAGE:-kode-agent-sandbox}"
TAG="${TAG:-$VERSION}"
PROFILE="${PROFILE:-dev}"
PLATFORMS="${PLATFORMS:-linux/amd64,linux/arm64}"
TAG_LATEST="${TAG_LATEST:-true}"
LATEST_TAG="${LATEST_TAG:-latest}"
EXTRA_TAGS="${EXTRA_TAGS:-}"

# Build mode:
# - MODE=local: docker build (single-platform, local only)
# - MODE=load:  docker buildx build --load (single-platform, loads into local docker)
# - MODE=push:  docker buildx build --push (multi-arch recommended)
MODE="${MODE:-local}"

DOCKERFILE="${DOCKERFILE:-$ROOT_DIR/docker/sandbox/Dockerfile}"
CONTEXT="${CONTEXT:-$ROOT_DIR}"

FULL_TAG="${IMAGE}:${TAG}"
TAGS_ARGS=("-t" "$FULL_TAG")

# Optionally tag ":latest" as well (useful for deployments that track the latest published sandbox).
if [[ "$TAG_LATEST" == "true" && "$TAG" != "$LATEST_TAG" ]]; then
  TAGS_ARGS+=("-t" "${IMAGE}:${LATEST_TAG}")
fi

# Additional tags can be provided as a comma-separated list (e.g. EXTRA_TAGS=stable,0.1).
if [[ -n "$EXTRA_TAGS" ]]; then
  IFS=',' read -r -a EXTRA_TAGS_ARR <<< "$EXTRA_TAGS"
  for t in "${EXTRA_TAGS_ARR[@]}"; do
    t="$(echo "$t" | xargs)"
    [[ -z "$t" ]] && continue
    [[ "$t" == "$TAG" ]] && continue
    [[ "$t" == "$LATEST_TAG" && "$TAG_LATEST" == "true" ]] && continue
    TAGS_ARGS+=("-t" "${IMAGE}:${t}")
  done
fi

usage() {
  cat <<EOF
Build the Kode DockerSandbox image.

Env vars:
  IMAGE       Image repo/name (default: kode-agent-sandbox)
  TAG         Image tag (default: <Version> from Directory.Build.props, fallback: dev)
  TAG_LATEST  Also tag ":latest" (default: true)
  LATEST_TAG  Latest tag name (default: latest)
  EXTRA_TAGS  Comma-separated additional tags (default: empty)
  PROFILE     base|dev (default: dev)
  PLATFORMS   Buildx platforms (default: linux/amd64,linux/arm64)
  MODE        local|load|push (default: local)

Examples:
  # Local single-platform build
  MODE=local IMAGE=kode-agent-sandbox ./docker/sandbox/build.sh

  # Local buildx build (loads into local docker). Note: --load only supports one platform.
  MODE=load PLATFORMS=linux/arm64 ./docker/sandbox/build.sh

  # Multi-arch build + push (recommended for publishing)
  MODE=push IMAGE=your-org/kode-agent-sandbox ./docker/sandbox/build.sh
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

echo "Building sandbox image:"
echo "  ROOT     : $ROOT_DIR"
echo "  DOCKERFILE: $DOCKERFILE"
echo "  IMAGE    : $FULL_TAG"
echo "  PROFILE  : $PROFILE"
echo "  PLATFORMS: $PLATFORMS"
echo "  MODE     : $MODE"
echo "  TAG_LATEST: $TAG_LATEST (LATEST_TAG=$LATEST_TAG)"
echo "  EXTRA_TAGS: ${EXTRA_TAGS:-<none>}"

case "$MODE" in
  local)
    command docker build \
      "${TAGS_ARGS[@]}" \
      --build-arg "PROFILE=$PROFILE" \
      -f "$DOCKERFILE" \
      "$CONTEXT"
    ;;
  load)
    command docker buildx build \
      --platform "$PLATFORMS" \
      --load \
      "${TAGS_ARGS[@]}" \
      --build-arg "PROFILE=$PROFILE" \
      -f "$DOCKERFILE" \
      "$CONTEXT"
    ;;
  push)
    command docker buildx build \
      --platform "$PLATFORMS" \
      --push \
      "${TAGS_ARGS[@]}" \
      --build-arg "PROFILE=$PROFILE" \
      -f "$DOCKERFILE" \
      "$CONTEXT"
    ;;
  *)
    echo "Unknown MODE: $MODE"
    usage
    exit 1
    ;;
esac

echo "Done:"
printf '  %s\n' "${TAGS_ARGS[@]}" | sed -n 's/^-t /  /p'
