# Authorization and integration verification

This pass checks the existing frontend APIs and fixes confirmed ownership, authorization, and credential-disclosure defects. It adds no product endpoints or database migration. The current domain calls runs **Scrapes** and exposes them under `/api/v1/scrapes`; it has no separate `/runs` resource.

## Coverage

| Area | Verification |
| --- | --- |
| Client authentication | Anonymous, deleted-user, and stale-session rejection across 27 protected route/method combinations; valid user responses; cookie-only identity contracts. |
| Auth lifecycle | User/Admin/SuperAdmin login, `/me`, refresh rotation/replay rejection, expired access JWT recovery using a valid refresh cookie, missing/unknown/expired/revoked refresh rejection, logout and cookie clearing. |
| Role boundaries | Existing admin users/bots/subscriptions suites cover Admin and SuperAdmin success, regular User/other-role rejection, and stale/inactive admin checks. New tests also reject regular User principals on admin `/me` and logout. |
| Connected accounts | Owned create/update/delete; foreign read/update/delete rejection; server-owned creation despite a supplied `userId`; safe list/detail fields and masked cookies; owner-scoped eligible lists. |
| Bots | Shared active catalog with personalized subscription state, own subscribed list, and ownership-required detail rejection. Existing admin lifecycle/DTO/module-code tests remain included. |
| Groups | Shared-bot list isolation, foreign detail/update/delete rejection without mutation, owned CRUD, and handler/validator regressions. |
| Runs/Scrapes | Owner-scoped list/detail/cancel, only Running-to-Cancelled transition, end timestamp, scoped account release, and rejection before dispatch of foreign/mismatched/unassigned/disabled/expired resources. |
| Eligibility and messaging | Only Active-status accounts are offered/accepted; Busy, CoolingDown, AccountFlagged and Banned accounts are excluded/rejected. Disabled, expired-cookie and missing-cookie accounts are excluded from eligibility. Messaging requires current UserBot ownership and matching account/lead bot ownership. |
| Leads and details | Existing `LeadDetailsApiTests` cover metadata/DTO fields, shared-bot isolation, missing/foreign lead equivalence, assignment removal and nullable history. New tests include list/preview auth and subscription boundaries, plus foreign lead rejection during messaging. |
| Settings and activity logs | Existing suites verify owner-scoped reads/writes, ignored supplied user IDs, safe/masked credentials, log correlation/filter/pagination isolation, stack-trace exclusion, and role restrictions. New route tests exercise valid/deleted/stale/anonymous sessions. |
| Admin subscriptions | Existing suites verify duplicate assignment conflicts, relational uniqueness, filters/pagination, removal and client ownership effects, invalid related IDs, and safe DTOs. |
| Authentication logs | Regression tests capture application logs and assert that refresh and rejected/current session credential values are absent. |

New HTTP coverage lives in `AuthAuthorizationApiTests`, `ClientAuthorizationApiTests`, and `RunAuthorizationApiTests`, using `AuthorizationApiTestBase`. Each test has an isolated SQLite database and the production controllers, cookie JWT authentication, session middleware, MediatR validation/handlers, encryption, password verification, and EF mappings. Outbound worker/network integrations are mocked. The SQLite adapter now provides an insert default for SQL Server's generated `Job.RowVersion`; these tests do not claim to verify SQL Server rowversion concurrency behavior.

Existing target-group handler/validator tests now supply an explicit authenticated owner, with four new foreign-owner/empty-identity regressions. Existing messaging handler unit tests explicitly configure the successful subscription prerequisite for their authorized scenarios.

## Confirmed defects fixed

