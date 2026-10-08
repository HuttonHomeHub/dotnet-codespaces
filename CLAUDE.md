# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PhotoMapper: a .NET 10 / .NET Aspire 13 solution with a Blazor Web App frontend (`src/PhotoMapper.Web`) and a minimal API (`src/PhotoMapper.ApiService`), orchestrated by `src/PhotoMapper.AppHost`. Both apps are currently empty starters with no domain code yet. The solution file is `PhotoMapper.slnx`.

## Commands

```bash
aspire run                                    # run everything with hot reload (watch mode is on in aspire.config.json)
dotnet build PhotoMapper.slnx
dotnet test PhotoMapper.slnx
dotnet test PhotoMapper.slnx --filter "FullyQualifiedName~WebTests"   # single test class/method
dotnet format PhotoMapper.slnx                # fix formatting; CI runs it with --verify-no-changes
```

Ports: Aspire dashboard 15051 (login token printed by `aspire run`), API 8080 (Scalar UI at `/scalar` in Development), web 8081. These are set in each project's `Properties/launchSettings.json` and forwarded in `.devcontainer/devcontainer.json`, so change both together.

## Architecture

- **AppHost** (`AppHost.cs`) declares the services. The web frontend references the API as `apiservice`, so the web app can reach it by service discovery at `http://apiservice` (register a typed `HttpClient` with that base address) instead of a hard-coded URL. New services, databases or containers are added here.
- **ServiceDefaults** (`Extensions.cs`) is referenced by every service and called via `builder.AddServiceDefaults()` / `app.MapDefaultEndpoints()`. It configures OpenTelemetry, service discovery, HTTP resilience and the `/health` and `/alive` endpoints (Development only).
- **Web** uses the .NET 8+ Blazor Web App model (`AddRazorComponents().AddInteractiveServerComponents()`, `Components/` folder, `App.razor` + `Routes.razor`), not the legacy `_Host.cshtml` Blazor Server model.
- **ApiService** clears `document.Servers` in its OpenAPI transformer as a workaround for Codespaces port forwarding (dotnet/aspnetcore#57332); keep it.
- **Tests** use `Aspire.Hosting.Testing` (`DistributedApplicationTestingBuilder`) to start the real AppHost, so each test boots the whole app (~20s). Prefer one shared fixture when adding many tests.

## Build conventions

- Target framework, nullable, implicit usings, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended` and `EnforceCodeStyleInBuild` live in `Directory.Build.props`; don't repeat them in `.csproj` files. Together these make `.editorconfig` rules set to `warning` (and recommended CA rules) fail the build.
- Package versions are central in `Directory.Packages.props`; `PackageReference` items have no `Version`.
- Every project has a committed `packages.lock.json`. After changing a package, run `dotnet restore PhotoMapper.slnx` and commit the updated lock files; CI restores in locked mode (set in `Directory.Build.props` when `GITHUB_ACTIONS` is true) and fails on drift. NuGet audit fails restore on any known-vulnerable package, transitive ones included.
- `nuget.config` restricts restore to nuget.org via package source mapping; adding another feed means adding a source and a mapping there.

## Repo and CI

- `main` is protected by a ruleset (source of truth: `.github/rulesets/protect-main.json`): changes go through squash-merged PRs that must pass Build and Test, CodeQL (`Analyze (csharp)`, `Analyze (actions)`) and Dependency Review. Renaming a workflow job breaks the required check, so update the ruleset with it.
- Actions are pinned to full commit SHAs with a `# vX.Y.Z` comment; Dependabot updates both. Dependabot waits 14 days before proposing new versions.
- The Aspire version (13.6.1) appears in four places that must move together: the `Aspire.AppHost.Sdk/<version>` in the AppHost `.csproj`, `Aspire.Hosting.Testing` in `Directory.Packages.props`, the Aspire CLI install in `.devcontainer/devcontainer.json`, and `ASPIRE_CLI_VERSION` in `.github/workflows/build.yml`.
- The AppHost uses `AspireUseCliBundle=true`, so builds of the AppHost need the Aspire CLI (`aspire`) on PATH (the container adds `~/.dotnet/tools`).
- The SDK is pinned in `global.json`. Line endings are LF (`.gitattributes`, `.editorconfig`).
