# Getting started

This page walks you from a clean machine to a running stack and a successful provision.

## Prerequisites

- macOS, Linux, or WSL2
- Docker 20+ and Docker Compose
- `.NET 8 SDK` (the repo also builds cleanly against .NET 9 — `global.json` is set to roll forward)
- `curl`, `jq`, `python3` (for the demo scripts)

If you only need to run the stack and don't intend to develop, you can skip the .NET SDK.

## Clone and bring the stack up

```bash
git clone https://github.com/your-org/open-edge-platform.git
cd open-edge-platform
docker-compose up --build
```

The first build is slow (it pulls .NET SDK + Envoy + Postgres). Subsequent runs are cached.

Watch the logs. You should see, in order:

1. `postgres` reports `database system is ready to accept connections`
2. `rabbitmq` reports `Server startup complete`
3. `redis` reports `Ready to accept connections`
4. `service-broker` reports `Now listening on: http://0.0.0.0:8080`
5. `control-plane` reports `Now listening on: http://0.0.0.0:8081`
6. `provisioning-worker` reports `MassTransit bus started`
7. `envoy` logs `lds: add/update listener` once the control plane pushes config

## Verify health

```bash
curl -s http://localhost:8080/health | jq
curl -s http://localhost:8081/health | jq
curl -s http://localhost:8081/api/proxies | jq
```

The third call should show the connected Envoy node (only after the stack is fully up).

## Provision your first instance

```bash
./scripts/provision-example.sh demo-1 api.example.com
```

Behind the scenes:

1. The script PUTs the OSB provision payload.
2. The broker persists the instance with state `Pending` and publishes
   `ProvisioningRequested`.
3. The worker consumes the event, picks regions, resolves DNS, and publishes
   `ProvisioningCompleted`.
4. The control plane consumes the completed event, builds the xDS snapshot, persists it, and
   pushes it to connected Envoy proxies.
5. The script polls `/last_operation` until it reads `succeeded`.

## Inspect the running config in Envoy

```bash
curl -s http://localhost:9901/config_dump | jq '.configs[] | select(.["@type"] | contains("Dump")) | .name? // .static_listeners? // ._?'
curl -s http://localhost:9901/listeners
curl -s http://localhost:9901/clusters
```

You should see `listener_demo-1` in the LDS dump and `cluster_my-service_demo-1` in the CDS dump.

## Inspect the control plane state

```bash
curl -s http://localhost:8081/api/snapshots | jq
curl -s http://localhost:8081/api/snapshots/demo-1 | jq
```

## Tail logs for a specific component

```bash
docker-compose logs -f service-broker
docker-compose logs -f provisioning-worker
docker-compose logs -f control-plane
```

## Run unit tests locally

```bash
dotnet test OpenEdgePlatform.sln
```

The integration tests use an in-memory EF provider and the MassTransit test harness, so they
don't need Docker.

## Tear down

```bash
docker-compose down -v
```

The `-v` removes the Postgres volume, giving you a clean slate.