1. **Target-group write ownership:** PUT and DELETE previously looked up only the group ID. They now carry the claims-derived user ID through CQRS and scope the database lookup by ID plus owner. Foreign and missing groups share the existing 404 response, and an empty command identity fails validation.
2. **Run account/group association:** Scrape creation previously accepted disabled accounts and accounts/groups from another bot owned by the caller. Account lookup now requires matching owner, requested bot and `IsActive`; optional group lookup also requires the requested bot. Rejected requests create no run and make no outbound call.
3. **Messaging subscription and association:** The UserBot check was commented out and account lookup did not match the requested bot. The existing ownership service is enforced again, and accounts must belong to the requested bot. Foreign/wrong-bot leads remain rejected by the existing lead scope.
4. **Cancellation side effects:** Cancelling an owned scrape with an inconsistent account link could release another user's Busy account. The account release lookup now also checks the authenticated owner and scrape bot.
5. **Admin session role checks:** Admin `/me` and logout previously used authentication alone; regular-user logout returned success. Both now use the existing `Admin,SuperAdmin` authorization convention. Anonymous requests receive 401 and authenticated regular users receive 403.
6. **Authentication credentials in logs:** User refresh logged the raw refresh token; session mismatch logged both database and claimed session tokens. Those values have been removed from log messages while retaining diagnostic user IDs/status.

## Verified response behavior

Success envelopes retain `isSuccess`, `message`, `errorType`, and `data`. Auth identities contain only `userId`, `email`, `name`, and `role`; credentials are delivered in HTTP-only cookies. Logout returns `ApiResponse<string>` with `data: null`, rather than the older documentation's boolean example.

`ErrorType.ValidationError` maps to **422**, including non-Active accounts, expired cookies, invalid/foreign messaging leads, and cancellation of a non-Running scrape. FluentValidation/model-binding errors remain **400**. Ownership failures remain **404**; authentication challenges/session failures are **401**, and role failures are **403**. Middleware challenge/forbid responses, missing-refresh responses and session failures preserve their existing response shapes; the tests do not force all of them into `ApiErrorResponse`.

The actual eligible-account route is `/api/v1/connectedaccounts/eligible/{botId}`. The older contract's alternative query-only route did not exist and has been removed from the documentation.

## Existing behavior requiring a separate policy decision

- **Admin logout is stateless for access JWTs.** Logout revokes the supplied admin refresh token and clears cookies, but an already-issued access JWT remains valid until expiry. This behavior is explicitly tested and preserved; immediate access-token revocation would require a separate authentication design change. Existing inactive-admin checks still protect admin `/me` and management operations.
- **Diagnostic metadata is public.** `ReportsController` has no authorization attribute and exposes controller/type/property-name reports; Swagger is also enabled outside Development by the current startup code. These expose schema metadata, not stored user credentials. No access-policy change is made because the intended policy is not specified by the current contract. `TestAuthController` has no executable actions (its sample methods are commented out).

## Validation commands

```powershell
dotnet build MultiBotSaaS.slnx --no-restore --artifacts-path artifacts/admin-users-tests --disable-build-servers -m:1
dotnet test MultiBotSaaS.slnx --no-build --no-restore --artifacts-path artifacts/admin-users-tests --disable-build-servers -m:1
```

Final verification on 2026-10-15:

| Suite | Passed | Failed / skipped |
| --- | ---: | ---: |
| API integration | 537 | 0 / 0 |
| Application unit | 498 | 0 / 0 |
| Domain unit | 95 | 0 / 0 |
| Infrastructure unit | 116 | 0 / 0 |
| **Total** | **1,246** | **0 / 0** |

This adds **155 HTTP integration cases and 4 unit cases** to the previous 1,087 tests. The complete solution build succeeded with **0 errors** and **7 cached NU1900 warnings**. No test/build failures remain. The existing admin JWT/logout and public diagnostic metadata policy observations above remain explicit follow-up decisions.

The isolated artifact path avoids locking a running development API's normal output. SQL Server deployment, live worker/webhook calls, and browser cookie policy are not exercised by this in-process suite. Cached NU1900 vulnerability-feed warnings reflect unavailable NuGet metadata, not failed tests; existing unrelated nullability warnings are retained.
