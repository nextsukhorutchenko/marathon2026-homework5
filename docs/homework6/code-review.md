# Independent final code review

Reviewer: fresh-context gpt-6-astra, read-only. No code or git state mutations by reviewer.

Critical: none. Important: one P2. Minor: none confirmed.

## P2: stale order after successful Submit

The backend consolidates current submitted guest carts. A guest submitting after the last UI poll could be included in the real order while the read-only screen stayed at the earlier $540 instead of $760. The page previously discarded the POST response and stopped polling without reconciling it.

Resolution: retain the authoritative dishId/quantity response. If it differs from the displayed consolidation, read sessions once silently while keeping Submitted locked. Hide the stale total until reconciliation succeeds. A failed final read leaves a visible explanation and a manual Refresh retry; it never resubmits. An already matching order does not issue a redundant read. Regular background polling remains stopped after Submit.

Regression: `group-order-page.component.submit-race.spec.ts` has three tests (latest guest included, failed final read with retry, slow final read without stale final total). All three failed before the fix, recorded in submit-reconciliation-red.log. Green suite result is recorded in feature-regressions.log after the fix.

## Considered and left outside this change

- Existing shared-modal focus management: no shared-component rewrite; this feature uses the established modal and semantic controls.
- Existing snapshot-only reservation route parameter handling: no in-screen navigation to a second reservation was introduced; normal dashboard navigation destroys the screen. Existing route reuse behavior is unchanged.
- The 46 unrelated baseline frontend failures are reported and compared by exact test name.
- Backend guest-capacity counting and atomic consistency across separate status/sessions reads require separate backend changes. Frontend guards overlaps and preserves server errors without claiming atomic backend snapshots.

No second reviewer dispatch: the fix is verified by regression tests and final suite/build checks.
