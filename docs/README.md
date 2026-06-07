# Documentation

This folder contains project notes for the Carsties microservices tutorial repo.

## Current Project Docs

- [Delta sync optimization](delta_sync_optimization.md): How `SearchService` seeds and refreshes its MongoDB projection from `AuctionService`.
- [MongoDB text search](mongodb_text_search.md): How the current MongoDB full-text search works and when to consider a dedicated search engine.
- [Elasticsearch search](elasticsearch_search.md): How Elasticsearch indexing, analysis, scoring, shards, replicas, and sync patterns work.
- [Asynchronous communication](asynchronous_communication.md): RabbitMQ, MassTransit, events, commands, reliability rules, and broker alternatives.
- [Source-generated request processing](source_generated_request_processing.md): Azure Table batching, EF Core generic updates, and a source-generated dispatcher pattern.
- [Docker concepts](docker_concepts.md): Short notes on Docker volumes used by the local database containers.

## Reference Notes

- [.NET 10 features](dotnet_10_features.md): Summary of .NET 10, C# 14, ASP.NET Core, SDK, and tooling updates.
- [Engineering standards and architecture patterns](engineering_standards_architecture.md): Future-facing engineering guidance. Some sections describe patterns that are not yet implemented in this repo.
