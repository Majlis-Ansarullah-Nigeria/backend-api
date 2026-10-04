# ADR 0002 — Report templates and answers as JSONB

Status: accepted

## Decision
Store each published template version as a JSON schema document and each report's answers as JSONB keyed by stable field keys. Copy numeric answers to a narrow `report_metrics` table for dashboards and roll-ups.

## Consequences
- The Secretariat can add or change report types without migrations.
- Old reports render with their own version forever.
- Validation must run against the schema on both client and server (shared expression fixtures keep them in step).
- Analytics only use fields copied to `report_metrics`; adding a dashboard metric means marking the field numeric with an aggregation rule.
