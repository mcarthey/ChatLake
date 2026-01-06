# Why Shrinking Your Data Makes It Smarter: The UMAP + HDBSCAN Journey

*Follow-up to [What 1,290 Conversations Taught Me](https://learnedgeek.com/Blog/Post/what-1290-conversations-taught-me)*

---

After my last post about analyzing ChatGPT conversation exports, I hit a wall. The clustering was... bad. Like, "Mariah Carey Christmas songs grouped with CBD regulations and griddle pan recommendations" bad.

This post is about the rabbit hole I went down to fix it, and the counterintuitive lesson I learned: **sometimes you have to throw away information to find meaning.**

## The Problem: 768 Dimensions of Loneliness

Modern text embeddings are incredible. When you run text through a model like `nomic-embed-text`, you get back a 768-dimensional vector that captures semantic meaning. Similar concepts end up near each other in this high-dimensional space.

So I did what seemed logical: run a clustering algorithm on these embeddings. Find groups of similar conversations. Easy, right?

**Wrong.**

My first attempt with KMeans produced clusters where completely unrelated topics got lumped together. My conversations about 3D printing were grouped with recipe discussions. Unity game development sat next to fitness tracking.

"Fine," I thought. "KMeans forces everything into clusters. Let me try HDBSCAN instead - it can identify outliers!"

HDBSCAN's result? **86% of my data was classified as noise.** And the remaining 14% in actual clusters? Still garbage. A cluster named "Cheat Script Development" contained novel writing, gaming discussions, fitness topics, and random programming questions.

What was going on?

## The Curse of Dimensionality (It's Not Just a Scary Name)

Here's something that broke my brain when I first learned it: **in high-dimensional space, everything is far away from everything else, and simultaneously, everything is roughly equidistant.**

Wait, what?

Imagine you're in a 2D room. You can easily tell that the couch is closer to the coffee table than to the kitchen. Distances are intuitive.

Now imagine a 768-dimensional room. (Don't worry, I can't either.) In this space, something weird happens mathematically:

1. **Distances converge**: The difference between the "nearest" and "farthest" points becomes proportionally tiny
2. **Density becomes meaningless**: HDBSCAN looks for dense regions, but when everything is sparse and equidistant, there ARE no dense regions
3. **Neighbors get weird**: Your "nearest neighbors" might not be meaningfully closer than random points

This is the **curse of dimensionality**. It's why my 768D embeddings weren't clustering properly - not because the embeddings were bad, but because clustering algorithms can't perceive structure in that space.

## The Counterintuitive Solution: Throw Away 753 Dimensions

Here's where it gets weird. The fix isn't to add more data or use fancier embeddings. It's to **reduce** from 768 dimensions down to just 15.

"But wait!" I hear you asking (because I asked the same thing). "Wouldn't reducing dimensions cause MORE grouping of unrelated topics? Aren't we losing information?"

Yes, we're losing information. But here's the key insight:

**We're strategically losing the RIGHT information.**

Think of it like this: a 768-dimensional embedding captures *everything* about a piece of text - its topic, tone, writing style, sentence structure, vocabulary complexity, cultural references, and thousands of other subtle features.

But for clustering by TOPIC, we don't need all that. In fact, all those extra dimensions are noise that obscure the topical signal.

## Enter UMAP: Smart Compression

UMAP (Uniform Manifold Approximation and Projection) isn't just throwing away random dimensions. It's doing something clever:

1. **Finds neighborhoods**: For each point, UMAP identifies its nearest neighbors in the original 768D space
2. **Preserves local structure**: It tries to keep those neighborhood relationships intact in the lower-dimensional space
3. **Allows global flexibility**: Points that were far apart can end up wherever, but nearby points stay nearby

The magic is that UMAP preserves **semantic neighborhoods**. If your embedding about "React component optimization" was close to "JavaScript performance tuning" in 768D space, they'll still be close in 15D space.

What gets lost? The subtle distinctions that differentiate "React optimization" from "Vue optimization" might blur a bit. But guess what? For clustering purposes, those probably SHOULD be in the same cluster anyway!

## Why 15 Dimensions?

Fair question. The answer is: it's a tunable parameter, and there's no magic number.

- **Too few (2-5)**: You lose too much nuance. Everything blurs together.
- **Too many (50+)**: You start hitting the curse of dimensionality again.
- **Sweet spot (10-30)**: Enough dimensions to preserve meaningful distinctions, few enough for density-based clustering to work.

BERTopic (a popular topic modeling library) defaults to 5 dimensions. I went with 15 because I wanted slightly more granular topics. Your mileage may vary.

## The Full Pipeline: What Actually Works

Here's the approach that finally produced sensible clusters:

```
Raw Text
    ↓
Embedding Model (nomic-embed-text)
    ↓
768-dimensional vectors
    ↓
UMAP (reduce to 15D, preserve neighborhoods)
    ↓
15-dimensional vectors
    ↓
HDBSCAN (find density-based clusters)
    ↓
Meaningful topic clusters + identified noise
```

This is essentially what BERTopic does, and it's become the industry standard for semantic clustering.

## "But Why Not Just Use Cosine Similarity in 768D?"

Another great question I wrestled with!

Cosine similarity does help in high dimensions - it measures the angle between vectors rather than the distance, which partially mitigates the curse of dimensionality.

But HDBSCAN fundamentally needs to find **dense regions** in space. Even with cosine similarity:
- The "density" concept breaks down in 768D
- Computing all pairwise similarities for thousands of points is expensive
- The algorithm still struggles to find meaningful cluster boundaries

UMAP doesn't just help with the distance metric - it fundamentally restructures the space so that density-based clustering becomes possible.

## "What About PCA? Isn't That Simpler?"

PCA (Principal Component Analysis) is indeed simpler and faster. But there's a crucial difference:

- **PCA**: Preserves global variance (the "big picture" directions of variation)
- **UMAP**: Preserves local neighborhoods (who's near whom)

For clustering, we care about local structure. We want points that are semantically similar to stay together. PCA might preserve the overall "shape" of your data while scrambling the local neighborhoods.

That said, some pipelines use PCA as a first step (768D → 50D) before UMAP (50D → 15D). The speed improvement can be worth it for large datasets.

## What Happens to the "Noise"?

HDBSCAN marks points as noise when they don't fit cleanly into any cluster. This isn't a bug - it's a feature!

In my conversation data, noise points are often:
- **One-off topics**: That single conversation about sourdough bread doesn't need its own cluster
- **Genuinely ambiguous**: Conversations that span multiple topics
- **Unique explorations**: Novel questions that don't fit patterns

For my use case (organizing conversations into projects), noise is fine. Those conversations just stay ungrouped until I manually categorize them or until I import more similar conversations that form a cluster.

A healthy noise percentage is typically 20-40%. If you're seeing 80%+ noise, your parameters are too strict. If you're seeing <10% noise, you might be forcing unrelated things into clusters.

## The Humbling Lesson

I spent way too long trying to make raw embeddings cluster properly. I tried:
- Different clustering algorithms (KMeans, HDBSCAN)
- Filtering out conversational greetings ("Hey Eva!")
- Outlier detection thresholds
- Various parameter tuning

None of it worked until I accepted the counterintuitive truth: **the embeddings were too good**. They captured so much information that the signal I cared about (topic) was drowned in noise (style, structure, tone).

Sometimes the path forward isn't adding complexity - it's strategic simplification.

## Try It Yourself

If you're working with text embeddings and clustering isn't working:

1. **Don't blame the embeddings** - they're probably fine
2. **Reduce dimensions first** - UMAP with 10-30 target dimensions
3. **Then cluster** - HDBSCAN works great in reduced space
4. **Tune iteratively** - adjust UMAP neighbors, HDBSCAN min_cluster_size

The combination of UMAP + HDBSCAN has become the de facto standard for a reason. It works.

---

## FAQ: Questions I Asked Myself

**Q: How long does UMAP take?**
A: Longer than you'd expect. For ~2000 embeddings, expect 30-60 seconds. It's doing real mathematical work to preserve neighborhoods.

**Q: Is the reduction deterministic?**
A: With a fixed random seed, yes. UMAP uses randomization internally, so set a seed for reproducibility.

**Q: Can I visualize the reduced embeddings?**
A: Yes! Reduce to 2D or 3D and plot them. It's a great way to sanity-check your clustering before running HDBSCAN.

**Q: What if my clusters are still bad after UMAP?**
A: Try adjusting `n_neighbors` in UMAP (lower = more local structure, higher = more global). Also check your embedding model - garbage in, garbage out.

**Q: Should I re-embed with a different model?**
A: Maybe, but try UMAP first. The problem is usually the dimensionality, not the embeddings.

---

*This post is part of my ongoing series about building ChatLake, a tool for analyzing and organizing ChatGPT conversation exports. The code is open source at [GitHub link].*

*Next up: What the clusters actually revealed about my year of AI conversations...*
