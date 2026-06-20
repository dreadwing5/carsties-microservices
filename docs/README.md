# Documentation

This folder contains project notes for the Carsties microservices tutorial repo.

## Current Project Docs

- [Delta sync optimization](delta_sync_optimization.md): How `Carsties.SearchService` seeds and refreshes its MongoDB projection from `Carsties.AuctionService`.
- [Fault tolerance and data consistency](fault_tolerance_consistency.md): How the EF outbox, RabbitMQ, retries, fault consumers, and startup sync keep auction/search data eventually consistent.
- [MongoDB text search](mongodb_text_search.md): How the current MongoDB full-text search works and when to consider a dedicated search engine.
- [Elasticsearch search](elasticsearch_search.md): How Elasticsearch indexing, analysis, scoring, shards, replicas, and sync patterns work.
- [Asynchronous communication](asynchronous_communication.md): RabbitMQ, MassTransit, events, commands, reliability rules, and broker alternatives.
- [Source-generated request processing](source_generated_request_processing.md): Azure Table batching, EF Core generic updates, and a source-generated dispatcher pattern.
- [OAuth 2.0 and OpenID Connect](oauth_oidc.md): Authentication and authorization flows, tokens, sessions, refresh, MSAL, multiple tabs, BFF cookies, and XSS considerations.
- [Docker concepts](docker_concepts.md): Short notes on Docker volumes used by the local database containers.

## Reference Notes

- [.NET field notes](dotnet_field_notes.md): Small .NET and C# idioms, guard clauses, gotchas, and lessons collected while building the project.
- [.NET 10 features](dotnet_10_features.md): Summary of .NET 10, C# 14, ASP.NET Core, SDK, and tooling updates.
- [Engineering standards and architecture patterns](engineering_standards_architecture.md): Future-facing engineering guidance. Some sections describe patterns that are not yet implemented in this repo.
