# Elasticsearch: How Search Works

Elasticsearch is a distributed search engine built on top of Apache Lucene. It is designed for fast full-text search, filtering, sorting, aggregations, and relevance ranking across large datasets.

In a microservice system like Carsties, Elasticsearch would usually act as a **search projection**, not the source of truth. `AuctionService` and Postgres would still own auction data, while Elasticsearch would store a searchable copy optimized for user queries.

## Why Elasticsearch Exists

Databases are excellent at storing source-of-truth data and answering structured queries. Search engines are optimized for a different job:

- Matching natural language text.
- Ranking results by relevance.
- Handling typos, synonyms, stemming, and tokenization.
- Filtering and aggregating large result sets quickly.
- Scaling search workloads across multiple machines.

For example, a user might search for `"red ford mustang"` and expect results for red Ford Mustangs, maybe including `"Ford Mustang GT"` or `"Mustang red coupe"`, even when the words are spread across different fields.

## Documents and Indexes

Elasticsearch stores data as JSON documents inside indexes.

For Carsties, an auction document might look like this:

```json
{
  "id": "1f4c4a7a-7a7b-4f0d-9e3f-8f78e8e94f5c",
  "make": "Ford",
  "model": "Mustang",
  "color": "Red",
  "year": 2021,
  "mileage": 12000,
  "seller": "alice",
  "auctionEnd": "2026-06-30T18:00:00Z",
  "updatedAt": "2026-06-02T10:15:00Z"
}
```

An **index** is similar to a searchable collection of documents. For example, an `auctions` index would contain all auction documents that users can search.

## Inverted Index

Like MongoDB text search, Elasticsearch relies on an inverted index.

Instead of scanning every auction document for each query, Elasticsearch maps terms back to the documents that contain them:

- `"ford"` -> Auction 1, Auction 4
- `"red"` -> Auction 1, Auction 9
- `"mustang"` -> Auction 1, Auction 4

When a user searches for `"red ford"`, Elasticsearch can quickly find candidate documents by looking up those terms in the inverted index.

## Analysis: Turning Text Into Searchable Terms

Before text can be indexed, Elasticsearch runs it through an **analyzer**.

An analyzer usually performs steps like:

1. **Character filtering:** Clean or normalize raw text.
2. **Tokenization:** Split text into terms.
3. **Lowercasing:** Convert `Ford` and `FORD` into `ford`.
4. **Stop-word removal:** Optionally remove common words like `the` or `and`.
5. **Stemming:** Optionally reduce words to a root form, such as `running` -> `run`.
6. **Synonym expansion:** Optionally treat related terms as equivalent, such as `car` and `vehicle`.

For example:

```text
"Red Ford Mustang GT"
```

may become:

```text
["red", "ford", "mustang", "gt"]
```

The important idea is that Elasticsearch does not just store raw strings. It stores analyzed terms that are optimized for matching.

## Mappings

A mapping tells Elasticsearch how each field should be stored and searched.

Example mapping for auction search:

```json
{
  "mappings": {
    "properties": {
      "make": { "type": "text" },
      "model": { "type": "text" },
      "color": { "type": "keyword" },
      "year": { "type": "integer" },
      "mileage": { "type": "integer" },
      "auctionEnd": { "type": "date" },
      "updatedAt": { "type": "date" }
    }
  }
}
```

Common field types:

- **`text`:** Full-text search field. Analyzed into tokens.
- **`keyword`:** Exact-match field. Good for filters, sorting, IDs, and categories.
- **`integer`, `long`, `double`:** Numeric fields.
- **`date`:** Date/time fields.
- **`boolean`:** True/false values.

Choosing `text` versus `keyword` matters. Searching `"Ford Mustang"` benefits from `text`; filtering by exact status like `"Live"` benefits from `keyword`.

## Query Flow

When a user searches for `"red ford"`:

1. The application sends a query to Elasticsearch.
2. Elasticsearch analyzes the search text using the configured analyzer.
3. It looks up matching terms in the inverted index.
4. It calculates a relevance score for each matching document.
5. It applies filters, sorting, pagination, and aggregations.
6. It returns matching documents or IDs back to the application.

Example query:

```json
{
  "query": {
    "multi_match": {
      "query": "red ford",
      "fields": ["make^3", "model^2", "color"]
    }
  }
}
```

The `^3` and `^2` values are boosts. They tell Elasticsearch that a match on `make` is more important than a match on `model`, and both are more important than a match on `color`.

## Relevance Scoring

Elasticsearch uses a scoring model called BM25 by default. The exact math is handled by Lucene, but the intuition is:

- A document scores higher when it contains more of the searched terms.
- A rare term usually matters more than a common term.
- A shorter field may score higher if it contains the same matching term.
- Boosted fields contribute more to the final score.

