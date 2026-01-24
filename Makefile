# Repo Makefile (developer convenience)
#
# Docker sandbox image helpers:
# - Uses ./docker/sandbox/build.sh (auto-tags from Directory.Build.props by default)
# - Supports multi-arch builds via buildx
#
# Usage examples:
#   make sandbox-build
#   make sandbox-push IMAGE=your-org/kode-agent-sandbox
#   make sandbox-load-arm64
#

.PHONY: help \
        sandbox-build sandbox-build-base sandbox-build-dev \
        sandbox-load-amd64 sandbox-load-arm64 \
        sandbox-push sandbox-push-base sandbox-push-dev \
        sandbox-bake sandbox-bake-push

help:
	@echo "Targets:"
	@echo "  sandbox-build          Build sandbox image locally (single-arch)"
	@echo "  sandbox-load-amd64     Buildx build and --load for linux/amd64"
	@echo "  sandbox-load-arm64     Buildx build and --load for linux/arm64"
	@echo "  sandbox-push           Buildx multi-arch build and --push (amd64+arm64)"
	@echo ""
	@echo "Common overrides (env):"
	@echo "  IMAGE=... TAG=... PROFILE=base|dev PLATFORMS=... TAG_LATEST=true|false EXTRA_TAGS=... MODE=local|load|push"

# Defaults (can be overridden by env vars when invoking make)
IMAGE ?= kode-agent-sandbox
PROFILE ?= dev
PLATFORMS ?= linux/amd64,linux/arm64

# If TAG is not provided, build.sh will derive it from Directory.Build.props (<Version>).

sandbox-build:
	@MODE=local IMAGE="$(IMAGE)" PROFILE="$(PROFILE)" bash ./docker/sandbox/build.sh

sandbox-build-base:
	@MODE=local IMAGE="$(IMAGE)" PROFILE=base bash ./docker/sandbox/build.sh

sandbox-build-dev:
	@MODE=local IMAGE="$(IMAGE)" PROFILE=dev bash ./docker/sandbox/build.sh

sandbox-load-amd64:
	@MODE=load IMAGE="$(IMAGE)" PROFILE="$(PROFILE)" PLATFORMS=linux/amd64 bash ./docker/sandbox/build.sh

sandbox-load-arm64:
	@MODE=load IMAGE="$(IMAGE)" PROFILE="$(PROFILE)" PLATFORMS=linux/arm64 bash ./docker/sandbox/build.sh

sandbox-push:
	@MODE=push IMAGE="$(IMAGE)" PROFILE="$(PROFILE)" PLATFORMS="$(PLATFORMS)" bash ./docker/sandbox/build.sh

sandbox-push-base:
	@MODE=push IMAGE="$(IMAGE)" PROFILE=base PLATFORMS="$(PLATFORMS)" bash ./docker/sandbox/build.sh

sandbox-push-dev:
	@MODE=push IMAGE="$(IMAGE)" PROFILE=dev PLATFORMS="$(PLATFORMS)" bash ./docker/sandbox/build.sh

# Optional: buildx bake workflow (advanced)
# Example:
#   make sandbox-bake-push IMAGE=your-org/kode-agent-sandbox TAG=0.1.0
sandbox-bake:
	@docker buildx bake -f docker/sandbox/docker-bake.hcl

sandbox-bake-push:
	@docker buildx bake -f docker/sandbox/docker-bake.hcl \
		--set sandbox.tags="$(IMAGE):$(TAG)" \
		--push
