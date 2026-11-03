# Level 3: deployed application and dish search

User request: deploy the supplied restaurant application and implement an additional feature. User selected dish search and explicitly delegated implementation decisions on 02.11.2026.

Build on the current .NET/Angular checkout, which already contains the completed owner group-order and automation homework. The supplied marathon2026-main.zip is reference material; do not overwrite completed work with its starter implementation.

## Feature

Expose the existing case-insensitive API name filter in Menu. A labelled search field, Search button (also Enter), and Clear search button control the `q` URL parameter. Trim surrounding whitespace; blank text removes the filter. Preserve category, sort and reservation/pre-order parameters. Opening, reloading or navigating Back to a URL restores its search.

Search is explicitly submitted, avoiding a network request for each keystroke. Changing filters cancels the previous in-flight request so stale responses cannot replace the current results. Subscriptions end when leaving the page. Show loading, a retryable error, empty menu and no matches; never present an old result list as the new search. Keep keyboard and mobile access and existing dish/pre-order interactions.

## Approaches

Selected by user: dish search using the already-supported `GET /dishes?name=...`. Alternative offered: browser-local favourites, which would not sync across devices. No backend schema or new dependency is needed for the selected feature.

## Deployment

Reuse the existing AWS backend, private frontend S3 bucket and Terraform state. Check credentials, then inspect the saved Terraform plan before applying only the missing CloudFront distribution and its S3 policy. Keep S3 Block Public Access enabled. If the account still denies CloudFront, document the exact failure and use a public HTTPS static-hosting alternative, with the existing real AWS API. No paid upgrades, domain purchases, or unrelated infrastructure changes.

## Completion evidence

Relevant Jasmine tests must fail before implementation and pass afterwards. Run app/spec TypeScript checks, production build, homework regression tests and the complete frontend suite, recording existing failures separately. Verify search, combined filters, clear, URL reload, keyboard submit and mobile layout in a real browser. Verify the deployed menu against the real API and preserve the public URL, build hash and deployment result in documentation. No credentials or test-user secrets in evidence.
