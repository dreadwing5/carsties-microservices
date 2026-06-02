# MongoDB Text Search: Under the Hood

`SearchService` performs MongoDB full-text search with `.Match(Search.Full, searchTerm).SortByTextScore()`. MongoDB handles this using ideas borrowed from classical search engines: inverted indexes, term scoring, and relevance-based sorting.

## Inverted Index

When a text index is created on fields such as `Make`, `Model`, and `Color`, MongoDB does not scan every document for every search. Instead, it builds an inverted index.

The index maps normalized terms back to matching documents:

- `"ford"` -> Document 1, Document 4
- `"red"` -> Document 1, Document 9
- `"mustang"` -> Document 4

That is why text search can be much faster than scanning raw document fields.

## Text Scores

When you search for a term like `"red ford"`, MongoDB calculates a text score for matching documents. The exact implementation details are MongoDB-specific, but the intuition is similar to TF-IDF:

- **Term frequency:** A term that appears more often in a document is usually more relevant.
- **Inverse document frequency:** A rare term is usually more meaningful than a common term.
- **Field weights:** Matches in important fields can be weighted higher than matches in less important fields.

For example, if `Make` has a weight of `10` and `Color` has a weight of `1`, a match on `Make` contributes more to the relevance score than the same term found in `Color`.

## The `$meta` Projection

When you call `.SortByTextScore()` in C#, MongoDB uses the `$meta` operator to sort by the computed text score:

```javascript
db.Item.find(
  { $text: { $search: "red ford" } },
  { score: { $meta: "textScore" } }
).sort({ score: { $meta: "textScore" } })
```

---

## Scaling Considerations

Full-text search changes from a simple query problem into an infrastructure problem as the dataset grows.

### Working Set and RAM

For text search to stay fast, the active portion of the index should fit in memory. If MongoDB has to read large parts of the index from disk, searches can slow down significantly.

### CPU Cost

Scoring and sorting many matches can be CPU-intensive. Very common search terms, such as popular makes or colors, may match a large percentage of the collection.

### Sharding

At larger scale, MongoDB sharding can split data across multiple servers. Search queries can then run across shards, with results merged before being returned to the caller.

## When to Use a Dedicated Search Engine

MongoDB text search is a good fit for simple search needs, especially early in the project. Consider a dedicated search engine such as Elasticsearch, Meilisearch, or Typesense when you need:

- Typo tolerance or fuzzy matching.
- Advanced ranking and boosting.
- Faceted search at high scale.
- Search analytics.
- More control over tokenization, stemming, and synonyms.
