# Frontend — Next.js (App Router, TypeScript)

Latest stable Next.js and React, TypeScript strict, pnpm. Output `standalone` for Docker.

## Stack
Tailwind CSS + shadcn/ui (Radix) · TanStack Query · React Hook Form + Zod · generated API client (orval, from the backend OpenAPI) · Recharts · dnd-kit (template builder) · next-intl (English only at launch) · Vitest + Testing Library · Playwright + axe-core.

## Structure
```
src/
  app/
    (auth)/login, (auth)/invite/[token], (auth)/reset
    (app)/layout.tsx            app shell: sidebar (desktop), bottom tabs (mobile), unit switcher
    (app)/home                  role-aware home
    (app)/reports, reports/[id]/edit, reports/[id]/review, reports/[id]
    (app)/units                 observer compliance grid + child reports (Dila/Zone)
    (app)/history, (app)/notifications, (app)/profile
    (app)/secretariat/{inbox,report-types,templates/[id],windows,organization,users,announcements,audit,exports,dashboards}
    (app)/admin/settings
  features/<module>/{components,hooks,schemas,utils}   module UI and logic
  components/ui/                shadcn primitives (generated, edit sparingly)
  components/shared/            StatusChip, DueCountdown, SaveIndicator, EmptyState, PageHeader, ReadOnlyBanner
  lib/api/generated/            orval output — never edit by hand
  lib/api/client.ts             fetch wrapper (credentials: include, ProblemDetails parsing)
  lib/auth/                     session helpers, role guards
  lib/form-engine/              renders a template version: fields, conditional logic, computed fields
  styles/tokens.css             design tokens (from docs/design/README.md)
```

## Rules
- Server Components by default; `"use client"` only for interactivity (forms, builder, charts).
- All data goes through the generated client and TanStack Query hooks. No ad-hoc `fetch` to the API in components.
- Auth: the backend sets httpOnly cookies. Browser calls go to same-origin `/api/...`. Server-side calls use `API_INTERNAL_URL` and forward the cookie. `middleware.ts` only redirects anonymous users; real permission checks are the API's.
- Roles change navigation, never security. Hide actions the user can't take, and handle 403 gracefully anyway.
- Forms: Zod schemas built from the template version, mirroring server validation. Autosave every 10 s and on blur, show `SaveIndicator`, queue unsaved changes in IndexedDB when offline (NFR-RES-01).
- The computed-field expression evaluator in `lib/form-engine` must give the same results as the backend one. Both run the shared fixtures in `/shared/expression-fixtures.json`.
- Every screen has loading (skeleton), empty and error states. Mobile first: design at 360 px, touch targets ≥ 44 px, body text 16 px.
- Accessibility: WCAG 2.2 AA. Real buttons/links/labels, visible focus, status chips with icon + word, never colour alone.
- Use design tokens (CSS variables / Tailwind theme). No hard-coded hex values in components.
- Strings go through next-intl message files, even though only English ships.
- Keep the first-load JS of report screens under 200 KB (check `next build` output).

## Commands
`pnpm dev` · `pnpm build` · `pnpm lint` · `pnpm typecheck` · `pnpm test` · `pnpm test:e2e` · `pnpm api:generate`
