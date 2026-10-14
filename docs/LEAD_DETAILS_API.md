# Lead details API

`GET /api/v1/leads/{leadId}` requires the existing `[Authorize]` cookie JWT authentication and session checks. The ID is a positive Int64. This endpoint adds no query parameters or request body.

The existing list (`GET /api/v1/leads/leads/{botId}`) and messaging preview (`GET /api/v1/leads/bot/messaging-preview/{botId}`) routes remain unchanged.

## Response

Success returns `200` with `ApiResponse<LeadDetailsDto>` and message `Lead retrieved successfully.` The DTO exposes only stored lead fields:

| JSON field | Type | Source |
| --- | --- | --- |
| `id` | Int64 | `Lead.Id` |
| `botId` | nullable integer | `Lead.BotId` |
| `groupId` | nullable integer | `Lead.GroupId` |
| `accountId` | nullable integer | `Lead.AccountId` |
| `scrapeId` | nullable integer | `Lead.ScrapeId` |
| `externalId` | string | `Lead.ExternalId` |
| `profileName` | string | `Lead.ProfileName` |
| `profileUrl` | string | `Lead.ProfileUrl` |
| `aiMessage` | nullable string | `Lead.AiMessage` |
| `status` | string | `Lead.Status`, unchanged |
| `createdAt` | datetime | `Lead.CreatedAt` |
| `processedAt` | nullable datetime | `Lead.ProcessedAt` |
| `metadataJson` | nullable string | `Lead.Detail.MetaDataJson` |

Metadata is returned as the stored JSON **string**, not a parsed object. A missing detail maps to null; the existing split-table metadata column defaults to `"{}"`. Nullable fields retain their stored nulls. There is no `UpdatedAt` property in the current model, so no update timestamp is fabricated. User IDs, EF navigation objects and related account/security fields are excluded.

## Ownership and errors

The controller derives the user ID from `ClaimTypes.NameIdentifier`; client-supplied `userId` or `botId` query values cannot change ownership. The MediatR handler uses `IAppDbContext`, an untracked projection, and the existing validation pipeline.

The query filters by both lead ID and authenticated user ID. When the lead has a bot ID, it also requires `IUserBotService.CheckOwnershipAsync` against the current `UserBots` relationship. Sharing a bot does not grant access to another user's leads, and removing an assignment revokes access to leads still linked to that bot. A historical lead with a null bot ID remains readable by its recorded owner, consistent with the model's nullable/SetNull bot relationship.

Missing, foreign-owned, and unassigned-bot leads all return `404` with the same `ApiErrorResponse` message, `Lead not found.` The error includes the standard status, request path, method, and trace ID. Unauthenticated/invalid sessions return `401`. Nonpositive IDs return `400` through FluentValidation; nonnumeric or out-of-range Int64 segments do not match the route (`404`).

## Verification

`LeadDetailsApiTests` exercises production controllers, JWT cookies, session validation, MediatR handlers/validators, and relational SQLite with the existing EF mappings. It covers owned retrieval and metadata, DTO field restrictions, nullable fields, shared-bot isolation, missing IDs, client ownership spoofing, assignment removal, mismatched bot links, historical null-bot leads, anonymous/invalid/stale/deactivated sessions, ID validation, and the unchanged list/preview routes. No schema, worker write behavior, or export behavior changes are required.
