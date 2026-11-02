# Findings & Decisions

## Requirements
-

## Research Findings
-

## Technical Decisions
| Decision | Rationale |
|----------|-----------|

## Issues Encountered
| Issue | Resolution |
|-------|------------|

## Resources
-

- Existing app running from 1.2.0 single-file; preserve its session and user data. Use current real editor in a temporary isolated demo host if needed, without editing product source.
- README currently embeds old test-pattern evidence. User requests actual public browser content and no sensitive data. Candidate page: NASA Webb mission public site.
- No commit or push requested in this follow-up. Documentation/images only; no full app tests needed.

- Browser opened NASA Webb mission page. First viewport has a predominantly black hero; choose its public image gallery for a stronger visual. Browser captures are viewport-only (no address bar/tabs/profile).
- Current genuine EditorWindow can be shown from an isolated temporary host with the published assembly; no original product code changes, no existing user session replacement, private settings/history unaffected.

- NASA page image assets did not render; switched browser to official Firefox public homepage (mozilla.org redirects firefox.com). Snapshot confirms public marketing content without account state.
- Browser page CSP blocked a main-world read-only evaluate; use screenshot/snapshot APIs instead. Demo host compiled successfully, 0 warnings, against actual 1.3.2 assembly.
