# ADR-002: Asynchronous provisioning over a message bus

- Status: accepted
- Date: 2026-05-26

## Context

A provision request might take milliseconds (DNS already cached, regions selected, control
plane warm) or seconds (DNS cold, control plane catching up, downstream cluster sluggish). We
need a contract that lets the broker:

- Return predictably fast to the caller — within ms, regardless of downstream latency.
- Recover gracefully when the worker is overloaded, partitioned, or restarting.
- Scale workers horizontally based on queue depth without coupling to broker availability.

## Decision

Adopt the **OSB async pattern**: broker returns `202 Accepted` with an `operation` token; the
client polls `/v2/service_instances/{id}/last_operation` until terminal. Internally, all
provisioning logic runs in **a separate worker process** consuming events from a message bus.

## Rationale

1. **SLA isolation.** The broker is a 99.9% availability HTTP service. The worker can crash,
   redeploy, or block on DNS without affecting the broker's response time.
2. **Backpressure.** Queue depth is observable, alertable, and naturally throttles new
   provisions when the worker pool is saturated.
3. **Retries.** Failed events are redelivered by the bus (with exponential backoff and
   dead-letter on repeated failure) — much better than HTTP timeouts and broken state.
4. **Multi-step coordination.** The provisioning chain involves the broker → worker →
   control plane → Envoy. An async bus is the natural carrier for those events; the alternative
   (HTTP chains with circuit breakers) couples failure modes across services.

## Tradeoffs

- **Client complexity.** Clients must poll. Mitigated by following the OSB spec, which most
  cloud-native tooling already understands.
- **Eventual consistency.** A provision returns before traffic actually flows. The
  `dashboard_url` and `last_operation` endpoint give the client a clean view of progress.

## Consequences

- All public mutations are async. The sync path (`accepts_incomplete=false`) is rejected with
  `422 Unprocessable Entity`.
- The worker has its own scaling story (consumer count, prefetch, retry policy) decoupled from
  broker scaling.
- Observability spans the chain via OpenTelemetry — without it, debugging a stuck provision
  would be hard.
