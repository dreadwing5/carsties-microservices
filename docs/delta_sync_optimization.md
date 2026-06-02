# Delta Sync: HTTP Data Seeding

`AuctionService` and its Postgres database are the source of truth for auction data.

`SearchService` keeps a MongoDB projection of that data so search queries can stay fast and independent. Because MongoDB is only a projection, it needs a reliable way to build its initial dataset and catch up when it has been offline.

## Problem: Full Synchronization

If `SearchService` relied on a hardcoded `auctions.json` seed file, MongoDB would become stale as soon as new auctions were created in `AuctionService`.

A simple fix is to call `GET /api/auctions` on startup and download everything. That works for small datasets, but it does not scale well:

1. **Performance:** Large JSON responses are slow to transfer and deserialize.
2. **Resource usage:** Both services spend CPU and memory handling records that may already exist in MongoDB.
3. **Database load:** Postgres has to serve a large query even when only a few records changed.

## Solution: Delta Sync

`SearchService` uses a delta sync based on the `UpdatedAt` timestamp.

On startup:

1. `SearchService` queries MongoDB for the most recent `UpdatedAt` value it already has.
2. It calls `AuctionService` with that value: `GET /api/auctions?date=2026-05-24T14:00:00Z`.
3. `AuctionService` returns only auctions where `UpdatedAt` is greater than the supplied date.
4. `SearchService` upserts those records into MongoDB.

If MongoDB is empty, the local state check returns no date. In that case, `SearchService` falls back to a full sync and rebuilds the projection from scratch.

## Benefits

- **Fast normal startup:** Most syncs transfer only recently changed auctions.
- **Self-healing projection:** A wiped MongoDB database can be rebuilt from `AuctionService`.
- **Lower database pressure:** Postgres avoids repeated full-table sync requests.
- **Lower network usage:** Internal traffic stays proportional to the number of changed records.

## Fault Tolerance

Temporary service outages are normal in a microservice system. For example, `AuctionService` may be unavailable during a restart or deployment.

The current `SearchService` startup flow handles that with two pieces:

1. **Polly retry policy:** The HTTP client retries transient failures every 3 seconds until `AuctionService` becomes available.
2. **Background initialization:** The sync starts inside `app.Lifetime.ApplicationStarted.Register(...)`, so the service can bind to its port before the sync completes.

This means `SearchService` can continue serving whatever data already exists in MongoDB while the background sync retries.

## Current Limitations

- `UpdatedAt` must be maintained correctly for creates and updates. If an update does not change `UpdatedAt`, `SearchService` will not see it during delta sync.
- The current flow only fetches auctions changed after the last known timestamp. Deletes need a separate event, tombstone, or soft-delete strategy to remove records from MongoDB.
- Date values should stay in UTC to avoid timezone drift between services.
