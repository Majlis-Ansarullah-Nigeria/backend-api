# Accessibility audit: head-facing screens (M12.3, NFR-ACC-01)

Target: WCAG 2.2 AA. NFR-ACC-01 spells this out as keyboard navigation, screen-reader labels,
4.5:1 contrast, focus states and no colour-only status.

## Automated: axe in Playwright (2026-10-25)

`frontend/e2e/accessibility.spec.ts` covers every head-facing screen, signed in as real seeded
heads, at desktop (Desktop Chrome) and mobile (Pixel 7) sizes. It checks the axe rule tags `wcag2a`,
`wcag2aa`, `wcag21a`, `wcag21aa` and `wcag22aa`, which include contrast, names, labels, landmarks and
target size.

| Role | Screens |
| --- | --- |
| Signed out | Sign in, forgot password |
| Muqam head with a draft | Home, My reports, report form, review and submit, notifications, profile |
| Muqam head who submitted | Submitted, History, version history, compare periods |
| Dila head (observer) | Compliance grid ("My Muqamaat"), read-only child report |

Result: no violations on any screen at either size.

To run it, see `frontend/e2e/global-setup.ts`. You need the API on :8080, the local database, and
`API_PROXY_URL=http://localhost:8080 pnpm -C frontend build`, then `pnpm -C frontend test:e2e`.

## Fixed in this pass

| Issue | WCAG | Fix |
| --- | --- | --- |
| No way to skip the sidebar's navigation | 2.4.1 Bypass Blocks | "Skip to main content" link, the first Tab stop on every signed-in page |
| The version history page had no `<h1>` | 1.3.1, 2.4.6 | The panel's heading is an `<h1>` on its own page and an `<h2>` inside the review pane |
| The report form's current section was shown by colour only | 1.4.1 Use of Colour, 1.3.1 | `aria-current="step"` on the current section |
| Completed sections were shown by an icon only | 1.1.1, 1.3.1 | Hidden ", completed" after the section name |
| Moving to another section left focus behind, so the new section wasn't announced | 2.4.3 Focus Order | Focus moves to the new section's heading (not on first load) |
| The section list was an unnamed `<nav>` | 1.3.1, 2.4.6 | Labelled "Sections" |

## Manual screen-reader pass (to do: needs a person)

Use NVDA + Chrome on Windows and TalkBack + Chrome on an Android phone, signed in as a muqam head.
Record anything unclear here.

- [ ] Sign in: field labels and errors are announced, and the method tabs read as tabs.
- [ ] Home: the greeting, the current report's status and due date are read as text, not just colour.
- [ ] Report form: each field's label, help text, unit and required state are read. An error is
      announced on blur and read again when the field is focused. "Saved" / "Saving" is announced
      without stealing focus. Next/Back announces the new section.
- [ ] Pull from Muqamaat (Dila head): included and missing children are read as lists.
- [ ] Review and submit: warnings are read before the confirm checkbox, and the confirmation dialog
      traps focus and returns it afterwards.
- [ ] Submitted: the reference number and on-time/late status are read.
- [ ] History: the table has column headers, and the row actions name the report ("View",
      "Version history of …").
- [ ] Notifications: unread state is announced, and "Mark all read" reports the result.
- [ ] Ctrl/Cmd+K palette: the input reads as a combobox, arrow keys announce the active option, and
      Escape returns focus to where it was.
- [ ] Mobile (TalkBack): bottom tabs announce the current tab, and every touch target is easy to
      hit.

## Not covered here

- Secretariat and admin screens. M12.3 scopes the pass to head-facing screens.
- The app shell still shows placeholder user and unit names (`src/app/(app)/layout.tsx`). That's a
  functional gap, not an accessibility one, and is tracked separately.
