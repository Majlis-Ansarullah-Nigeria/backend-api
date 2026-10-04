---
name: frontend-feature
description: Build or change a screen, form or component in the Next.js frontend, matching the mockups and design tokens, using the generated API client. Use for any UI work in frontend/.
paths: frontend/**
---

# Build a frontend feature

1. **Find the design.** Open `docs/design/README.md`, find the mockup board for this screen and the requirement IDs. Note every state the mockup shows (empty, loading, error, read-only, mobile).
2. **Check the contract.** Make sure the endpoints exist in `src/lib/api/generated`. If not, build the backend first (`backend-feature` skill) and run `api-sync`.
3. **Route and data.**
   - Page in `src/app/(app)/...`. Server Component fetches initial data with the server client (cookie forwarded) and passes it to client components as `initialData`.
   - Client interactivity uses the generated TanStack Query hooks. Mutations invalidate the affected query keys and show a toast.
4. **UI.**
   - Compose from `components/ui` (shadcn) and `components/shared`. Put module-specific components in `src/features/<module>/components`.
   - Use tokens only (`bg-surface`, `text-ink-muted`, `--color-accent`), never raw hex.
   - Status chips use `StatusChip` (icon + word). Read-only screens show `ReadOnlyBanner` and no action buttons.
   - Mobile layout at 360–390 px: bottom tabs, stacked cards, 44 px targets, 16 px inputs. Desktop: sidebar shell.
5. **Forms.** React Hook Form + Zod. Report forms render through `lib/form-engine` from the template version (never hand-code template fields). Autosave with `useAutosave` (10 s + blur, `If-Match`, offline queue).
6. **States.** Skeletons for loading, `EmptyState` with a next action, `ErrorState` that maps ProblemDetails `code` to a human message.
7. **Tests.**
   - Vitest + Testing Library for components with logic (form engine, guards, formatters).
   - Playwright for the user flow, with `@axe-core/playwright` and no serious violations, at desktop and a 390 px mobile viewport.
8. **Verify.** Run the `verify` skill. Summarize with requirement IDs and mention any difference from the mockup and why.

Never: fetch the API directly from components, store tokens in localStorage, edit `lib/api/generated` by hand, or gate security in the UI only.
