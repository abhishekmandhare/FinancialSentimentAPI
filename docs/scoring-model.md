# Sentiment Scoring Model

All scoring logic lives in `Application/Features/Sentiment/SentimentMath.cs`. Configuration is in `appsettings.json` under `SentimentScoring`.

## Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| `HalfLifeHours` | 36 | Hours for an article's weight to halve |
| `DefaultWindowDays` | 14 | Data window for trending, watchlist, and snapshots |
| `MaxDataAgeDays` | 30 | Maximum data age for stats queries |

## Raw Score

Each article is analyzed by the AI model and assigned a score between **-1.0** (bearish) and **+1.0** (bullish), along with a confidence value (0 to 1) and a label (Positive, Neutral, Negative).

## Decay-Weighted Average

The core of the scoring system. Recent articles matter more than old ones.

Each article gets a weight based on its age:

```
weight = e^(-ln(2) / halfLifeHours * ageHours)
```

This is exponential decay with a configurable half-life (default 36 hours / 1.5 days). A 1.5-day-old article has half the influence of a brand-new one. A 3-day-old article has 25%.

The weighted score for a set of articles:

```
score = sum(weight_i * score_i) / sum(weight_i)
```

## Symbol Stats (Trending / Watchlist / Snapshots)

`ComputeSymbolStats` computes the full picture for a symbol. Used by the ingestion snapshot handler, trending fallback, and watchlist fallback.

### Current vs Previous Score

The data window is split at the midpoint:

```
window = DefaultWindowDays * 24 hours (default 336h)
midpoint = now - window/2
```

- **Current score**: decay-weighted average of articles from `midpoint` to `now`, weighted relative to `now`
- **Previous score**: decay-weighted average of articles before `midpoint`, weighted relative to `midpoint`

### Delta and Direction

```
delta = currentScore - previousScore
```

| Delta | Direction |
|-------|-----------|
| > 0 | `"up"` |
| < 0 | `"down"` |
| = 0 | `"flat"` |

## Trend (Linear Regression)

Fits a line through all (day-offset, score) pairs using ordinary least squares:

```
slope = (n * sumXY - sumX * sumY) / (n * sumX^2 - sumX^2)
```

Where X = days since the oldest article, Y = raw score.

| Slope | Direction |
|-------|-----------|
| > 0.005 | `"Improving"` |
| < -0.005 | `"Deteriorating"` |
| else | `"Stable"` |

The slope represents score change per day. A slope of 0.01 means sentiment is improving by ~0.01 per day.

## Dispersion (Weighted Standard Deviation)

Measures how much articles disagree. High dispersion = conflicting sentiment.

```
mean = weighted average of all scores
dispersion = sqrt(sum(weight_i * (score_i - mean)^2) / sum(weight_i))
```

- **0.0**: all articles agree
- **> 0.5**: strong disagreement (e.g., some very bullish, some very bearish)

## Signal Strength

Based on the total decay weight (sum of all article weights):

| Total Weight | Strength |
|-------------|----------|
| >= 3.0 | `"strong"` |
| >= 1.0 | `"moderate"` |
| < 1.0 | `"no signal"` |

A single brand-new article has weight ~1.0. Three recent articles give ~3.0. Old articles contribute less.

## Distribution

Simple percentage breakdown of article labels:

```
positivePercent = (count of Positive) / total * 100
neutralPercent  = (count of Neutral)  / total * 100
negativePercent = (count of Negative) / total * 100
```

Not decay-weighted — every article in the window counts equally for distribution.

## Sentiment Shift

Compares the current weighted score to what the score *would have been* at two historical points:

- **Vs24h**: current score minus the score computed using only articles that existed 24 hours ago (weighted relative to that point)
- **Vs7d**: same but 7 days ago

Returns `null` if no articles existed at the reference time.

## Most Impactful Article

The article with the highest `weight * |score|`. A high-confidence recent article with a strong score dominates. Used in the stats detail view.

## Where Each Metric is Used

| Metric | Trending | Watchlist | Stats Detail | Snapshot |
|--------|----------|-----------|--------------|----------|
| Score (weighted avg) | x | x | x | x |
| Previous Score / Delta | x | | | x |
| Direction | x | | | x |
| Trend | x | x | x | x |
| Dispersion | x | x | x | x |
| Signal Strength | | | x | |
| Distribution | | | x | |
| Sentiment Shift | | | x | |
| Most Impactful | | | x | |
| Article Count | x | x | x | x |

## Precomputed Snapshots

`SymbolSnapshot` caches the output of `ComputeSymbolStats` in the database. It is refreshed on every new analysis (via `RefreshSnapshotHandler`). The trending and watchlist endpoints read snapshots for instant responses, falling back to live computation only on cold start when no snapshots exist yet.
