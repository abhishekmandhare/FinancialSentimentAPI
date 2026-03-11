# Decision Log

This document records significant incidents, root cause analyses, and architectural decisions for the Financial Sentiment API project. Check here before debugging any issue -- it may already be documented.

---

## Incident 1: Missing Designer.cs causing migration crash

**Category:** deployment
**Related PRs:** #27, #38
**Date discovered:** 2026-03

**Symptoms:**
- Container entered a restart loop after deployment
- Postgres error: `42P01: relation "TrackedSymbols" does not exist`

**Root cause:**
- EF Core requires a `Designer.cs` file with the `[Migration]` attribute to discover migrations at runtime
- Without it, `MigrateAsync()` silently skipped the migration, leaving the database without the expected tables
- The background worker then queried the non-existent `TrackedSymbols` table
- `BackgroundServiceExceptionBehavior.StopHost` (the .NET default) killed the entire process on the unhandled exception

**Fix:**
- Manually created the missing `Designer.cs` file with the correct `[Migration]` attribute and model snapshot reference

**Lesson:**
- Always verify that EF Core migration output includes all three files: the migration class, the Designer.cs, and the model snapshot update
- If a migration appears to run but tables are missing, check for a missing Designer.cs first

---

## Incident 2: CORS blocking browser requests to Yahoo Finance

**Category:** integration
**Related PR:** #40

**Symptoms:**
- Dashboard sparklines and prices failed to load
- Browser console showed CORS errors when fetching Yahoo Finance data

**Root cause:**
- The frontend was making direct browser requests to Yahoo Finance
- Browsers enforce CORS policy; Yahoo Finance does not send `Access-Control-Allow-Origin` headers
- Server-side requests are not subject to CORS restrictions

**Fix:**
- Added a server-side proxy via `PriceProxyController` that fetches Yahoo Finance data on behalf of the frontend

**Lesson:**
- Third-party APIs without CORS headers must be accessed through a server-side proxy when consumed from a browser

---

## Incident 3: Yahoo Finance 502 without User-Agent header

**Category:** integration
**Related PR:** #41

**Symptoms:**
- The newly added price proxy returned 502 errors

**Root cause:**
- Yahoo Finance blocks HTTP requests that do not include a `User-Agent` header
- The default .NET `HttpClient` does not send a `User-Agent` by default

**Fix:**
- Added a `User-Agent` header to the named `HttpClient` registration for the Yahoo Finance service

**Lesson:**
- When integrating with third-party APIs, always set a realistic `User-Agent` header -- many services reject or block requests without one

---

## Incident 4: Rate limiting duplicate policy names in tests

**Category:** testing
**Related PR:** #13

**Symptoms:**
- `AddFixedWindowLimiter` threw a duplicate policy name error during test runs

**Root cause:**
- Multiple test classes were sharing a single `WebApplicationFactory` instance
- Each test class re-registered the same rate limiting policy, causing a duplicate name conflict

**Fix:**
- Gave each test class its own `WebApplicationFactory` instance to isolate policy registrations

**Lesson:**
- Integration test classes that modify service registrations should use isolated `WebApplicationFactory` instances to avoid shared state conflicts

---

## Incident 5: BackgroundServiceExceptionBehavior.StopHost

**Category:** deployment

**Symptoms:**
- An unhandled exception in a background worker crashed the entire API host process

**Root cause:**
- .NET's default `BackgroundServiceExceptionBehavior` is `StopHost`, meaning any unhandled exception in a `BackgroundService` terminates the application
- The background worker did not have adequate error handling (try/catch around its main loop)

**Fix:**
- Need proper error handling (try/catch with logging) in all background service `ExecuteAsync` methods
- Consider setting `BackgroundServiceExceptionBehavior.Ignore` where appropriate, combined with structured error logging

**Lesson:**
- All `BackgroundService` implementations must wrap their work loops in try/catch blocks
- Decide explicitly whether a background service failure should be fatal or recoverable

---

## Incident 6: Crypto symbols never appearing on dashboard

**Category:** integration
**Related PR(s):** #73, #77
**Date discovered:** 2026-03

