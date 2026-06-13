# Asynchronous Communication

This document explains how asynchronous service communication fits into the Carsties microservices project, with RabbitMQ and MassTransit as the current local-development stack.

## Why Use Asynchronous Communication?

Synchronous calls, such as HTTP or gRPC, are useful when the caller needs an immediate answer. Asynchronous messaging is useful when a service needs to notify other services that something happened, or hand work to another process without waiting for it to finish.

Benefits:

- **Temporal decoupling:** The producing service and consuming service do not need to be online at exactly the same time.
- **Load leveling:** A queue can absorb bursts while consumers process messages at their own pace.
- **Loose coupling:** A publisher does not need to know every downstream service that reacts to an event.
- **Resilience:** Failed processing can be retried or moved to an error/dead-letter path instead of losing the whole workflow.
- **Eventual consistency:** Services can own their own data stores while reacting to changes from other services.

Tradeoffs:

- Data is not immediately consistent across services.
- Message handlers must be idempotent because duplicate delivery can happen.
- Operational visibility becomes important: queues, retries, dead letters, and consumer lag need monitoring.
- Message contracts become part of the public API between services.

## Current Carsties Setup

The local infrastructure in `docker-compose.yml` includes:

- `postgres` for `Carsties.AuctionService`
- `mongodb` for `Carsties.SearchService`
- `rabbitmq` for asynchronous messaging

Both `Carsties.AuctionService` and `Carsties.SearchService` reference `MassTransit.RabbitMQ` and register a RabbitMQ bus in `Program.cs`.

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

The project currently has the messaging infrastructure wired, but the shared `Carsties.Contracts` project does not yet define event contracts and the services do not yet publish or consume messages. The next natural step is to add integration events such as `AuctionCreated`, `AuctionUpdated`, and `AuctionDeleted`, then have `Carsties.SearchService` consume those events to keep its MongoDB search projection up to date.

This repo pins `MassTransit.RabbitMQ` to `8.5.5` to avoid the MassTransit `9.x` license requirement during local development.

## RabbitMQ

RabbitMQ is the message broker. It stores and routes messages between services.

Important RabbitMQ concepts:

- **Producer:** Application code that sends or publishes a message.
- **Consumer:** Application code that receives and processes a message.
- **Exchange:** Routing point where published messages are sent first.
- **Queue:** Buffer that stores messages until consumers process them.
- **Binding:** Rule connecting an exchange to a queue.
- **Acknowledgement:** Signal from a consumer that the message was processed successfully.
- **Dead-letter queue:** Place for messages that cannot be processed after retries or rejection.

RabbitMQ is a good default for Carsties because it is easy to run locally, works well with Docker Compose, supports competing consumers, and maps neatly to event-driven microservice workflows.

## MassTransit

MassTransit is the .NET messaging library used by the services. It sits above RabbitMQ and gives the application a cleaner programming model.

MassTransit helps with:

- Registering consumers through dependency injection.
- Publishing events without manually creating RabbitMQ exchanges and bindings.
- Sending commands to specific endpoints.
- Retry policies, delayed redelivery, and error queues.
- Request/response messaging when a temporary asynchronous RPC pattern is needed.
- Outbox patterns to reduce the risk of saving database state but failing to publish the related event.

In practice:

- RabbitMQ is the broker.
- MassTransit is the .NET abstraction and runtime integration.
- Contracts are the shared message shapes that services agree on.

## Events vs Commands

Use **events** for facts that already happened:

```csharp
public record AuctionCreated(
    Guid Id,
    string Make,
    string Model,
    string Seller,
    DateTime AuctionEnd);
```

An event name should be past tense: `AuctionCreated`, `BidPlaced`, `PaymentCaptured`.

Use **commands** for work that should be done by one service:

```csharp
public record RebuildSearchIndex(Guid AuctionId);
```

A command name should be imperative: `RebuildSearchIndex`, `CancelAuction`, `NotifyBidWinner`.

