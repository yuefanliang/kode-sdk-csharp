group "default" {
  targets = ["sandbox"]
}

target "sandbox" {
  dockerfile = "docker/sandbox/Dockerfile"
  context    = "."
  platforms  = ["linux/amd64", "linux/arm64"]
  args = {
    PROFILE = "dev"
  }
  # Set tags via: docker buildx bake --set sandbox.tags=your-org/kode-agent-sandbox:0.1.0
}

