# Owner group pre-order

Homework #5 attachment reconciliation on 2026-10-28: the original file supplied from Telegram is byte-identical to `homework-backend.md`; all three levels and error conditions are covered. The backend suite passed again on 2026-10-30 (184/184). The full live-AWS run passed on 2026-10-28: 49/49 HTTP checks and 13/13 additional assertions. All 16 temporary records and 2 Cognito users were verified removed. See [the requirement checklist](homework5-checklist.md) and [request/response examples](homework5/evidence-2026-10-28/EXAMPLES.md).

Frontend publication was retried on 2026-10-30 using a reviewed Terraform plan limited to the CloudFront distribution and its S3 policy. AWS rejected CreateDistributionWithTags with 403 (request 6a9bd411-c107-4e99-9cd9-5f42aa119830); no public frontend URL exists. AWS Support replied on October 28 that CloudFront access could not be approved at this time, citing insufficient account usage and billing history, and closed the case. See [the homework delivery notes](homework5/README.md).

Implemented on 2026-10-25 against `homework-backend.md` and the archived .NET lecture. Backend/API changes are deployed to AWS and verified with real Cognito and DynamoDB. Frontend 1.1.2 is uploaded to private S3; public HTTPS publication remains blocked by AWS account verification for CloudFront.

## User flow

1. Sign in as a customer and open Reservations.
2. For a Reserved reservation with at least two people, choose **Group pre-order**.
3. For a legacy group with no Owner session, choose **Prepare group pre-order**. This explicit POST prepares missing records; the GET endpoints remain read-only.
4. Review participant dishes, prices, quantities, submission state and totals. Your existing personal pre-order is included; draft guest carts are excluded from the amount to send.
5. Remove guests with confirmation, or confirm the consolidated submission. The screen refreshes from the DELETE result, rather than estimating totals on the client.
6. Successful submission locks group mutations. The existing personal-cart APIs also reject changing a consolidated group order.

The new screen is lazy-loaded at `/reservations/:reservationId/group-pre-order`. Owner session metadata supplies the group ID without fetching a public invite, which may be full or closed. Customer-facing UI uses the application's existing English copy style.

## Implementation

`OwnerGroupOrderHandler` handles the owner routes; `OwnerGroupOrderService` validates ownership against `Reservation.CustomerId`, builds snapshots and performs consolidation; `GroupOrderTransactionRepository` owns atomic writes. Existing guest-token APIs keep their separate authentication flow. Owner `/guest/groups/.../sessions` requests receive the signed-in customer's Cognito ID token through the interceptor.

Source session IDs are discovered through the existing GSI and then re-read consistently by primary key. Final submission condition-checks the submitted carts, the original order contents/status and the reservation owner/status, while changing the order and group together. Deletion checks that the group remains Open in the same transaction as deleting the participant. Conditional individual-cart writes prevent a stale or in-flight personal edit from overwriting a completed group submission.

The original Owner contribution is stored in the internal `groupOwnerDishes` order attribute so a post-submit snapshot does not attribute guests' dishes to the Owner or count them twice. It is not exposed as an extra public order field. A transaction supports at most 97 submitted sessions after its order/group/reservation actions; larger groups are rejected before writing.

New groups create an Owner session. Owner sessions do not consume another guest seat. The explicit preparation endpoint also repairs older groups and prepares an empty order when needed. A direct submit with no order still returns 400. The normal 30-minute cutoff is preserved for owner mutations.

## Deployment

Current backend: `https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop`. Deployment evidence is in `D:/epamMarafone/output/group-order-deployment/REPORT.md`. The real group-order flow passed through the UI against AWS, using local delivery of the same production files verified in S3 because CloudFront is blocked. Test data and test users are removed after verification.

Deploy the backend **including** `deployment_resources.json`: four added Gateway methods use `cognito_authorizer`, Lambda proxy integration and CORS. The IAM policy gains `dynamodb:ConditionCheckItem`; existing PutItem/DeleteItem permissions cover the transaction writes. Rebuild/redeploy the frontend with the actual deployed backend URL. Earlier local verification used `https://example.com/develop`; the frontend subsequently uploaded to S3 was rebuilt with the real AWS API URL above.

No manual table schema migration is needed for DynamoDB's optional `groupOwnerDishes` field. Preparation creates missing legacy records through authenticated commands instead of silently writing during GET.

## Validation evidence

Results and logs are in `D:/epamMarafone/output/group-order-implementation`. Source snapshots before this task are under its `before` directory because the supplied project is not a Git checkout.

- Backend suite and transaction request serialization tests.
- TypeScript checks for application and test sources; production build and placeholder check.
- HTTP, interceptor, owner-page and reservation-entry tests in Chrome.
- Browser flow against the real C# handler/service with in-memory AWS boundaries, including personal-item preservation, guest removal, group submission, legacy preparation, ownership rejection and 390px layout.
- The complete frontend suite has pre-existing failures. A separate run against the unchanged source snapshot establishes the baseline; the verification report lists any new failures explicitly.

The original local browser fixtures used synthetic tokens. A subsequent live AWS run verified real Cognito/API Gateway/IAM/DynamoDB, atomic submission, ownership, mutation locks and CORS. Public CloudFront delivery and public SPA routes remain unverified until AWS Support clears the account restriction (case 179281677200923).