For Carsties, search projection updates should usually be events, because `Carsties.AuctionService` should not know or care exactly which services react to auction changes.

## Recommended Carsties Flow

A typical event-driven update flow:

1. A user creates an auction through `Carsties.AuctionService`.
2. `Carsties.AuctionService` writes the auction to Postgres.
3. `Carsties.AuctionService` publishes `AuctionCreated`.
4. RabbitMQ routes the event to interested queues.
5. `Carsties.SearchService` consumes the event.
6. `Carsties.SearchService` updates its MongoDB search document.

This keeps Postgres as the source of truth and lets `Carsties.SearchService` maintain its own read model.

## Reliability Rules

Message-driven systems need defensive handlers.

- **Make consumers idempotent:** Processing the same event twice should not corrupt state.
- **Store event IDs or use natural idempotency:** For example, upsert a search document by auction ID.
- **Prefer outbox for critical events:** Save domain state and outgoing messages as one durable unit, then dispatch messages after commit.
- **Use retries carefully:** Retry transient failures, but do not endlessly retry invalid messages.
- **Observe error queues:** Failed messages should page a developer or appear on a dashboard.
- **Version contracts:** Add fields in a backward-compatible way and avoid renaming message types casually.

## Broker and Service Options

RabbitMQ is not the only option. The right tool depends on delivery semantics, cloud provider, scale, and operational preferences.

| Option | Best For | Notes |
| --- | --- | --- |
| **RabbitMQ** | General microservice messaging, queues, pub/sub, local development | Strong fit for Carsties. Simple local Docker story and good MassTransit support. |
| **Azure Service Bus** | Managed enterprise queues and topics on Azure | Good when deploying to Azure and wanting managed durability, topics/subscriptions, dead-lettering, and operational tooling. |
| **AWS SQS + SNS** | Managed queueing and pub/sub on AWS | SQS handles queues; SNS handles fan-out to queues, Lambda, HTTP endpoints, and other subscribers. |
| **Apache Kafka** | High-throughput event streams, replay, analytics pipelines | Excellent for append-only event logs and replay, but heavier operationally than RabbitMQ for a small tutorial app. |
| **Redis Streams** | Lightweight stream processing when Redis is already in the stack | Useful for simple streams and consumer groups, but it is usually not the first choice for durable cross-service messaging in this repo. |

## When to Use HTTP Instead

Do not turn every service interaction into a message.

Use HTTP/gRPC when:

- The caller needs an immediate answer.
- The operation is user-facing and must complete before returning.
- The dependency is simple and availability expectations are clear.

Use messaging when:

- Multiple services need to react to a change.
- Work can happen after the request returns.
- You need buffering, retries, or load leveling.
- The producing service should not depend directly on downstream service availability.

## Implementation Checklist

When adding a new async workflow to Carsties:

1. Add the message contract to `src/Shared/Carsties.Contracts`.
2. Publish the event after the source-of-truth database write succeeds.
3. Add a public MassTransit consumer in the receiving service.
4. Register consumers with `AddConsumers(...)` or the repo's chosen registration style.
5. Make the consumer idempotent.
6. Add integration tests or a local manual test using RabbitMQ from Docker Compose.
7. Document the event contract and ownership.

## References

- [MassTransit RabbitMQ transport](https://masstransit.io/documentation/configuration/transports/rabbitmq)
- [MassTransit RabbitMQ usage](https://masstransit.io/usage/transports/rabbitmq.html)
- [RabbitMQ exchanges](https://www.rabbitmq.com/docs/exchanges)
- [RabbitMQ queues](https://www.rabbitmq.com/docs/queues)
- [Azure Service Bus queues, topics, and subscriptions](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-queues-topics-subscriptions)
- [Amazon SQS documentation](https://docs.aws.amazon.com/sqs/)
- [Amazon SNS documentation](https://aws.amazon.com/documentation-overview/sns/)
- [Apache Kafka documentation](https://kafka.apache.org/documentation/)
- [Redis Streams documentation](https://redis.io/docs/latest/develop/data-types/streams/)
