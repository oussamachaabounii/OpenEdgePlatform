# ADR-001: Envoy + xDS for the data plane

- Status: accepted
- Date: 2026-05-26

## Context

The platform needs a programmable L7 proxy that can be reconfigured at runtime by a control
plane without restarts or config-file drops. The major candidates considered:

| Option   | Programmable runtime config? | Operational maturity at scale | License |
| -------- | ---------------------------- | ----------------------------- | ------- |
| Envoy    | Yes — xDS (gRPC, REST)       | Used by Lyft, Stripe, Pinterest, every major service mesh | Apache 2.0 |
| NGINX    | Partial — NGINX Plus only; OSS reloads config file | Mature, but ops-heavy at fleet scale | BSD-2 (Plus is commercial) |
| HAProxy  | Runtime API exists but limited (no full route reconfig) | Mature but coarse-grained | GPL |
| Traefik  | Yes — dynamic providers       | Smaller deployments           | MIT     |

## Decision

Use **Envoy** as the data plane, driven by **xDS v3 over ADS** (Aggregated Discovery Service)
from a custom control plane.

## Rationale

1. **First-class runtime config**: xDS is *the* standard runtime config protocol. Istio,
   Consul Connect, AWS App Mesh, and Gloo all standardise on it. Skills and tooling transfer.
2. **Atomic apply**: ADS over a single gRPC stream gives us atomic, ordered application of
   listener + cluster + route + endpoint deltas. No torn states.
3. **Operational maturity**: stats, tracing, access logging, admin endpoint, hot-restart
   semantics — all production-grade out of the box.
4. **Per-service scale**: Lyft and Stripe have shown Envoy scales to tens of thousands of
   clusters per proxy.

## Tradeoffs

- **Envoy protos are huge.** Building a real-world control plane requires vendoring the
  `envoy.config.*` protobuf bundle. This learning repo deliberately ships a *minimal* `.proto`
  to keep build dependencies small; resources are sent as JSON-inside-`Any`. Real interop with
  Envoy requires either (a) vendoring the upstream protos or (b) using a managed library like
  `go-control-plane`'s C# equivalent.
- **Memory cost.** Envoy is heavier than NGINX (~150MB RSS baseline vs ~20MB). Acceptable at
  the edge where we run a handful per node, not per workload.

## Consequences

- The control plane speaks gRPC, not HTTP, to proxies. Internal networking must allow HTTP/2.
- Operators learn Envoy admin endpoint (`/config_dump`, `/listeners`, `/clusters`) — see the
  getting-started guide.
- A future production-grade fork would replace `AdsGrpcService.Wrap<T>` with proper protobuf
  encoding against the upstream Envoy types.
