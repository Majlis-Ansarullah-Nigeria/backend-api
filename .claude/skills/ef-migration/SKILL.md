---
name: ef-migration
description: Create, review or apply an Entity Framework Core migration for the backend. Use after changing an entity, EF configuration or index.
argument-hint: "[MigrationName]"
allowed-tools: Bash(dotnet ef *) Bash(dotnet build *)
---

# EF Core migration

1. Make sure the model change is complete and `dotnet build backend/HRS.sln` passes.
2. Create the migration (name in PascalCase describing the change, e.g. `AddReportRowVersion`):
   `dotnet ef migrations add $ARGUMENTS -p backend/src/HRS.Infrastructure -s backend/src/HRS.Api -o Persistence/Migrations`
3. Read the generated `Up` and `Down`. Check for:
   - accidental drops or renames that lose data (use `RenameColumn`, not drop + add)
   - missing indexes for foreign keys and for `unit_path` prefix queries
   - the unique index on `(window_id, unit_id)` for report instances
   - JSONB columns typed `jsonb`, timestamps `timestamptz`
4. Apply locally: `dotnet ef database update -p backend/src/HRS.Infrastructure -s backend/src/HRS.Api`
5. Run `dotnet test backend/HRS.sln` (integration tests run migrations on a fresh container).

Rules:
- Never edit or delete a migration already merged to `main`. Add a new one.
- Data backfills go in the migration with `migrationBuilder.Sql(...)` and must be idempotent.
- Production applies migrations through the `migrator` container (EF bundle) before the API starts; never call `Database.Migrate()` at API startup.
