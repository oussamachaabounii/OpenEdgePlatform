# ADR-004: MassTransit over a raw RabbitMQ client

- Status: accepted
- Date: 2026-05-26

## Context

Three services communicate over RabbitMQ: the broker publishes provisioning events, the
worker consumes and re-publishes completion events, and the control plane consumes those. We
have to pick a client library.

Options considered:

- **RabbitMQ .NET Client (raw AMQP)** — the official low-level client.
- **MassTransit** — opinionated framework on top of RabbitMQ / Azure Service Bus / Amazon SQS.
- **EasyNetQ** — simpler RabbitMQ-only convenience wrapper.

## Decision

Use **MassTransit** as the abstraction. Wire RabbitMQ as the transport in `Program.cs`. Keep
all references to MassTransit confined to `*.Infrastructure` and host `Program.cs` files.

## Rationale

1. **Transport portability.** MassTransit treats RabbitMQ, Azure Service Bus, and Amazon SQS
   as interchangeable transports. If we ever migrate to Azure or AWS-native messaging, only
   `Program.cs` changes.
2. **Built-in patterns.** Retries, redelivery, dead-letters, message scheduling, sagas — all
   first-class. With the raw AMQP client we'd build these ourselves.
3. **Consumer dependency injection.** MassTransit integrates with `IServiceProvider` so
   consumer classes can request scoped services. This matters because the worker's
   `ProvisioningRequestHandler` resolves the DB context per message.

## Tradeoffs

- **Heavier dependency.** MassTransit is a sizeable assembly graph compared to the raw client.
  Worth it for the abstraction.
- **Sometimes opaque.** Errors in topology setup can be hard to debug. We mitigate by keeping
  exchange / queue naming explicit via the `MessagingTopics` constants.

## Consequences

- `IProvisioningPublisher` in `ServiceBroker.Core` knows nothing about MassTransit or
  RabbitMQ — it's a pure domain interface. The MassTransit-backed implementation lives in
  `ServiceBroker.Infrastructure`. Swapping transports does not touch core code.
- Tests use the MassTransit `ITestHarness` rather than running a real broker — fast and
  deterministic.
