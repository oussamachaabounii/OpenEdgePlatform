# ADR-003: Open Service Broker API as the developer contract

- Status: accepted
- Date: 2026-05-26

## Context

The platform needs a contract for application teams to provision routing. Two broad choices:

1. **Custom REST API** designed by us for our exact use case.
2. **Open Service Broker (OSB) v2**, an open spec already supported by Kubernetes, Cloud
   Foundry, and most platform-engineering tooling.

## Decision

Implement the **OSB v2.17 contract** for the broker. Provision and deprovision flow through
`PUT/DELETE /v2/service_instances/{id}` with `/last_operation` polling.

## Rationale

1. **Existing ecosystem.** Tools like Crossplane, Cloud Foundry, and the Kubernetes Service
   Catalog speak OSB natively. We get integrations for free.
2. **Strong semantic primitives.** Idempotent provisioning, conflict on parameter drift,
   accepted-async polling — all hard problems with documented, battle-tested answers in the
   OSB spec.
3. **Documentation and reference clients.** Developers can use any OSB client (Bosh, Cloud
   Foundry CLI, `osb-cmd`) without us writing one.

## Tradeoffs

- The OSB spec has features we don't need (bindings, plans, dashboard SSO). They sit unused,
  but adding them later is straightforward.
- The `accepts_incomplete=true` flag is verbose and confusing for first-time users. We mitigate
  with example scripts and a hard `422` if it's omitted.

## Consequences

- `X-Broker-API-Version: 2.17` is required on every `/v2` call (enforced by middleware).
- A non-trivial portion of the broker code is dedicated to spec compliance (versioning, status
  codes 200 vs 202 vs 410 vs 422). See `ServiceInstancesController` and `OsbVersionMiddleware`.
- Custom fields land under `parameters` rather than top-level — keeps us spec-compatible.
