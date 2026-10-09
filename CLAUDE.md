# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PhotoMapper: a .NET 10 / .NET Aspire 13 solution with a Blazor Web App frontend (`src/PhotoMapper.Web`) and a minimal API (`src/PhotoMapper.ApiService`), orchestrated by `src/PhotoMapper.AppHost`. Both apps are currently empty starters with no domain code yet. The solution file is `PhotoMapper.slnx`.

## Commands

```bash
scripts/run.sh                                # aspire run (hot reload, from aspire.config.json) with a guaranteed clean stop
scripts/stop.sh                               # stop leftovers from any run and free the app's ports
dotnet build PhotoMapper.slnx
dotnet test --solution PhotoMapper.slnx       # tests run on Microsoft.Testing.Platform (global.json): no positional path
dotnet test --solution PhotoMapper.slnx --filter-class "*WebFrontendTests"   # or --filter-method "*TestName"
dotnet format PhotoMapper.slnx                # fix formatting; CI runs it with --verify-no-changes
```

Ports: Aspire dashboard 15051 (login token printed by `aspire run`), API 8080 (Scalar UI at `/scalar` in Development), web 8081. These are set in each project's `Properties/launchSettings.json` and forwarded in `.devcontainer/devcontainer.json`, so change both together; `scripts/stop.sh` reads its port list from the launchSettings files. All other ports are not auto-forwarded (`otherPortsAttributes`).

Run and stop the app with the scripts (or the VS Code `run`/`stop` tasks; both F5 configurations run `stop` before and after debugging), not bare `aspire run`: if `aspire run`'s terminal is closed or it is killed, its `dotnet watch` process survives, keeps ports open and ignores SIGTERM, and the orphaned orchestrator (DCP) lingers for minutes. `stop.sh` kills only this repo's runs and DCPs whose AppHost is gone, so it is safe while integration tests run.

## Architecture

- **AppHost** (`AppHost.cs`) declares the services. The web frontend references the API as `apiservice`, so the web app can reach it by service discovery at `http://apiservice` (register a typed `HttpClient` with that base address) instead of a hard-coded URL. New services, databases or containers are added here.
- **Deployment** is Docker Compose. In publish mode the AppHost's `AddDockerComposeEnvironment` and `PublishAsDockerComposeService` callbacks shape the generated `docker-compose.yaml` (released dashboard image on loopback only, restart policies, no host port for the web app, a `webfrontend-home` volume for data protection keys). `deploy/` holds the hand-written parts (Caddy proxy, `.env` writer, smoke test) and `deploy/README.md` is the runbook. After changing the AppHost, run the local bundle test in that README.
- **ServiceDefaults** (`Extensions.cs`) is referenced by every service and called via `builder.AddServiceDefaults()` / `app.MapDefaultEndpoints()`. It configures OpenTelemetry, service discovery, HTTP resilience and the `/health` and `/alive` endpoints (Development only).
- **Web** uses the .NET 8+ Blazor Web App model (`AddRazorComponents().AddInteractiveServerComponents()`, `Components/` folder, `App.razor` + `Routes.razor`), not the legacy `_Host.cshtml` Blazor Server model.
- **ApiService** clears `document.Servers` in its OpenAPI transformer as a workaround for Codespaces port forwarding (dotnet/aspnetcore#57332); keep it.
- **Integration tests** (`tests/PhotoMapper.IntegrationTests`) use `Aspire.Hosting.Testing` to start the real AppHost. `AppHostFixture` is an xUnit v3 assembly fixture, so the app boots once per run (~20s); new test classes take it as a constructor parameter instead of building their own AppHost. The coverage extension follows the app processes Aspire launches, so these tests do count towards coverage.
- **Unit tests** live in one project per app (`tests/PhotoMapper.Web.Tests`, using bUnit for Razor components: test classes derive from `BunitContext`). Put fast logic tests there rather than in the integration tests.
- **Coverage**: CI merges the Cobertura reports with ReportGenerator (pinned in `dotnet-tools.json`), writes a summary to the job page and fails below `MIN_LINE_COVERAGE` (80%) in `build.yml`. `tests/coverage.config` limits coverage to the app assemblies. Local equivalent: `dotnet test --solution PhotoMapper.slnx --coverage --coverage-output-format cobertura --coverage-settings tests/coverage.config --results-directory TestResults`, then `dotnet tool run reportgenerator -reports:"TestResults/*.cobertura.xml" -targetdir:TestResults/coverage -reporttypes:HtmlInline`.
- **Test projects** get xUnit v3, the coverage extension, `IsTestProject` and `OutputType=Exe` from `tests/Directory.Build.props`; don't repeat them in test `.csproj` files.

## Build conventions

- Target framework, nullable, implicit usings, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended` and `EnforceCodeStyleInBuild` live in `Directory.Build.props`; don't repeat them in `.csproj` files. Together these make `.editorconfig` rules set to `warning` (and recommended CA rules) fail the build, including unused usings (IDE0005, which is why `GenerateDocumentationFile` is on with CS1591 suppressed).
- Package versions are central in `Directory.Packages.props`; `PackageReference` items have no `Version`.
- Every project has a committed `packages.lock.json`. After changing a package, run `dotnet restore PhotoMapper.slnx` and commit the updated lock files; CI restores in locked mode (set in `Directory.Build.props` when `GITHUB_ACTIONS` is true) and fails on drift. NuGet audit fails restore on any known-vulnerable package, transitive ones included.
- `nuget.config` restricts restore to nuget.org via package source mapping; adding another feed means adding a source and a mapping there.
- The Aspire version (13.6.1) appears in six places that must move together: the `Aspire.AppHost.Sdk/<version>` in the AppHost `.csproj`; `Aspire.Hosting.Testing` and `Aspire.Hosting.Docker` in `Directory.Packages.props`; the Aspire CLI install in `.devcontainer/devcontainer.json`; and `ASPIRE_CLI_VERSION` in both `.github/workflows/build.yml` and `deploy.yml`.
- Container images are built by the .NET SDK (`dotnet publish -t:PublishContainer`), not Dockerfiles: `ContainerFamily` and the source label are in `Directory.Build.props`, `ContainerRepository` in each app's `.csproj`.
- The AppHost uses `AspireUseCliBundle=true`, so builds of the AppHost need the Aspire CLI (`aspire`) on PATH (the container adds `~/.dotnet/tools`).
- The SDK is pinned in `global.json`. Line endings are LF (`.gitattributes`, `.editorconfig`).
- Codespaces keeps only `/workspaces` across container rebuilds, so `devcontainer.json` sets `CLAUDE_CONFIG_DIR=/workspaces/.claude-code`: Claude Code's chats, memory, settings and login live there (outside the repo) instead of `~/.claude`.

## Repo and CI

- `main` is protected by a ruleset (source of truth: `.github/rulesets/protect-main.json`): changes go through squash-merged PRs that must pass Build and Test, CodeQL (`Analyze (csharp)`, `Analyze (actions)`), Dependency Review, Lint (`Lint workflows and scripts`: actionlint and shellcheck) and the Containers smoke test (`Build, smoke-test and publish images`). Renaming a workflow job breaks the required check, so update the ruleset with it.
- Actions are pinned to full commit SHAs with a `# vX.Y.Z` comment; Dependabot updates both. Dependabot waits 14 days before proposing new versions.
