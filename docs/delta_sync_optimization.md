# Microservice Delta Sync: HTTP Data Seeding

When building distributed systems, maintaining a "Single Source of Truth" is critical. In our architecture, the **AuctionService** (and its Postgres database) is the definitive source of truth for all auction data. 

The **SearchService** uses a MongoDB database designed specifically to project and index this data for lightning-fast queries. However, the SearchService needs a reliable way to get its initial data and stay synchronized.

## The Problem: Full Synchronization
If the SearchService relies on a hardcoded `auctions.json` file for initialization, it will immediately have stale data if any new auctions were created in the AuctionService since the JSON file was last updated.

The naive solution is to have the SearchService make an HTTP request to the AuctionService on startup: `GET /api/auctions` to download the entire database. 

However, as the system grows to millions of records, a "Full Sync" becomes catastrophic:
1. **Performance:** Sending gigabytes of JSON over the network is incredibly slow.
2. **Resource Exhaustion:** It will spike the CPU and RAM of both microservices, potentially crashing them.
3. **Database Load:** It executes a massive query against the Postgres database, degrading performance for actual end-users.

## The Solution: Delta Sync (Date Filtering)
To solve this, we use a **Delta Sync** approach utilizing an `UpdatedAt` timestamp.

Instead of asking for everything, the SearchService performs the following workflow on startup:

1. **Check Local State:** The SearchService queries its own MongoDB to find the most recently updated item.
   > *"The most recent auction I know about was updated on May 24th at 2:00 PM."*

2. **Targeted Request:** It sends an HTTP request to the AuctionService with a date parameter.
   > `GET /api/auctions?date=2026-05-24T14:00:00`

3. **Optimized Response:** The AuctionService queries Postgres for **only** the records where `UpdatedAt > [Provided Date]`. 
   > Instead of returning 10 million records, it instantly returns just the 5 records that were created or modified while the SearchService was offline.

### Benefits
* **Speed:** Syncs take milliseconds instead of minutes.
* **Resiliency:** If the SearchService's MongoDB database is ever completely wiped, the local state check returns `null`. The SearchService will then automatically fall back to a full sync, perfectly rebuilding its index from scratch without manual intervention.
* **Network Efficiency:** Massively reduces bandwidth usage within the internal cluster network.

## Fault Tolerance and Resiliency

In a microservice architecture, it is completely normal and expected for a service to be temporarily unavailable (e.g., during a rolling deployment or a crash). 

If the **SearchService** attempts to perform its HTTP data sync on startup but the **AuctionService** is down, here is what happens:
1. The HTTP Client throws an `HttpRequestException` (Connection Refused).
2. The `SearchService` catches this exception in `Program.cs`, logs the error to the console, and gracefully continues its startup process.
3. The `SearchService` remains functional and will serve search requests using whatever data it already has in its local MongoDB instance (or 0 items if the database is brand new).

### The Solution: Polly & Background Initialization
To make this synchronization completely bulletproof, we implemented a two-part solution:

1. **Polly Retry Policy:** We wrapped our HTTP Client with a Polly resilience policy. If the request fails (e.g., AuctionService is down), Polly automatically waits 3 seconds and retries indefinitely until the AuctionService comes back online.
2. **Background Sync (`ApplicationStarted`):** If we awaited the database initialization during startup, the Polly retry loop would block the SearchService from booting! To solve this, we wrapped the initialization inside `app.Lifetime.ApplicationStarted.Register()`. 

This guarantees that the SearchService immediately finishes booting up, binds to its port, and can instantly serve requests using its old MongoDB data. Meanwhile, in the background, Polly silently retries until the AuctionService is available to sync the missing data.
