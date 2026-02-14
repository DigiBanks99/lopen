---
name: auth
description: The authentication requirements of Lopen for GitHub Copilot SDK access
---

# Auth Specification

> **Status**: Minimal stub — to be refined in a future session.

## Overview

Lopen requires authentication with GitHub to use the Copilot SDK. This module handles credential management for the GitHub Copilot SDK integration.

---

## Commands

```sh
lopen auth login          # Authenticate with GitHub
lopen auth status         # Check current authentication state
lopen auth renew          # Renew authentication tokens
lopen auth logout         # Clear stored credentials
```

See [CLI Specification § lopen auth](../cli/SPECIFICATION.md#lopen-auth) for command details.

---

## TODO

The following areas need refinement:

- [ ] Authentication flow details (device flow, OAuth, PAT?)
- [ ] Credential storage location and format (`~/.config/lopen/` vs keychain vs Copilot SDK managed)
- [ ] Token refresh strategy (automatic vs manual)
- [ ] Multi-account support (if needed)
- [ ] Relationship with existing `gh` CLI authentication — can Lopen reuse `gh auth` tokens?
- [ ] Error handling for expired/revoked tokens during a workflow session
- [ ] Headless/CI authentication (service accounts, environment variables)

---

## References

- [CLI Specification](../cli/SPECIFICATION.md) — Auth command structure
- [LLM Specification](../llm/SPECIFICATION.md) — How authentication feeds into SDK invocation
- [Configuration Specification](../configuration/SPECIFICATION.md) — Auth-related settings (if any)