**Symptoms:**
- No crypto symbols (BTC-USD, ETH-USD, etc.) appeared in the trending dashboard despite being seeded in the TrackedSymbols DB table
- Stock symbols worked fine

**Root cause (multi-layered):**
1. **News source queries (PR #73):** Reddit ingestion only searched `/r/stocks` and Google News appended "stock news" — neither effective for crypto. Fixed by searching `/r/cryptocurrency` for crypto symbols and using "crypto news" suffix.
2. **Relevance filter (PR #77):** Even after fixing news sources, `ArticleRelevanceFilter` silently dropped all crypto articles because:
   - The `FinancialKeywords` list had zero crypto terms — articles about "Bitcoin surges" or "Ethereum staking" matched no keywords
   - Symbol matching required exact whole-word `btc-usd` in article text, but titles say "BTC" or "Bitcoin"
3. **Stale Docker image on TrueNAS:** The deployed container was not updated after merging fixes — `docker pull` + restart required
4. **Search query used full Yahoo ticker (PR #78):** Even after all above fixes, Reddit and Google News searches used `BTC-USD` as the search term — nobody writes "BTC-USD" in Reddit posts or news headlines. Added `StockSymbol.BaseTicker` property to extract `BTC` from `BTC-USD` for search queries.
5. **Reddit rate limiting (PR #78):** With 95 tracked symbols (15 crypto × 2 subreddits + stocks), Reddit aggressively rate-limited the rapid-fire unauthenticated RSS requests, returning 429 errors. Added inter-request delays (2s between subreddits, 3s between symbols).

**Fix:**
- PR #73: Crypto-aware subreddit selection and Google News query suffix
- PR #77: Added 20 crypto keywords (bitcoin, ethereum, blockchain, defi, mining, halving, etc.) and base-ticker extraction (`BTC` from `BTC-USD`) in relevance filter
- PR #78: Search by base ticker (`BTC` not `BTC-USD`) in Reddit and Google News queries + rate-limit delays
- Deployed latest image to TrueNAS

**Lesson:**
- When adding new asset classes (crypto, forex, commodities), audit the entire pipeline end-to-end: tracked symbols → news sources → relevance filter → AI analysis → display
- The relevance filter is a silent gatekeeper — articles it drops produce no errors or warnings at INFO level. Check DEBUG logs if articles seem to vanish
- Always pull the latest Docker image after merging fixes — GHCR builds on push to main but TrueNAS doesn't auto-update
- Search queries must use human-friendly terms, not internal ticker formats — people write "BTC" and "Bitcoin", not "BTC-USD"
- Unauthenticated RSS endpoints (especially Reddit) rate-limit aggressively — always add delays between requests when processing many symbols

---

## Incident 7: Admin dashboard latency always showing 0

**Category:** deployment
**Related PR(s):** #72
**Date discovered:** 2026-03

**Symptoms:**
- Admin stats page showed "Average Latency: 0s" for all analyses

**Root cause:**
- PR #72 added the `DurationMs` column (nullable bigint) to `SentimentAnalysis`, but existing rows all have `DurationMs = NULL`
- `SystemStatsRepository.GetAverageAnalysisLatencySecondsAsync` correctly queries only non-null rows, but there were none
- Additionally, the TrueNAS container was running a pre-PR#72 image where the column didn't exist

**Fix:**
- Deploy latest image so new analyses record `DurationMs` via `Stopwatch` in `AnalyzeSentimentCommandHandler`
- Latency stats will self-populate as new data flows through — no backfill needed

**Lesson:**
- When adding nullable metrics columns, the admin dashboard will show zero/empty until new data accumulates — consider showing "No data yet" instead of "0" for better UX
- Verify the deployed image version matches the expected code version after merging

---

## Template for future entries

```
## Incident N: <short title>

**Category:** <deployment | integration | testing | architecture | performance>
**Related PR(s):** #XX
**Date discovered:** YYYY-MM

**Symptoms:**
- <what was observed>

**Root cause:**
- <why it happened>

**Fix:**
- <what was done to resolve it>

**Lesson:**
- <what to check or do differently in the future>
```
