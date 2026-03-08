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
