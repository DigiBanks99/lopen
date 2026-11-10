# Research: Claude Agent SDK auth and billing for a personal factory

Ticket: #26 (child of #11). Researched 2026-11-10 against Anthropic's live docs and terms. Help-center and docs pages change often; re-check the sources before you rely on a date-sensitive point.

## Question

How can the Claude Agent SDK authenticate and be billed (API key, Claude subscription, Bedrock/Vertex/Foundry), what do Anthropic's terms allow for a single-user, locally run, partly unattended factory, and how is cost reported on each route?

## Answer in brief

- **The SDK supports four credential families.** These are an Anthropic API key, a cloud provider (Bedrock, Google Cloud's Agent Platform/Vertex, Microsoft Foundry, Claude Platform on AWS), a gateway bearer token, and a Claude subscription OAuth login (`/login` or `CLAUDE_CODE_OAUTH_TOKEN`).
- **Anthropic's sanctioned route for "developers building products" on the SDK is an API key or a cloud provider.** Third-party developers may not offer claude.ai login or subscription rate limits in their products.
- **A user's own subscription is still allowed to drive the SDK for that user's own use.** It draws from the plan's limits, and Anthropic says advertised plan limits assume "ordinary, individual usage of Claude Code and the Agent SDK".
- **`total_cost_usd` is always a client-side estimate at list price.** It is not a bill on any route, and on a subscription it is notional.
- **Recommendation (interpretation):** default Lopen to an API key, funded by the $100/month API credits included with Max if the user has Max. Allow subscription auth only as "use whatever the user's own Claude Code login is", and never have Lopen collect, store, or broker the OAuth token itself.

## Findings: what Anthropic states

### 1. Credential types and precedence

The Agent SDK runs the Claude Code binary, so it uses the CLI's credential resolution. `apiKeyHelper`, `ANTHROPIC_API_KEY` and `ANTHROPIC_AUTH_TOKEN` "apply to the CLI and the surfaces that wrap it, including ... the Agent SDK" ([Authentication](https://code.claude.com/docs/en/authentication)).

The precedence order, from [Authentication: precedence](https://code.claude.com/docs/en/authentication#authentication-precedence):

1. Cloud provider, when `CLAUDE_CODE_USE_BEDROCK`, `CLAUDE_CODE_USE_VERTEX`, or `CLAUDE_CODE_USE_FOUNDRY` is set
2. `ANTHROPIC_AUTH_TOKEN` (bearer, for LLM gateways)
3. `ANTHROPIC_API_KEY`. "In non-interactive mode (`-p`), the key is always used when present."
4. `apiKeyHelper` script (rotating or vault credentials)
5. `CLAUDE_CODE_OAUTH_TOKEN`, a one-year token from `claude setup-token`, "for CI pipelines and scripts where browser login isn't available"
6. Anthropic profile or Workload Identity Federation credentials
7. Subscription OAuth from `/login`, the default for Pro, Max, Ultra, Team, and Enterprise

The SDK quickstart documents an API key plus these provider switches: `CLAUDE_CODE_USE_BEDROCK=1`, `CLAUDE_CODE_USE_ANTHROPIC_AWS=1` with `ANTHROPIC_AWS_WORKSPACE_ID`, `CLAUDE_CODE_USE_VERTEX=1`, and `CLAUDE_CODE_USE_FOUNDRY=1` ([Quickstart](https://code.claude.com/docs/en/agent-sdk/quickstart)).

Constraints on the `setup-token` OAuth token ([Authentication: long-lived token](https://code.claude.com/docs/en/authentication#generate-a-long-lived-token)):

- It requires a Pro, Max, Ultra, Team, or Enterprise plan.
- "It can only make model requests", so it gives no Remote Control and no claude.ai connectors.
- Bare mode (`--bare`) does not read it.

Unattended runs on a `/login` credential stop when the login expires. "Renewing early matters most for sessions that run unattended" (same page).

### 2. What the SDK docs say about subscription login in SDK-built products

- "Unless previously approved, Anthropic does not allow third party developers to offer claude.ai login or rate limits for their products, including agents built on the Claude Agent SDK. Use the API key authentication methods described in the Quickstart instead." ([SDK overview](https://code.claude.com/docs/en/agent-sdk/overview), repeated in the [Quickstart](https://code.claude.com/docs/en/agent-sdk/quickstart))
- "Use of the Claude Agent SDK is governed by Anthropic's Commercial Terms of Service." ([SDK overview: License and terms](https://code.claude.com/docs/en/agent-sdk/overview#license-and-terms))
- The branding rules forbid calling the product "Claude Code" or mimicking it. "Claude Agent" and "{Name} Powered by Claude" are allowed (same page).

### 3. Legal and compliance page (Claude Code and Agent SDK)

Source: [Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance).

- Which terms apply: Commercial Terms "for Team, Enterprise, and Claude API users", and Consumer Terms "for Free, Pro, Max, and Ultra users".
- "Advertised usage limits for Pro, Max, and Ultra plans assume ordinary, individual usage of Claude Code and the Agent SDK."
- "OAuth authentication is intended exclusively for purchasers of Claude Free, Pro, Max, Ultra, Team, and Enterprise subscription plans and is designed to support ordinary use of Claude Code and other native Anthropic applications."
- "Developers building products or services that interact with Claude's capabilities, including those using the Agent SDK, should use API key authentication through Claude Console or a supported cloud provider. Anthropic does not permit third-party developers to offer Claude.ai login into their own applications, or to route requests through Free, Pro, Max, or Ultra plan credentials on behalf of their users. Moreover, developers may not collect, store, or intermediate Claude.ai credentials or session tokens — sign-in to a Claude account must complete through Anthropic's own flow."
- Customers may configure their own API keys or provider credentials "in a development environment, secrets manager, or machine image", provided "the resulting usage is billed to the key owner" and is not resold or intermediated. Nor does the restriction "prevent an end user from signing in to the unmodified Claude Code binary with their own Claude subscription".
- "Anthropic reserves the right to take measures to enforce these restrictions and may do so without prior notice."

### 4. Consumer Terms (Pro, Max, Ultra)

Source: [Consumer Terms](https://www.anthropic.com/legal/consumer-terms), effective Oct 8, 2025.

- Section 3, item 7 forbids, "Except when you are accessing our Services via an Anthropic API Key or where we otherwise explicitly permit it", accessing "the Services through automated or non-human means, whether through a bot, script, or otherwise."
- Section 2 forbids sharing account login, API key, or credentials, or making the account available to anyone else.
- "Our Commercial Terms of Service govern your use of any Anthropic API key."

### 5. Commercial Terms (API key)

Source: [Commercial Terms](https://www.anthropic.com/legal/commercial-terms), page dated Jun 17, 2025.

- The Commercial Terms are an agreement with "you or the organization ... you represent ('Customer')", and they govern "use of Anthropic API keys".
- "Services under these Terms are not for consumer use." Consumer offerings fall under the Consumer Terms.

### 6. Subscription plus SDK: current status (help center)

Source: [Use the Claude Agent SDK with your Claude plan](https://support.claude.com/en/articles/15036540-use-the-claude-agent-sdk-with-your-claude-plan).

- Update of June 15, 2026: previously announced changes to Agent SDK usage are paused. Agent SDK, `claude -p`, and third-party app usage "still draw from your subscription limits."
- Update of October 7, 2026: Max, Ultra, and Team plans include monthly API credits. With an API key from the linked Console org, SDK and `claude -p` use draws from those credits. Signed in with the plan, they still draw from subscription limits.
- Subscription limits are shared across Claude and Claude Code ([Use Claude Code with your Pro, Max, or Ultra plan](https://support.claude.com/en/articles/11145838-use-claude-code-with-your-pro-or-max-plan)).

### 7. Monthly API credits for subscribers

Source: [API credits for Max, Ultra, and Team plans](https://platform.claude.com/docs/en/about-claude/api-credits-for-subscribers).

- Amounts: $100/month (Max), $200/month (Ultra), and up to $500/month pooled (Team). Pro, Free, and Enterprise are not eligible.
- The credits cover the Claude API, Managed Agents, the Claude Agent SDK, and the Playground. They do not cover Claude Code, and they do not apply on Bedrock, Vertex, Foundry, or Claude Platform on AWS.
- The credits expire each cycle with no rollover, and no payment method is needed. When they run out, requests stop ("Usage is never charged to your Claude plan") unless purchased credits or auto-reload exist.
- A linked org moves up to at least the Start tier, or the Build tier if credits exceed $200.
- To cap a project, give it its own workspace with a spend limit.

### 8. Cost reporting

Source: [SDK: Track cost and usage](https://code.claude.com/docs/en/agent-sdk/cost-tracking).

- "The `total_cost_usd` and `costUSD` fields are client-side estimates, not authoritative billing data. The SDK computes them locally from a price table bundled at build time, unless a `modelPricing` table is in effect."
- "Don't bill end users or trigger financial decisions from these fields."
- The result message carries `total_cost_usd` (subagents included), `modelUsage` (per model, subagents included, with a `costBasis` of `list`, `managed`, or `unknown`), and `usage` (top-level loop only).
- Per-step `output_tokens` is a placeholder; read output totals from the result message.
- A session crash can zero the cost fields.
- `maxBudgetUsd` caps a call's own estimated spend.
- Cache TTL: on a subscription within plan usage, the main conversation gets a 1-hour TTL by default. API key and cloud providers default to 5 minutes; set `ENABLE_PROMPT_CACHING_1H` to change that.

From [Manage costs](https://code.claude.com/docs/en/costs):

- For Pro, Max, and Ultra subscribers "the session cost figure isn't relevant for billing purposes". Subscribers see plan usage bars instead.
- Authoritative spend lives in a different place for each route:

  | Route | Where authoritative spend appears |
  | --- | --- |
  | Console (API key) | Cost page, workspace spend limits, Claude Code Analytics API |
  | Cloud provider | The cloud's billing console. No data reaches Anthropic's analytics. |
  | Subscription | Plan usage windows (5-hour and weekly), plus a usage-credits row if credits are enabled |

- OpenTelemetry export works on every setup.

## Interpretation for Lopen

The points below are my reading of the sources above, not Anthropic's words.

1. **Lopen used only by its author is "ordinary, individual usage".** The author runs Lopen on their own machine with their own account. The legal page explicitly names the Agent SDK among the things plan limits are sized for, and the June 2026 help article confirms that SDK use on a plan draws from plan limits. So subscription auth for personal use is not prohibited today.
2. **Unattended, scheduled automation on a subscription is the grey zone.** The Consumer Terms bar bot or script access "except via an Anthropic API Key or where we otherwise explicitly permit it". Arguably, `claude setup-token` ("for CI pipelines and scripts") and the help-center statements count as explicit permission. However, heavy 24/7 factory load is not "ordinary" usage, and Anthropic reserves enforcement without notice. An API key removes the ambiguity: the Consumer Terms carve it out, and the Commercial Terms govern it.
3. **Publishing Lopen as a public repo for others to use makes it a "third-party developer product".** In that case Lopen must not offer claude.ai login, must not drive requests through users' plan credentials on their behalf, and must not collect, store, or intermediate their OAuth tokens. The safe design is for Lopen never to own Claude credentials:
   - Workers inherit the environment (`ANTHROPIC_API_KEY`, provider variables, or whatever the user configured for their own Claude Code binary).
   - Any sign-in happens through Anthropic's own flow (`claude /login` or `claude setup-token`), not a Lopen UI.
   - Lopen should not keep a `CLAUDE_CODE_OAUTH_TOKEN` in its own secret store or database. If the user exports one in their own shell or OS secret store, that is the user configuring their own binary, but Lopen should not prompt for it or persist it.
4. **Recommended billing route: Console API key.** If the user is on Max or Ultra, linking a Console org turns the plan's $100 or $200 monthly API credits into a free, hard-capped factory budget. When the credits run out, requests stop rather than overspend, unless auto-reload is on. Add a dedicated workspace with a spend limit for Lopen. Bedrock, Vertex, or Foundry are equally sanctioned. Pick one if the user already has cloud credits, but the subscriber API credits do not apply there.
5. **Cost reporting design.**
   - Lopen can always read `total_cost_usd` and `modelUsage` per run. Label the figure an estimate at list price.
   - On a subscription, the dollar figure is notional and plan-window consumption is what matters. The SDK exposes no plan-window percentage in the result message, so Lopen should show tokens plus a "notional $" figure.
   - On an API key, Console Cost and workspace limits are the source of truth. If reconciliation is wanted, pull them via the Admin or Usage API.
   - Use `maxBudgetUsd` per run as a guard on every route.
   - Record `costBasis` and the auth route alongside each run so the figures can be interpreted later.
6. **Operational notes.**
   - In `-p`/SDK mode a present `ANTHROPIC_API_KEY` always wins over a subscription login. Lopen should report which route a worker is using and should not silently mix routes.
   - `/login` credentials expire, and unattended workers then fail. A `setup-token` token lasts one year. An API key does not expire this way.
   - Bare mode ignores `CLAUDE_CODE_OAUTH_TOKEN`.

## Open points

- The original "previously announced changes" to SDK-on-subscription usage, which are now paused, were not found on a primary page. They could resume. Watch the help article above.
- No primary source defines a numeric threshold for "ordinary" usage.
