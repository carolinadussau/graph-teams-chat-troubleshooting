# graph-teams-chat-troubleshooting

## Goal
Troubleshoot and implement a Microsoft Graph approach to retrieve Microsoft Teams group chat messages and attempt to export downloadable file attachments.

## Scope
- C# console app (.NET 8)
- Microsoft Graph SDK v5
- App-only authentication using a certificate (.pfx) via MSAL (Confidential Client)
- Chat message pagination (fetch beyond the default page size)
- Attachment inspection (contentType-driven logic)

## Background / Why this repo exists
A PowerShell-first approach was evaluated initially, but practical certificate-based auth and SDK compatibility constraints pushed the implementation toward a C#/.NET solution.

This repo focuses on engineering process: validating assumptions, debugging SDK behavior, and instrumenting the code to confirm what Graph returns versus what Teams UI shows.

## Key learnings
- PowerShell module limitations can block certificate-based Graph flows depending on environment/module constraints.
- Teams “attachments” are not always downloadable files; many are references/metadata rather than direct file payloads.
- Downloadable attachments typically appear with a Teams-specific attachment content type:
  `application/vnd.microsoft.teams.file.download.info`
- Pagination is required to retrieve full chat history beyond the initial response.

## Results observed in testing (sanitized)
- Successfully authenticated (certificate-based, app-only)
- Successfully retrieved chat messages across multiple pages (example run: 462 messages)
- No downloadable attachments were found in the tested chat (Attachments folder remained empty)
- This outcome matched the returned attachment content types (non-downloadable references/metadata)

## How it works (high level)
1. Load certificate (.pfx) locally
2. Acquire token for `https://graph.microsoft.com/.default`
3. Use Graph SDK to call `/chats/{chatId}/messages`
4. Follow `@odata.nextLink` to pull all pages
5. Inspect `message.Attachments`
6. If `contentType` indicates downloadable file info, parse `downloadUrl` and download bytes

## Configuration
This project uses a template config file:
- `src/TeamsChatAttachmentExport/appsettings.template.json`

Create your own local `appsettings.json` (not committed) and populate:
- TenantId
- ClientId
- CertificatePath (or certificate store reference)
- CertificatePassword (if used)
- ChatId

## Security notes
Do not commit tenant/client IDs, chat IDs, certificate paths, certificate passwords, or exported chat data to a public repository.
