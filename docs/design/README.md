# Design

Mockups (source of truth for UI): https://claude.ai/artifact/434012db-251d-4dbc-96ea-1789a7d6b293
Open the canvas, find the board named below, and match its layout, copy and states. If code and mockup disagree, ask before changing either.

## Tokens
Plus Jakarta Sans (UI) and IBM Plex Mono (reference numbers and codes), 8 px spacing grid, radii 10–16 px, touch targets ≥ 44 px. Copy into `frontend/src/styles/tokens.css` and map them in the Tailwind theme.

```css
:root {
  --font-sans: "Plus Jakarta Sans", ui-sans-serif, system-ui, sans-serif;
  --font-mono: "IBM Plex Mono", ui-monospace, monospace;

  --color-forest-900: #053D27;  /* sidebar, dark surfaces */
  --color-green-700:  #075C39;  /* primary text on mint, links */
  --color-green-600:  #0B7A4B;  /* primary actions */
  --color-green-500:  #12A15F;  /* charts, success */
  --color-lime:       #B7F171;  /* highlights, active marks */
  --color-mint:       #DDF6E8;  /* selected, soft fills */
  --color-ground:     #F3F8F4;  /* app background */
  --color-surface:    #FFFFFF;
  --color-ink:        #0F1F17;  /* text */
  --color-ink-muted:  #4F6157;  /* secondary text */
  --color-line:       #E1EAE4;
  --color-line-strong:#CFDCD3;
  --color-danger:     #B42318;

  /* status chips: background / text (always with an icon and a word) */
  --status-not-started-bg: #EEF2F0; --status-not-started-fg: #46524C;
  --status-draft-bg:       #EEF2F0; --status-draft-fg:       #33403A;
  --status-submitted-bg:   #E5EDFF; --status-submitted-fg:   #1D44B8;
  --status-in-review-bg:   #EEE8FF; --status-in-review-fg:   #5427C2;
  --status-late-bg:        #FFF1DB; --status-late-fg:        #8A4300;
  --status-returned-bg:    #FDE8E7; --status-returned-fg:    #A11C12;
  --status-acknowledged-bg:#D9F5E5; --status-acknowledged-fg:#075C39;
  --status-missed-bg:      #F1E4E4; --status-missed-fg:      #6E1D1D;
  --status-overdue-bg:     #FFE6D3; --status-overdue-fg:     #8F3700;
  --status-upcoming-bg:    #E9F2FB; --status-upcoming-fg:    #24527F;

  --radius-sm: 10px; --radius-md: 14px; --radius-lg: 16px;
}
```
Dark mode values: see the "Dark mode (mobile)" board. Re-check every value against the "Design system" board before M0.5.

## Screen map
| Board on the canvas | Route | Milestone |
| --- | --- | --- |
| Design system; Empty, loading and error states | `components/shared`, tokens | M0.5 |
| Sign in; Email code (mobile); Two-step sign-in setup | `/login`, `/login/code`, `/settings/security/mfa` | M1 |
| Accept invitation | `/invite/[token]` | M1.3 |
| Head home · Muqam; Head home (mobile); Dark mode (mobile) | `/home` | M5.2 |
| My reports | `/reports` | M5.2 |
| Report form · desktop; Report form (mobile stepper) | `/reports/[id]/edit` | M5.3 |
| Review and submit; Submitted (mobile) | `/reports/[id]/review`, `/reports/[id]/submitted` | M5.5 |
| Returned report | `/reports/[id]/edit?returned=1` | M7.2 |
| Compliance grid · Dila | `/units` | M6.1 |
| Child report · read-only | `/units/[unitId]/reports/[id]` | M6.2 |
| Pull from Muqamaat · Dila | panel inside `/reports/[id]/edit` | M6.3 |
| Department data · Finance | `/department` | M7.4 |
| History and downloads; Compare periods | `/history`, `/history/compare` | M9 |
| Notifications (mobile) | `/notifications` | M8.2 |
| Profile, delegates, sessions | `/profile` | M1.5, M2.4 |
| Secretariat overview | `/secretariat` | M10.1 |
| Secretariat inbox and review | `/secretariat/inbox` | M7.1–M7.3 |
| Report types; Template builder | `/secretariat/report-types`, `/secretariat/templates/[id]` | M3 |
| Windows calendar; Create window · per-level due dates | `/secretariat/windows`, `/secretariat/windows/new` | M4.5 |
| Organization tree and heads; Bulk import wizard | `/secretariat/organization`, `/secretariat/organization/import` | M2 |
| Users and roles | `/secretariat/users` | M2.4 |
| Announcements and email wording | `/secretariat/announcements` | M8.3 |
| Audit log; Settings, integrations, health | `/secretariat/audit`, `/admin/settings` | M11 |
| Compliance dashboard; Indicators, drill-down and map | `/secretariat/dashboards/...` | M10 |
| Exports centre; Compiled national report | `/secretariat/exports`, `/secretariat/compiled/[windowId]` | M9.2, M10.3 |
| Leadership overview (mobile) | `/home` for LEADERSHIP | M10.2 |

Tip for Claude Code: the canvas isn't readable from the CLI. Export the boards you're working on as PNG from the canvas (Share › Export) into `docs/design/screens/` and reference the file in your prompt.
