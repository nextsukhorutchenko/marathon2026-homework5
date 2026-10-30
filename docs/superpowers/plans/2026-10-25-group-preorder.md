# Owner group pre-order implementation plan

**Goal:** Implement the three missing owner APIs from `homework-backend.md` and connect all four owner actions to the Angular reservation workflow.
**Architecture:** Preserve Handler → Service → Repository. Use a focused owner service, existing session models/repositories and a conditional DynamoDB transaction for order/group submission. Add a lazy-loaded owner page linked from customer reservation cards.
**Spec:** `homework-backend.md`, archived lecture slides 8–18 and 28–29.
**Tech stack:** .NET 10, AWS SDK v4, xUnit/Moq, Angular 22 signals, HttpClient, Jasmine/Karma. Reuse existing dependencies. This extracted project has no Git repository; preserve a before snapshot and do not create commits or branches.

## Decisions

- GET sessions includes existing Owner sessions, with the correct participant type; monetary totals come from server-side dish prices.
- GET owner-session returns `guestSessionId` and additive `groupReservationId`/`status` metadata so the UI can load an owner group without a public invite, which can be closed/full. GET remains read-only.
- Provision a deterministic Owner session when creating a group; an explicit authenticated preparation POST repairs legacy groups and initializes only missing orders. GET remains read-only. Guest capacity counts exclude Owner sessions.
- Submit validates the reservation/group relationship and owner before reading participant data. Submitted guest carts are added to the existing personal pre-order, preserving Owner dishes; same dishes are combined. Conditional atomic writes check group state, original order data, reservation ownership/status and source session data. Repeats/conflicts return 409. Missing submitted data/order returns 400, missing reservation/group returns 404.
- Preserve the personal contribution as `groupOwnerDishes` so post-submit snapshots do not double count. Conditional individual-cart writes prevent stale edits from overwriting a consolidated order. Cancelled prepared orders can be recreated conditionally.
- Owner cannot remove their own Owner session, and group mutation after submission/closure is rejected. Use camelCase response JSON for the owner contracts, including existing DELETE.
- The screen displays all participant totals and a separate submitted total, supports refresh/remove/submit, handles loading/empty/error/success states, and links back to reservations. Preserve existing individual pre-order flow.
- Update Syndicate API Gateway resources with Cognito authorization, CORS and required IAM permissions. Do not deploy AWS or publish frontend as part of local implementation.

## Tasks

- [x] Add failing backend tests for owner isolation, snapshots, session lookup, draft exclusion, aggregation, invalid requests, state changes and transactional conflicts.
- [x] Implement owner service, transaction repository and HTTP handlers; wire routes and deployment configuration; provision Owner sessions without consuming guest capacity.
- [x] Add failing frontend HTTP/interceptor and owner-page interaction tests; implement typed service, lazy owner page and reservation entry point.
- [x] Run backend suite, TypeScript app/spec checks, frontend suite, production build and browser interaction checks. Review against original snapshots and document pre-existing failures and deployment limits.

## Results

184 backend tests pass. 20 targeted frontend tests pass. Application and spec TypeScript checks pass; production build passes with 790.09 kB initial bundle (existing 500 kB warning, below the 1 MB error limit). Full frontend suite: 213 pass, 46 fail; unchanged baseline: 195 pass, 47 fail. No newly failing tests. Local browser tests pass with real C# handlers/services and in-memory AWS boundaries, including ownership, legacy preparation, consolidation and mobile layout. AWS deployment/integration is not performed.

## Review focus

Owner token must reach `/guest/groups/.../sessions`; guest tokens remain untouched. Mismatched reservation/group IDs cannot submit. Failed transactions cannot leave a half-submitted group/order. Only submitted sessions reach the consolidated order. Loading/failed requests must never be displayed as a successful empty group.
