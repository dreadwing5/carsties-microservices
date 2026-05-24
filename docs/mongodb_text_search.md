# MongoDB Text Search: Under the Hood

When performing a full-text search in MongoDB using `.Match(Search.Full, searchTerm).SortByTextScore()`, the database relies on concepts borrowed from classical search engine design (similar to Elasticsearch or Google).

## 1. The Inverted Index
When a `Text` index is created on fields like `Make`, `Model`, and `Color`, MongoDB doesn't scan the raw documents every time you search. Instead, it creates an **Inverted Index**. 

It extracts the text from those fields, removes punctuation, converts it to lowercase, removes "stop words" (like "the", "and", "a"), and maps each remaining root word back to the document IDs:
* `"ford"` -> Document 1, Document 4
* `"red"` -> Document 1, Document 9
* `"mustang"` -> Document 4

This is why looking up a word is nearly instant—MongoDB just looks up the word in an alphabetical dictionary and instantly gets a list of document IDs.

## 2. The TF-IDF Algorithm
When you search for a term like `"Red Ford"`, MongoDB calculates a **Text Score** for every matching document using a formula based heavily on **TF-IDF** (Term Frequency - Inverse Document Frequency).

Here is the exact math breakdown of how MongoDB calculates the score for a specific document:

### A. Term Frequency (TF)
First, it calculates how densely the search term appears in the document.
**Math:** `(Number of times the word appears in the document) ÷ (Total number of words in the document)`
* *Example:* If the word "Red" appears 2 times in a description that is 100 words long, the TF is `2 / 100 = 0.02`.

### B. Inverse Document Frequency (IDF)
Next, it calculates how "rare" or valuable the word is across your entire database using a logarithmic function. This prevents common words from ruining your search results.
**Math:** `Log(Total number of documents in the DB ÷ Number of documents containing the word)`
* *Example (Rare Word):* If you have 1,000,000 cars, and only 10 are "Bugatti", the IDF is `Log(1,000,000 / 10) = 5.0`.
* *Example (Common Word):* If you have 1,000,000 cars, and 500,000 are "Ford", the IDF is `Log(1,000,000 / 500,000) = 0.30`.

### C. The Final Score Calculation
To get the final score for a word in a specific document, it simply multiplies them together:
**Math:** `TF × IDF`

If you searched for a multi-word phrase like `"Red Bugatti"`, MongoDB calculates the `TF × IDF` score for `"Red"` and the `TF × IDF` score for `"Bugatti"`, and simply adds them together to get the final `textScore` for that document.

### D. Field Weights (The MongoDB Bonus)
MongoDB adds one extra mathematical step to TF-IDF. You can assign "weights" to specific properties when you create the index. 
If you tell MongoDB that the `Make` property has a weight of `10`, but the `Color` property has a weight of `1`, MongoDB will take the final TF-IDF score and multiply it by 10 if the word was found inside the `Make` property!

## 3. The `$meta` Projection
When you call `.SortByTextScore()` in C#, the library injects a fake property into your query results called `textScore` using MongoDB's `$meta` operator, and then adds a `.Sort()` command pointing at that fake property:

```javascript
// What MongoDB actually executes:
db.Item.find(
   { $text: { $search: "red ford" } },
   { score: { $meta: "textScore" } }      // Injects the math score
).sort( { score: { $meta: "textScore" } } ) // Sorts highest to lowest
```

---

## What Happens With Millions of Records?

When your database scales to millions of records, full-text search changes drastically from a hardware perspective.

### 1. The "Working Set" and RAM Constraints
For a text index to remain fast with millions of records, the **entire inverted index must fit inside your server's RAM** (known as the "Working Set"). 
If the index grows larger than the available RAM, MongoDB has to start reading the index from the physical SSD/Hard Drive. This causes "Page Faults" and slows down searches from milliseconds to seconds.

### 2. High CPU Usage
Calculating TF-IDF math scores across hundreds of thousands of matches is extremely CPU-intensive. If 500 users search for "Ford" at the same time, the MongoDB server's CPU will spike to 100% trying to calculate and sort the scores for millions of "Ford" documents.

### 3. Sharding (Horizontal Scaling)
To handle massive scale, you must "Shard" the database. Sharding splits your millions of records across multiple physical servers. 
When a search query comes in, a router sends the search to *all* the servers at the same time. Each server searches its own chunk of data in RAM, calculates the scores, and returns the top results to the router, which merges them together and sends them to the user.

### When to Abandon MongoDB?
While MongoDB text search is great for millions of records if properly sharded, if you hit tens or hundreds of millions of records, or need fuzzy-matching (typo tolerance), it is standard industry practice to offload search to a dedicated engine like **Elasticsearch**, **Meilisearch**, or **Typesense**.
