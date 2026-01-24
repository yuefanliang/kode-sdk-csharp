FROM ubuntu:24.04

ARG DEBIAN_FRONTEND=noninteractive

# Profile:
# - base: minimal utilities for bash_run + common workflows
# - dev: includes extra build tools (python/node/build-essential)
ARG PROFILE=base

RUN apt-get update \
  && apt-get install -y --no-install-recommends \
    bash \
    ca-certificates \
    coreutils \
    findutils \
    grep \
    sed \
    gawk \
    util-linux \
    procps \
    git \
    curl \
    jq \
    ripgrep \
  && if [ "$PROFILE" = "dev" ]; then \
      apt-get install -y --no-install-recommends \
        python3 \
        python3-pip \
        nodejs \
        npm \
        make \
        gcc \
        g++ \
      ; \
    fi \
  && rm -rf /var/lib/apt/lists/*

# NOTE: We intentionally do not set USER here.
# DockerSandbox currently runs containers with default user (root) and mounts the workspace directory as rw.
# Using a non-root user is possible, but requires ensuring mount permissions are compatible.

