# Repository Guidelines

## Project Structure & Module Organization

The active client lives under `DatabaseManager.Avalonia/`. Its solution contains `DatabaseManager.Avalonia` (AXAML views and controls), `DatabaseManager.AppCore` (UI-independent models, services, and view models), and `DatabaseManager.AppCore.RegressionTests` (executable regression checks). Shared database engines are organized in `DatabaseInterpreter/` and SQL conversion components in `DatabaseConverter/`. The older WinForms code remains under `DatabaseManager/` for compatibility; implement new UI work in Avalonia unless a change explicitly targets legacy code. Architecture and delivery notes live in `DatabaseManager.Avalonia/docs/`; screenshots and smoke-test evidence belong in `docs/smoke/` or `resources/`.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet restore DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln
dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests
dotnet test DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
```

The first two commands reproduce the cross-platform CI build. The regression project is a console-based assertion suite and must be run directly; success ends with `All regression checks passed.` Use the GUI project's `-- --smoke` options only when the required local database is configured (see `docs/smoke/README.md`).

## Coding Style & Naming Conventions

Use four-space indentation and standard modern C# conventions: PascalCase for types and public members, camelCase for locals and parameters, and `_camelCase` for private fields. Nullable reference types and implicit usings are enabled in .NET 8 projects. Keep AXAML view names paired with their code-behind (`ConnectWindow.axaml` / `.axaml.cs`), and place reusable behavior in AppCore rather than UI code. Preserve existing localized user-facing text and nearby comment language. Format touched C# with `dotnet format` when practical; avoid unrelated formatting churn.

## Testing Guidelines

Add focused checks to the regression executable (typically `*Checks.cs`) for service, SQL-generation, and view-model behavior. Keep tests deterministic; database-backed checks must be opt-in, following patterns such as `DBM_TEST_TEMP_DATABASES=1`. For visual changes, run the app on the affected platform and update smoke evidence when relevant.

## Commit & Pull Request Guidelines

Recent history uses concise imperative subjects, often Conventional Commit prefixes such as `feat:`, `ui:`, or scoped forms like `refactor(workbench):`. Keep each commit focused. Pull requests should explain behavior and affected databases/platforms, link relevant issues, list build/regression results, and include before/after screenshots for UI changes. Never commit credentials, connection profiles, generated `bin/` or `obj/` output, or local database files.