For auction search, this means a direct match on `make: Ford` and `model: Mustang` should rank above a document where `"ford"` only appears in a long description.

## Filters vs Queries

Elasticsearch separates **queries** from **filters**.

- **Queries** answer: "How relevant is this document?"
- **Filters** answer: "Should this document be included?"

Example:

```json
{
  "query": {
    "bool": {
      "must": [
        {
          "multi_match": {
            "query": "ford mustang",
            "fields": ["make^3", "model^2"]
          }
        }
      ],
      "filter": [
        { "term": { "color": "red" } },
        { "range": { "year": { "gte": 2018 } } }
      ]
    }
  }
}
```

In this query:

- `must` affects relevance scoring.
- `filter` narrows the result set without changing relevance.

Filters are often cache-friendly and are ideal for exact constraints like status, category, date ranges, and price ranges.

## Shards and Replicas

Elasticsearch is distributed. Each index is split into shards.

- **Primary shard:** A partition of the index data.
- **Replica shard:** A copy of a primary shard used for high availability and read scaling.

If an index has three primary shards, Elasticsearch can spread those shards across multiple nodes. Search requests run across the relevant shards, and Elasticsearch merges the results.

Replicas help in two ways:

- If a node fails, a replica can be promoted so the cluster stays available.
- Search traffic can be shared across primary and replica shards.

## Keeping Elasticsearch in Sync

Elasticsearch should not be the source of truth for auction data. It should be updated when source data changes.

Common sync options:

- **Event-driven indexing:** `AuctionService` publishes events such as `AuctionCreated`, `AuctionUpdated`, and `AuctionDeleted`. A search indexing consumer updates Elasticsearch.
- **Delta sync:** A background job periodically fetches records changed after the latest known `UpdatedAt`.
- **Rebuild job:** If the index is corrupted or mappings change, rebuild the full index from Postgres.

For Carsties, event-driven indexing fits naturally with the RabbitMQ/MassTransit approach described in [Asynchronous communication](asynchronous_communication.md).

## Deletes and Rebuilds

Search projections need delete handling.

If an auction is deleted in Postgres but Elasticsearch never hears about it, users may keep seeing stale results. A robust design should include one of these:

- Publish an `AuctionDeleted` event and delete the document from Elasticsearch.
- Use soft deletes and filter out deleted auctions.
- Periodically reconcile Elasticsearch against the source database.
- Rebuild the index from the source of truth when needed.

## Aggregations

Elasticsearch can calculate search-time summaries called aggregations.

Examples:

- Count auctions by make.
- Count auctions by color.
- Calculate min/max/average mileage.
- Build year or price range facets.

Example:

```json
{
  "size": 0,
  "aggs": {
    "makes": {
      "terms": {
        "field": "make.keyword"
      }
    }
  }
}
```

This is useful for building filter sidebars like:

- Ford: 120
- BMW: 80
- Toyota: 73

## Why Not Use Elasticsearch for Everything?

Elasticsearch is powerful, but it is not a replacement for the source-of-truth database.

Use Postgres or another transactional database for:

- Writes that need strong consistency.
- Transactions.
- Foreign keys and relational integrity.
- Source-of-truth business data.

Use Elasticsearch for:

- Full-text search.
- Relevance ranking.
- Faceted filtering.
- Search analytics.
- Fast read-heavy search workloads.

## Elasticsearch vs MongoDB Text Search

MongoDB text search is simpler and works well early in a project. Elasticsearch becomes useful when search quality and scale become a product feature.

| Feature | MongoDB Text Search | Elasticsearch |
| --- | --- | --- |
| Basic full-text search | Good | Excellent |
| Relevance tuning | Limited | Strong |
| Typo tolerance | Limited | Strong with fuzzy queries |
| Synonyms and analyzers | Limited | Strong |
| Faceted search | Possible | First-class |
| Operational complexity | Lower | Higher |
| Best fit | Simple search projection | Dedicated search experience |

## Practical Carsties Architecture

A production-ready Carsties search flow could look like this:

1. User creates or updates an auction through `AuctionService`.
2. `AuctionService` saves the change in Postgres.
3. `AuctionService` publishes an event to RabbitMQ.
4. A search indexing consumer receives the event.
5. The consumer indexes or updates the auction document in Elasticsearch.
6. `SearchService` queries Elasticsearch instead of MongoDB for user search requests.

This keeps Postgres as the source of truth while allowing Elasticsearch to focus on fast, high-quality search.

## Operational Notes

- Design mappings carefully before indexing large amounts of data.
- Use aliases so indexes can be rebuilt and swapped without downtime.
- Keep search documents denormalized enough to answer user queries quickly.
- Monitor disk usage, heap memory, query latency, and shard health.
- Do not create too many shards for small indexes.
- Have a rebuild path. Search projections are useful because they can be recreated.
