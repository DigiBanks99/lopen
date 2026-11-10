# Knowledge: Research

## Open Knowledge Format (OKF) v0.2

Researched 2026-11-10 for issue #35. Primary source is the spec as of commit `ad30107`
(2026-08-21) of [GoogleCloudPlatform/open-knowledge-format/SPEC.md][spec]. Section
numbers (§) below refer to that document.

### Where the canonical spec lives (and is moving)

| Location | Status |
| --- | --- |
| [GoogleCloudPlatform/knowledge-catalog/okf][kc-okf] | Original home (v0.1, June 2026). Now a **frozen snapshot**; its README says "Stop using the copy under `okf/`". Its `SPEC.md` is byte-identical to the current v0.2. |
| [GoogleCloudPlatform/open-knowledge-format][repo] | Current canonical v0.2 home (Apache-2.0). Last spec change 2026-08-21. |
| `github.com/open-knowledge-format/open-knowledge-format` | **Announced successor.** Open PR [#63][pr63] (2026-11-06, by spec author Amir Hormati) deprecates the Google repo: "OKF development continues (spec v0.3, bundles, and tooling)" in a vendor-neutral org. As of 2026-11-10 the [org][org] exists but has **0 public repos**; v0.3 is not yet readable. |

Announcement: [Google Cloud blog, "Introducing the Open Knowledge Format"][blog] (2026-06-12,
McVeety and Hormati) introduced v0.1; it states "OKF v0.1 is a starting point, not a finished
standard" and "the spec defines the interoperability surface, not the content model."

### Bundle structure (§3)

- A bundle is a directory tree of `.md` files. Directory layout is producer-chosen and domain
  independent. Distribution as a git repo is recommended; a subdirectory of a larger repo is
  allowed (§3).
- Reserved filenames at any level: `index.md` (§8) and `log.md` (§9). They MUST NOT be used for
  concepts. **Every other `.md` file is a concept** (§3.1).
- No separate tag-aggregation file; tag views are synthesised by scanning frontmatter (§3.1).

### Concept IDs (§2)

- Concept ID = the file's path within the bundle with `.md` removed (e.g. `tables/orders`).
  Identity is the path; there is no `id` frontmatter key.
- Reference tooling restricts segments to `[A-Za-z0-9_][A-Za-z0-9_.\-]*` ([paths.py][paths]); the
  spec itself states no character rules.

### Frontmatter (§4.1, §5, §10)

YAML block delimited by `---` lines at file start; UTF-8 markdown body follows (§4). Every
timestamp is ISO 8601 with an explicit offset, e.g. `2026-06-30T14:00:00Z` (§5).

| Key | Required | Meaning / allowed values | § |
| --- | --- | --- | --- |
| `type` | **Yes (only required key)** | Short free-text kind (`BigQuery Table`, `Metric`, `Playbook`, `Attested Computation`). Not registered centrally. Consumers MUST tolerate unknown types. | 4.1 |
| `title` | Recommended | Display name; consumers MAY derive from filename. | 4.1 |
| `description` | Recommended | One sentence; feeds `index.md`, search, previews. | 4.1 |
| `resource` | Recommended | URI uniquely identifying the underlying asset. Absent for abstract concepts. Path-valued: absolute URL, `/`-bundle-relative, or relative path. | 4.1, 6.2 |
| `tags` | Recommended | YAML list of short strings. | 4.1 |
| `sources` | Optional | List of provenance entries: `resource` (REQUIRED in entry; URL, bundle path, or a non-followable scope descriptor such as "all queries in project X"), `id` (stable key; SHOULD be present when the body cites it), `title`, and credibility signals `author` (actor), `usage_count`, `last_modified`. | 5.1 |
| `usage_window` | Optional | Sibling of `sources`: `{ from, to }` framing every `usage_count`; an entry MAY override. | 5.1 |
| `generated` | Optional | `{ by, at }`: who wrote the current content (`by` REQUIRED, an actor) and when it last meaningfully changed. Supersedes v0.1 `timestamp`. | 5.2 |
| `verified` | Optional | List of `{ by, at }` confirmation events, independent of `generated`. A bare mapping MUST be treated as a one-element list. | 5.2 |
| `status` | Optional | `draft` \| `stable` \| `deprecated`. Absent ⇒ `stable`. `deprecated` = kept for links and history, no longer current. | 5.4 |
| `stale_after` | Optional | Absolute instant; stale when `now >= stale_after`. Deliberately not a relative TTL. | 5.5 |
| `runtime`, `parameters`, `computation`, `executor`, `attester` | Only for `type: Attested Computation` (`runtime` required there) | Sanctioned computation contract; out of scope for Lopen. | 10 |
| anything else | Optional | Producers MAY add any keys; consumers SHOULD preserve them on round-trip and MUST NOT reject them. | 4.1 |

**Trust tier** is derived, never stored (§5.3): no `verified` ⇒ unverified; only non-`human:`
verifiers ⇒ machine-confirmed; any `human:` verifier ⇒ human-reviewed. Advisory, not access
control.

**Actors** (§7): `<producer>/<version>` for agents/tools, `human:<id>` for people,
`process:<id>` for automation. Producers MUST use `human:` for human-authored or confirmed content.

### Body (§4.2)

Free markdown, structural markdown preferred. Conventional headings: `# Schema`, `# Examples`,
`# Computation`. Per-claim attribution uses footnotes whose label equals a `sources[].id`
(`[^ga4-schema]`); the label is the join key, the footnote prose is not parsed (§5.1).

### Linking (§6)

- Ordinary markdown links. Two forms: bundle-relative starting with `/` (**recommended**, stable
  when moving within a subdirectory) and plain relative (`./other.md`) (§6.1).
- **Links are untyped.** "The specific kind (parent/child, references, joins-with, depends-on) is
  conveyed by the surrounding prose, not by the link itself." Graph consumers "typically treat all
  links as directed edges of an untyped relationship" (§6.1).
- Broken links MUST be tolerated (may represent not-yet-written knowledge) (§6.1).
- Lineage is expressed via links: a `sources[].resource` pointing at another concept is an edge;
  explicit `derived_from` is out of scope for v0.2 (§5.1).
- `references/` subdirectory is a naming convention for mirrored external material (§6.3).

### Index and log files (§8, §9)

- `index.md` MAY appear in any directory for progressive disclosure. **No frontmatter**, except a
  bundle-root `index.md` MAY carry `okf_version`. Body: headings, each with bullets
  `* [Title](relative-url) - description`; descriptions SHOULD come from the target's frontmatter.
  Producers MAY generate; consumers MAY synthesise when absent.
- `log.md` MAY appear at any level: date headings `## YYYY-MM-DD` (MUST be ISO), newest first,
  prose bullets with a conventional bold verb (`**Update**`, `**Creation**`, `**Deprecation**`).
  The spec does not forbid frontmatter in `log.md`; the [acme_retail sample log][acme-log] carries
  `type: Log`.

### Conformance (§11)

A bundle is conformant when every non-reserved `.md` has parseable YAML frontmatter with a
non-empty `type`, and present `index.md`/`log.md` follow §8/§9. Consumers MUST NOT reject for
missing optional fields, unknown types, unknown keys, broken links, or missing `index.md`.

### Versioning and extension (§12, §13)

- Versions are `<major>.<minor>`; minor = backward-compatible additions, major = breaking.
- Declared only via `okf_version: "0.2"` in the bundle-root `index.md` frontmatter. Consumers
  that don't understand it SHOULD attempt best-effort consumption.
- v0.2 broke two v0.1 things: `timestamp` → `generated.at`; body `# Citations` → `sources` (§13.1).
- Extension rule is "any extra key, flat namespace". There is no namespacing guidance; open issue
  [#61][i61] asks for one (e.g. a single producer key like `acme:`) and for how to treat foreign
  `status` values such as `status: published`.

### Custom types and typed relations: what v0.2 does and does not allow

- **Custom types:** fully allowed. `Decision`, `Term`, `Spec`, `Learning` are valid `type` values.
- **Custom keys:** allowed (e.g. the [acme_retail `gross-margin.md`][acme-gm] uses a custom `not:`
  key listing rejected definitions).
- **Typed relations: not in v0.2.** There is no `supersedes`, `relationships`, or predicate key.
  Supersession in the official sample is `status: deprecated` on the old concept plus a prose link
  from the new one ([gross-margin-legacy.md][acme-legacy]) and a `log.md` entry.
- Active proposals likely to shape v0.3:
  - [#16][i16]: `relationships:` list in frontmatter, entries `{ to, type, direction, generated, verified }`,
    `type` producer-defined. 28 comments, multiple implementers.
  - [#22][i22]: registers query-time semantics for `type: supersedes` (exclude the target from
    in-force retrieval once affirmed; no stored inverse) and `type: contradicts` (surface both;
    `contradicted_by` derived, never written).
  - [#30][i30]: author agreed to fold `relations:` into #16's `relationships:` carrier as
    subject–predicate–object assertions. A rival proposal puts YAML in the markdown link title
    ([Property Graph Markdown][pgm]).
  - [knowledge-catalog#120][kc120]: stable `id` instead of path-as-identity; #22 targets stable ids.
- **Code-symbol references:** nothing specific. `resource` (a URI) and `sources[].resource`
  (URI, path, or scope descriptor) are the only hooks; a symbol URI scheme would be a producer
  convention.

### Known gaps relevant to a repo-embedded bundle

- [#58][i58]: no way to exclude non-concept markdown (`README.md`, `AGENTS.md`) inside a bundle;
  under §11 they make the bundle non-conformant. Proposed `okf_exclude` or `.okfignore`. Current
  interoperable fallback: keep the bundle in a dedicated subdirectory.
- [#59][i59]: recommended `/`-links break in GitHub's renderer when the bundle is a subdirectory.
- [#56][i56]: YAML 1.1 parsers coerce unquoted timestamps; reference parser disables the timestamp
  resolver to keep strings ([document.py][doc]).
- [#57][i57]: samples use `author: team:…`, not an actor form defined in §7.

### Reference tooling

| Tool | Language | Notes |
| --- | --- | --- |
| [reference_agent][repo] (`src/reference_agent`) | Python 3.13 (PyYAML) | Producer (BigQuery + web crawl with Gemini) and `visualize` command. [document.py][doc] has the parser, `validate()` (checks only `type`), `normalize_verified`, `trust_tier`, `is_stale`. Not a standalone validator. |
| Viewer ([generator.py][gen]) | Python generating one HTML file (Cytoscape.js, marked) | Builds untyped edges from body links via regex. **Skips `/`-prefixed links**, the spec's recommended form (fix open in PR [#23][pr23]); renders `log.md` as a concept ([#55][i55]); ignores frontmatter `sources` edges. |
| Third party, .NET | C# | [jchable/okf4net][okf4net] (LGPL-3.0, v0.7.0 2026-10-14): library, CLI validator, renderer, Agent Framework tools, MCP server. Not reviewed in depth. |
| Third party, other | Go, Rust, Python | [openknowledge-sh/openknowledge][oks] (Go CLI), [okf-rs][okfrs] (Rust validator/server), [scaccogatto/okf-skills][okfskills] (Claude Code plugin and GitHub Action). Not reviewed. |

There is no official JSON Schema or conformance test suite.

[spec]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/ad30107/SPEC.md
[repo]: https://github.com/GoogleCloudPlatform/open-knowledge-format
[kc-okf]: https://github.com/GoogleCloudPlatform/knowledge-catalog/tree/main/okf
[pr63]: https://github.com/GoogleCloudPlatform/open-knowledge-format/pull/63
[org]: https://github.com/open-knowledge-format
[blog]: https://cloud.google.com/blog/products/data-analytics/how-the-open-knowledge-format-can-improve-data-sharing
[paths]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/src/reference_agent/bundle/paths.py
[doc]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/src/reference_agent/bundle/document.py
[gen]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/src/reference_agent/viewer/generator.py
[acme-log]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/bundles/acme_retail/log.md
[acme-gm]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/bundles/acme_retail/metrics/gross-margin.md
[acme-legacy]: https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/bundles/acme_retail/metrics/gross-margin-legacy.md
[i16]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/16
[i22]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/22
[i30]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/30
[i55]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/55
[i56]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/56
[i57]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/57
[i58]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/58
[i59]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/59
[i61]: https://github.com/GoogleCloudPlatform/open-knowledge-format/issues/61
[pr23]: https://github.com/GoogleCloudPlatform/open-knowledge-format/pull/23
[kc120]: https://github.com/GoogleCloudPlatform/knowledge-catalog/issues/120
[pgm]: https://github.com/property-graph-markdown/specification/blob/main/SPEC.md
[okf4net]: https://github.com/jchable/okf4net
[oks]: https://github.com/openknowledge-sh/openknowledge
[okfrs]: https://github.com/jyjeanne/okf-rs
[okfskills]: https://github.com/scaccogatto/okf-skills
