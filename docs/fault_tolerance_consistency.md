# Fault Tolerance and Data Consistency

`Carsties.AuctionService` owns the source-of-truth auction data in Postgres.

`Carsties.SearchService` owns a MongoDB read model that is optimized for search. MongoDB is not the source of truth; it is a projection that can be updated from events or rebuilt from `Carsties.AuctionService`.

The system is designed for eventual consistency. Auction writes should remain correct in Postgres, while search results may temporarily lag during service or broker outages.

## The Main Problem: Atomicity

When `Carsties.AuctionService` creates, updates, or deletes an auction, two things need to happen:

1. Save the local database change in Postgres.
2. Publish an integration event such as `AuctionCreated`, `AuctionUpdated`, or `AuctionDeleted`.

Without an outbox, those two operations are separate writes to separate systems:

- Postgres
- RabbitMQ

That creates a consistency problem.

Example failure:

1. The auction is saved successfully in Postgres.
2. The service crashes before publishing `AuctionCreated`.
3. Postgres now contains the auction, but `Carsties.SearchService` never hears about it.

The reverse can also be dangerous:

1. `AuctionCreated` is published.
2. The database transaction fails.
3. Other services may react to an auction that does not actually exist.

The traditional solution would be a distributed transaction, often called two-phase commit or 2PC. In modern microservice systems this is usually avoided because it is expensive, operationally complex, and not supported consistently across all infrastructure.

The outbox pattern avoids 2PC by putting the local entity change and the outgoing message into the same local database transaction.

## How the Outbox Helps

`Carsties.AuctionService` uses the MassTransit Entity Framework outbox:

```csharp
x.AddEntityFrameworkOutbox<AuctionDbContext>(o =>
{
    o.QueryDelay = TimeSpan.FromSeconds(10);
    o.UsePostgres();
    o.UseBusOutbox();
});
```

The outbox tables are part of `AuctionDbContext`:

```csharp
modelBuilder.AddInboxStateEntity();
modelBuilder.AddOutboxMessageEntity();
modelBuilder.AddOutboxStateEntity();
```

When an endpoint publishes an event through `IPublishEndpoint`, MassTransit stores that outgoing message in the outbox as part of the same EF Core database transaction as the auction change.

This gives the service one atomic local save:

- auction row saved,
- outbox message saved,
- or neither is saved.

After the transaction commits, MassTransit delivers the outbox message to RabbitMQ asynchronously.

Important distinction: the main reason for the outbox is not that RabbitMQ lacks persistence. Message brokers can persist messages too. The outbox exists because the service needs atomicity between its own database write and its intent to publish a message, without using a distributed transaction.

## Consistency Model

The consistency model is:

- Postgres in `Carsties.AuctionService` is the source of truth.
- RabbitMQ transports integration events between services.
- MongoDB in `Carsties.SearchService` is a rebuildable search projection.
- Search data is eventually consistent with auction data.

This means the user-facing search index can be stale for a short time, but the authoritative auction data remains correct.

## Scenario: Search Service Is Down

Example: a user creates an auction while `Carsties.SearchService` is stopped.

1. `Carsties.AuctionService` saves the auction in Postgres.
2. The `AuctionCreated` event is saved in the outbox in the same transaction.
3. MassTransit publishes the event to RabbitMQ after the transaction commits.
4. `Carsties.SearchService` is offline, so it cannot consume the event yet.
5. RabbitMQ keeps the message in the search queue.
6. When `Carsties.SearchService` starts again, it consumes the queued event and writes the item to MongoDB.
7. Startup delta sync also asks `Carsties.AuctionService` for auctions changed after the latest local `UpdatedAt` value and upserts them into MongoDB.

Result: auction data remains correct immediately. Search is temporarily stale, then catches up.

## Scenario: RabbitMQ Is Down

Example: a user creates an auction while RabbitMQ is unavailable.

1. `Carsties.AuctionService` saves the auction in Postgres.
2. The `AuctionCreated` event is saved in the outbox in the same Postgres transaction.
3. The local transaction can commit without requiring a distributed transaction with RabbitMQ.
4. MassTransit later attempts to deliver the outbox message.
5. When RabbitMQ is available again, MassTransit publishes the stored outbox message.
6. `Carsties.SearchService` receives the event and updates MongoDB.

Result: the atomic local save is protected. RabbitMQ unavailability delays delivery, but it does not create the "database saved but publish intent lost" problem.

In production, broker redundancy and high availability are still important. The outbox is not a replacement for running RabbitMQ reliably; it solves the application-level atomicity problem around database changes and event publication.

## Scenario: MongoDB Write Fails

Example: `Carsties.SearchService` receives `AuctionUpdated`, but MongoDB is temporarily unavailable.

1. The consumer attempts to update MongoDB.
2. If the update is not acknowledged, the consumer throws a `MessageException`.
3. MassTransit can retry message handling according to the configured retry policy.
4. If retries are exhausted, MassTransit moves the message into its fault/error flow.

The `AuctionCreated` receive endpoint currently configures retry:

```csharp
e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(5)));
```

Result: the consumer does not silently ignore a failed MongoDB write. The message is retried and, if it still fails, becomes visible as a fault/error instead of disappearing.

## Fault Consumer

`AuctionCreatedFaultConsumer` demonstrates how to consume `Fault<AuctionCreated>` messages.

In the current code, it handles a demo `ArgumentException` case by changing the model value and republishing the message. In a real application, this is where the service would usually:

- log structured failure details,
- send the error to monitoring,
- update an operational dashboard,
- trigger an alert,
- or move the message into a manual repair workflow.

## Startup Delta Sync

`Carsties.SearchService` also has a recovery path outside RabbitMQ.

On startup, it queries MongoDB for the newest `UpdatedAt` value it already has, then calls:

```http
GET /api/auctions?date={lastUpdated}
```

`Carsties.AuctionService` returns auctions updated after that timestamp, and `Carsties.SearchService` upserts them into MongoDB.

If MongoDB is empty, this behaves like a full rebuild.

This helps when the search projection is stale or empty. It also gives the system a practical self-healing path because MongoDB is only a projection, not the source of truth.

## What This Protects Against

- Saving an auction but losing the intent to publish the matching integration event.
- Publishing an event for a database change that did not commit.
- Temporary RabbitMQ downtime delaying event delivery.
- `Carsties.SearchService` being down while auction events are produced.
- Temporary MongoDB failures while consuming messages.
- MongoDB search data being stale or empty after a restart.

## Current Limitations

- Retry is explicitly configured on the `search-auction-created` endpoint. If update/delete consumers need the same retry behavior, configure retries for their receive endpoints or at the bus/endpoint level.
- The outbox is configured in `Carsties.AuctionService`. Other services that save local state and publish events should use their own outbox.
- Search is eventually consistent, so search results can lag behind the auction database.
- Startup delta sync depends on `UpdatedAt`. Updates must maintain this value correctly.
- Hard deletes are best handled by events because a timestamp-based delta sync cannot discover rows that no longer exist in Postgres.

## Mental Model

Think of the outbox as making this local transaction atomic:

```text
Save auction change + save outgoing event intent
```

Then think of RabbitMQ and the search consumers as the delivery and projection steps that happen after that local transaction commits.

The outbox does not remove the need for reliable broker infrastructure. It removes the need for a distributed transaction between the auction database and the message broker.
