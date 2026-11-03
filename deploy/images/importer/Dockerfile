# syntax=docker/dockerfile:1
#
# The importer image (ADR 0011): varve-demo, published as Native AOT for
# linux-x64, on the chiseled runtime-deps base, which has no shell and no
# package manager. The base is the one pull from MCR, here and in no other
# file (Varve's ADR 0111 exception; the gate holds it to this directory).
#
#   dotnet publish src/VarveDemo.Tool -c Release -r linux-x64 -p:PublishAot=true -o artifacts/importer
#   mkdir -p artifacts/importer/cache
#   docker build -f deploy/images/importer/Dockerfile artifacts/importer
#
# The build context is the publish output: the binary and an empty cache
# directory, copied so a volume mounted there belongs to the non-root user.

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296

COPY --chmod=755 varve-demo /app/varve-demo
COPY --chown=1654:1654 cache/ /var/lib/varve-demo/

ENV VARVE_DEMO_CACHE=/var/lib/varve-demo

VOLUME /var/lib/varve-demo
USER 1654
WORKDIR /app

ENTRYPOINT ["/app/varve-demo"]
CMD ["import"]
