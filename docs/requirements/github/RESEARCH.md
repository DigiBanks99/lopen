# Research: GitHub Projects v2, sub-issues, dependencies, issue types/fields and webhooks

Question (issue #32): what do these GitHub features support through the API, and what limits apply to a local, single-user factory that keeps workflow state (stage, gate status, assigned run) in SQLite and mirrors it to GitHub?

Researched 2026-11-10 against docs.github.com, the GitHub changelog and live GraphQL schema introspection (`gh api graphql`). The REST examples use API version `2026-03-10`.

## TL;DR

- **Everything Lopen needs to write is in the API.** Projects v2 items and field values, sub-issues, blocked-by/blocking dependencies, issue types and issue fields all have GraphQL mutations. Since 2026 most of them also have REST endpoints.
- **The repo owner type matters more than anything else.** `DigiBanks99/lopen` belongs to a **user** account. User accounts have these restrictions:
  - Webhooks for `projects_v2` and `projects_v2_item` are **organization-only**, so changes to a user-owned project's fields raise **no events**.
  - Fine-grained PATs and GitHub App tokens **cannot** access user-owned Projects v2. Only the org `/orgs/{org}/projectsV2` endpoints are listed for them. That leaves a classic PAT or OAuth token with the `project` scope, such as the `gh` CLI token.
  - Issue types and issue fields are **organization-only**.
- **Treat Lopen's SQLite as the writer and GitHub as the mirror, and reconcile by polling.** Webhooks can only reach a local app through a forwarder: `gh webhook forward` (dev/test only, repo/org hooks only), smee.io, or a tunnel. For issues, comments, PRs, sub-issues and dependencies, add an optional webhook path via forwarding. Project field changes made by humans can only be detected by polling for a user-owned project.
- **The rate limits are generous for one user.** Each limit applies to both REST and GraphQL:

  | Limit | Value |
  | --- | --- |
  | Primary, per hour | 5,000 requests (REST) or 5,000 points (GraphQL) |
  | Secondary, per minute | 900 REST points or 2,000 GraphQL points |
  | Content creation | ~80 requests per minute and 500 per hour |

  The content-creation cap is the one a factory will hit, through comments, sub-issues and dependencies. A `304` response to a conditional (ETag) poll does not count against the primary limit.

## Projects v2

### API surface

| Capability                                                             | GraphQL                                                                                                                       | REST                                                                                                                           |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Add an issue or PR as an item                                          | `addProjectV2ItemById` (returns the existing item if it is already there)                                                     | `POST /{orgs/{org}\|users/{username}}/projectsV2/{n}/items` (by id or by owner/repo/number)                                    |
| Add a draft issue                                                      | `addProjectV2DraftIssue`                                                                                                      | `POST .../drafts` (org)                                                                                                        |
| Set field values (text, number, date, single select, iteration)       | `updateProjectV2ItemFieldValue` (one field per call); `clearProjectV2ItemFieldValue`                                          | `PATCH .../items/{item_id}` with a `fields: [{id, value}]` array (several fields in one call; `null` clears)                  |
| Read items with field values                                           | `ProjectV2.items { fieldValues }`                                                                                             | `GET .../items?fields=...` (only the title is returned unless `fields` is given; 100 per page max)                            |
| Create fields                                                          | `createProjectV2Field`                                                                                                        | `POST .../fields` (text, number, date, single_select with options, iteration with configuration)                              |
| Update or delete fields and options                                    | `updateProjectV2Field`, `deleteProjectV2Field`                                                                                | Not available (REST has list, get and create only)                                                                            |
| Archive, reorder or delete items                                       | `archiveProjectV2Item`, `updateProjectV2ItemPosition`, `deleteProjectV2Item`                                                  | `DELETE .../items/{id}`                                                                                                        |

Notes:

- You cannot add an item and set its fields in a single call. Add it first, then update it. [using-the-api]
- `updateProjectV2ItemFieldValue` cannot set Assignees, Labels, Milestone or Repository. These belong to the issue or PR, so use `addLabelsToLabelable`, `updateIssue` and similar mutations instead. [using-the-api]
- **Option identity hazard:** `updateProjectV2Field.singleSelectOptions` *overwrites* the option list. To keep the values already set on items, you must pass each existing option's `id`. Otherwise "item field values" are cleared. Fetch the options first, then patch them. [GraphQL schema introspection: `ProjectV2SingleSelectFieldOptionInput.id`]
- You can choose iteration values from active or completed iterations. [using-the-api]
- A project allows **50 fields**, built-in ones included. [about-projects] It allows **50,000 items**, active and archived together. [changelog 2025-04-09, adding-items docs]
- GraphQL also exposes multi-select field values (`ProjectV2ItemFieldMultiSelectValue`) and issue-field values on project items (`ProjectV2ItemIssueFieldValue`, `createProjectV2IssueField`). [schema introspection; changelog 2026-07-23, 2026-08-07]

### Auth for Projects v2

| Token                                  | Org-owned project                    | **User-owned project**                                      |
| -------------------------------------- | ------------------------------------ | ----------------------------------------------------------- |
| Classic PAT or OAuth (`gh auth` token) | `project` (or `read:project`) scope  | `project` scope: **the only option**                        |
| Fine-grained PAT                       | Org permission "Projects" read/write | Not supported: no `/users/{username}/projectsV2` endpoints are listed |
| GitHub App (installation or user token) | Org permission "Projects" read/write | Not supported: no `/users/{username}/projectsV2` endpoints are listed |

Sources: [fine-grained-permissions], [app-permissions] list only `/orgs/{org}/projectsV2/...` endpoints under "Organization permissions for Projects". [using-the-api] says to use `gh auth login --scopes "project"`. The local `gh` token already has `project`, `repo` and `read:org`.

## Sub-issues

- **REST:** these endpoints sit under `/repos/{owner}/{repo}/issues/{n}/`. They take the internal issue `id`, not the issue number. [rest-sub-issues]

  | Method and path | Action |
  | --- | --- |
  | `GET .../parent` | Get the parent issue |
  | `GET .../sub_issues` | List sub-issues (100 per page) |
  | `POST .../sub_issues` | Add a sub-issue. The body is `{sub_issue_id, replace_parent?}` |
  | `DELETE .../sub_issue` | Remove a sub-issue |
  | `PATCH .../sub_issues/priority` | Reorder sub-issues with `after_id` or `before_id` |

- **GraphQL:** `addSubIssue`, `removeSubIssue`, `reprioritizeSubIssue`. [schema introspection]
- **Limits:** up to **100 sub-issues per parent** and **8 levels of nesting**. [adding-sub-issues] Sub-issues can come from other repos, but "the sub-issue must belong to the same repository owner as the parent issue." [rest-sub-issues]
- **Auth:** the repository "Issues" permission (write to add or change). This works with fine-grained PATs and GitHub Apps. [fine-grained-permissions]
- **Webhook:** the `sub_issues` event (repo, org, app) has these actions: `sub_issue_added/removed` and `parent_issue_added/removed`. [webhook-events]
- Adding and removing sub-issues is content creation. Doing it in bulk can trigger secondary rate limits and `422` "spammed" responses. [rest-sub-issues]

## Issue dependencies (blocked by / blocking)

- **REST:** these endpoints sit under `/repos/{owner}/{repo}/issues/{n}/dependencies/`. [rest-dependencies]

  | Method and path | Action |
  | --- | --- |
  | `GET .../blocked_by` | List the issues blocking this one |
  | `POST .../blocked_by` | Add a blocker. The body is `{issue_id}`, the blocker's internal id |
  | `DELETE .../blocked_by/{issue_id}` | Remove a blocker |
  | `GET .../blocking` | List the issues this one blocks |

- **GraphQL:** `addBlockedBy`, `removeBlockedBy`. [schema introspection]
- **`gh` CLI:** the dependency flags accept a comma-separated list of issue numbers or URLs. [creating-issue-dependencies]
- **Limits:** the docs give no maximum number of dependencies per issue and do not say whether dependencies can cross repos or owners. Test this if it matters.
- **Auth:** the repository "Issues" permission. [fine-grained-permissions]
- **Webhook:** the `issue_dependencies` event (repo, org, app) has these actions: `blocked_by_added/removed` and `blocking_added/removed`. [webhook-events]

## Issue types and issue fields

- **Issue types:** organization-level only, with up to **25 per org**. The defaults are task, bug and feature. [issue-types]
  - REST: `/orgs/{org}/issue-types` for CRUD and `GET /repos/{owner}/{repo}/issue-types`.
  - GraphQL: `createIssueType` and `updateIssueIssueType`.
  - The `issues` webhook has `typed` and `untyped` actions. [fine-grained-permissions, webhook-events]
- **Issue fields:** these are structured, org-defined metadata of type text, single select, number and date, with multi-select added later. They became generally available on 2026-07-02. Each org gets up to **25**, with defaults Priority, Effort, Start date and Target date. [changelog 2026-03-12, 2026-07-02]
  - REST: `/orgs/{org}/issue-fields` for CRUD, `/repos/{owner}/{repo}/issues/{n}/issue-field-values` (GET, POST, PUT and DELETE), and `issue_field_values` on issue create and update.
  - GraphQL: `setIssueFieldValue` and related mutations.
  - The `issues` webhook has `field_added` and `field_removed` actions. [rest-issue-field-values, webhook-events]
  - Writing a value needs push access. Values can be set only for fields defined by the repo's organization.
- **Implication:** issue fields are attached to the issue, can be shown as project columns, and raise *repo-level* webhooks. Projects v2 field changes raise only org-level webhooks. In an org, issue fields would therefore be the better mirror target. **For the current user-owned repo, neither issue types nor issue fields are available.** Lopen's options there are Project v2 custom fields, with no events, or labels, which raise `issues.labeled/unlabeled` repo webhooks.

## Webhooks

| Event                                                     | Where you can subscribe | Notes                                                                                     |
| --------------------------------------------------------- | ----------------------- | ----------------------------------------------------------------------------------------- |
| `issues`                                                  | repo, org, app          | Includes `labeled`, `typed`, `field_added/removed`, `closed` and others                   |
| `issue_comment`                                           | repo, org, app          | Covers issues and PRs                                                                      |
| `pull_request`, `pull_request_review`                     | repo, org, app          |                                                                                           |
| `sub_issues`, `issue_dependencies`                        | repo, org, app          | "Issues" read permission                                                                  |
| `projects_v2`, `projects_v2_item`, `projects_v2_status_update` | **org only**       | "Projects" org permission. Project webhooks are "in public preview and subject to change" |

Source: [webhook-events]. **User-owned projects raise no item or field events at all.**

### Receiving events in a local single-user app

| Option                                                | Fit                                                                                                                                                                                                                         |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Polling, with ETag conditional requests**           | Always works, and needs no public endpoint. A `304` costs no primary rate limit. Honour `x-poll-interval`. GitHub recommends webhooks over polling, but polling is the only route for a user project's field changes. [best-practices] |
| `gh webhook forward` (`cli/gh-webhook` extension)     | Forwards **repo or org** webhooks to localhost. Org hooks need `admin:org_hook`. **Testing only, not supported for production.** One forwarder per repo or org at a time (`Hook already exists`). It cannot forward App webhooks. [gh-webhook-forward] |
| smee.io or a tunnel (dev tunnels, ngrok)              | A public relay to localhost. Works for repo, org and App webhooks. GitHub's own App tutorial uses Smee for local development. It is a third-party dependency and the relay URL must be kept secret. Verify `X-Hub-Signature-256`. |
| GitHub App                                            | Gets an App-level webhook and an installation rate limit of 5,000–12,500 per hour. **It cannot touch user-owned Projects v2.** It only pays off once Lopen targets an org.                                                    |
| Events API (`/repos/{o}/{r}/events`, `/issues/events`) | Built for polling with ETags, but latency is "30s to 6h". It holds at most 300 events from the last 30 days. Not suitable for driving a workflow. [rest-events]                                                              |

Recommended pattern: SQLite stays authoritative. Lopen writes to GitHub on transitions. A periodic reconciler polls issues (`GET /repos/.../issues?since=`) and project items (paged at 100 per page), uses ETags, and adopts or flags human edits. Webhook forwarding is an optional low-latency accelerator, not a dependency.

## Rate limits

| Limit                                  | REST                                                                 | GraphQL                                                                         |
| -------------------------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| Primary (PAT or user)                  | 5,000 requests per hour                                              | 5,000 points per hour. A query costs about connections ÷ 100, at least 1        |
| Primary (App installation)             | 5,000, plus 50 per repo and per user beyond 20, capped at 12,500 (15,000 on GHEC) | Same scaling, capped at 12,500 (10,000 on GHEC)                                 |
| Secondary: concurrency                 | 100 concurrent requests, shared by REST and GraphQL                  | Shared                                                                          |
| Secondary: points per minute           | 900. A GET costs 1 and a mutation 5                                  | 2,000. A query costs 1 and a mutation 5                                         |
| Secondary: content creation            | About 80 per minute and 500 per hour. Web UI actions count too       | Same                                                                            |
| Other                                  | Send mutations serially, about 1 second apart. Honour `retry-after` and `x-ratelimit-reset` | Connections take `first` or `last` from 1 to 100. At most 500,000 nodes per query. Requests time out after 10 seconds |

Sources: [rest-rate-limits], [graphql-rate-limits], [best-practices].

## Sources

- [using-the-api] https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/using-the-api-to-manage-projects
- [rest-project-items] https://docs.github.com/en/rest/projects/items
- [rest-project-fields] https://docs.github.com/en/rest/projects/fields
- [about-projects] https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects
- [changelog 2025-04-09] https://github.blog/changelog/2025-04-09-evolving-github-issues-and-projects/
- [rest-sub-issues] https://docs.github.com/en/rest/issues/sub-issues
- [adding-sub-issues] https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/adding-sub-issues
- [rest-dependencies] https://docs.github.com/en/rest/issues/issue-dependencies
- [creating-issue-dependencies] https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-issue-dependencies
- [issue-types] https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/managing-issue-types-in-an-organization
- [rest-issue-field-values] https://docs.github.com/en/rest/issues/issue-field-values
- [changelog 2026-03-12] https://github.blog/changelog/2026-03-12-issue-fields-structured-issue-metadata-is-in-public-preview/
- [changelog 2026-07-02] https://github.blog/changelog/2026-07-02-issue-fields-are-now-generally-available/
- [changelog 2026-07-23] https://github.blog/changelog/2026-07-23-multi-select-fields-for-projects-and-issues-in-public-preview/
- [webhook-events] https://docs.github.com/en/webhooks/webhook-events-and-payloads
- [gh-webhook-forward] https://docs.github.com/en/webhooks/testing-and-troubleshooting-webhooks/using-the-github-cli-to-forward-webhooks-for-testing
- [rest-events] https://docs.github.com/en/rest/activity/events
- [fine-grained-permissions] https://docs.github.com/en/rest/authentication/permissions-required-for-fine-grained-personal-access-tokens
- [app-permissions] https://docs.github.com/en/rest/authentication/permissions-required-for-github-apps
- [rest-rate-limits] https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- [graphql-rate-limits] https://docs.github.com/en/graphql/overview/rate-limits-and-query-limits-for-the-graphql-api
- [best-practices] https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api
- Schema introspection: `gh api graphql -f query='{__type(name:"Mutation"){fields{name}}}'`, run 2026-11-10
