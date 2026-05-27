# Self-service guide for developers

This guide is for application teams who want to **expose their service through the edge** by
calling the broker API. No tickets, no ops involvement.

## Prerequisites

- Your service is reachable by DNS (typically a Kubernetes service name).
- You have a hostname you want to terminate (e.g. `api.example.com`).
- The broker URL — usually `https://edge-broker.internal`.

## 1. Provision

Send a `PUT` with a stable instance id you choose (UUID is fine):

```bash
curl -X PUT \
  "https://edge-broker.internal/v2/service_instances/my-service-prod?accepts_incomplete=true" \
  -H "X-Broker-API-Version: 2.17" \
  -H "Content-Type: application/json" \
  -d '{
    "service_id": "svc-edge-lb",
    "plan_id": "standard",
    "parameters": {
      "upstream_service": "my-service.my-namespace.svc.cluster.local",
      "upstream_port": 8080,
      "hostname": "api.example.com",
      "listener_port": 443
    }
  }'
```

You'll get `202 Accepted` with an `operation` token and a `dashboard_url`.

### Idempotency

Re-sending the same `PUT` returns `200 OK` (instance already exists with same shape). Sending
a different payload to the same instance id returns `409 Conflict` — use a new instance id
instead.

## 2. Poll until ready

```bash
curl -s \
  "https://edge-broker.internal/v2/service_instances/my-service-prod/last_operation" \
  -H "X-Broker-API-Version: 2.17"
```

The response is `{ "state": "...", "description": "..." }`. Possible states:

| state          | meaning                                         |
| -------------- | ----------------------------------------------- |
| `in progress`  | The worker has not yet completed.               |
| `succeeded`    | Live across the fleet. Traffic will flow.       |
| `failed`       | See `description` for the reason.               |

`410 Gone` after deprovision is a successful terminal state.

## 3. Verify

```bash
curl -s \
  "https://edge-broker.internal/v2/service_instances/my-service-prod" \
  -H "X-Broker-API-Version: 2.17"
```

You'll see the parameters you posted plus a `metadata` block with the current state.

Operators can also inspect the live xDS view per instance:

```bash
curl -s "https://control-plane.internal/api/snapshots/my-service-prod" | jq
```

## 4. Deprovision

```bash
curl -X DELETE \
  "https://edge-broker.internal/v2/service_instances/my-service-prod?service_id=svc-edge-lb&plan_id=standard&accepts_incomplete=true" \
  -H "X-Broker-API-Version: 2.17"
```

Poll `/last_operation` until you get `410 Gone`.

## Parameter reference

| field              | type      | required | description                                              |
| ------------------ | --------- | -------- | -------------------------------------------------------- |
| `upstream_service` | string    | yes      | DNS name to forward traffic to                           |
| `upstream_port`    | int       | yes      | Port to use on the upstream                              |
| `hostname`         | string    | yes      | Host header to match (the public hostname)               |
| `listener_port`    | int       | yes      | Port the edge proxies should listen on (typically 443)   |
| `regions`          | string[]  | no       | Restrict to specific regions (defaults to platform pick) |

## Common pitfalls

- **Missing `accepts_incomplete=true`** → `422 Unprocessable Entity`. This service is async.
- **Missing `X-Broker-API-Version` header** → `412 Precondition Failed`.
- **`upstream_service` not resolvable from the worker network** → state goes to `failed`
  with description "DNS resolution failed for ...".
- **Different parameters with the same instance id** → `409 Conflict`. Pick a new id.
