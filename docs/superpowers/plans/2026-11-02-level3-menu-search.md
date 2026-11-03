# Level 3 Menu Search Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. User delegated implementation decisions; execute inline.

**Goal:** Publish the restaurant application with URL-backed dish search.

**Architecture:** Extend the existing Menu page and DishesService contract; keep API filtering on the real AWS backend. Reuse existing infrastructure and publish the verified production build.

**Tech Stack:** Angular 22, TypeScript, RxJS, Jasmine/Karma, AWS CLI/Terraform.

**Spec:** ../specs/2026-11-02-level3-menu-search-design.md

## Global Constraints

- No new npm dependencies; preserve completed homework and real authentication.
- Reuse current checkout; no destructive archive import or unrelated refactors.
- No paid upgrades; keep private S3 and existing API configuration.
- User delegated execution details. Current checkout is clean main; leave reviewable uncommitted changes until publication needs are concrete, following the existing repository rule.

## Review Focus

- An older slow search cannot replace newer results.
- Search/clear preserve pre-order reservation parameters and selected filters.
- Failed requests leave a usable retry and do not terminate subsequent searches.
- Back navigation restores the query; leaving the page cancels requests/subscriptions.
- Empty and special-character queries use correct HTTP encoding.

## Task 1: API-backed search and states

Files: `frontend/restaurant-frontend-app/src/app/pages/menu-page/menu-page.component.{ts,html,scss,spec.ts}`; add `services/dishes/dishes.service.spec.ts`; update package version.

- [x] Add HTTP-boundary tests using the real service and HttpTestingController: restored `q` with category/sort, form trim/merge, clear, loading/empty/error/retry, cancellation and teardown, ordinary dish detail interaction and preserved pre-order context. Direct Pre-Order click regression is a documented deferred minor.
- [x] Run targeted Jasmine tests and preserve the expected RED failures.
- [x] Add labelled search form and signal state; route subscription loads the selected name/category/sort; cancel previous requests and clean up subscriptions with DestroyRef.
- [x] Run targeted tests to GREEN; format only changed files.

## Task 2: Verify and deploy

Files: deployment configuration only if required; `docs/level3/README.md`; root README.

- [x] Run app/spec `tsc --noEmit`, production build with real BACKEND_URL, homework tests and full frontend suite. Record existing full-suite failures by name.
- [x] Verify search flows, URL reload, error recovery and 375px layout in a real browser.
- [x] Renew AWS CLI session via user-completed login; inspect infrastructure plan; publish only scoped frontend resources. If CloudFront remains blocked, use documented HTTPS static hosting fallback.
- [x] Verify deployment/public menu, archive build identity and test results; update README with the public URL and reproducible steps.
- [x] Review complete diff and report remaining limitations accurately.
