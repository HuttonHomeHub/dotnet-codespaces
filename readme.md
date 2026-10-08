# PhotoMapper

A .NET 10 web app built with [.NET Aspire](https://aspire.dev): a Blazor frontend and a minimal API, run together by an Aspire AppHost.

## Getting started

Open the repo in GitHub Codespaces (or VS Code with the Dev Containers extension). The container installs the .NET 10 SDK and the Aspire CLI and restores packages.

Run the app with hot reload:

```bash
aspire run
```

Or use the VS Code task **run (aspire, hot reload)**, or press F5 (**PhotoMapper (Aspire)**) to debug.

`aspire run` prints a dashboard link with a login token. The dashboard lists every service, its logs, traces and metrics. In Codespaces, open the forwarded ports from the **Ports** tab:

| Port  | What                                                    |
|-------|---------------------------------------------------------|
| 15051 | Aspire dashboard                                        |
| 8081  | Web frontend (Blazor)                                   |
| 8080  | API (interactive API docs at `/scalar`, Development only) |

Saving a file reloads the change into the running app. Edits that can't be hot-reloaded restart the affected service automatically.

## Project layout

```
src/
  PhotoMapper.AppHost/          Aspire orchestrator: starts and wires up the services
  PhotoMapper.ServiceDefaults/  Shared telemetry, health checks, resilience, service discovery
  PhotoMapper.ApiService/       Minimal API
  PhotoMapper.Web/              Blazor Web App (interactive server rendering)
tests/
  PhotoMapper.IntegrationTests/ xUnit v3 tests that start the whole app once via the AppHost
```

## Build, test, format

```bash
dotnet build PhotoMapper.slnx
dotnet test --solution PhotoMapper.slnx
dotnet format PhotoMapper.slnx
```

CI runs the formatting check, build and tests on every push and pull request to `main`, plus CodeQL and dependency review. Changes reach `main` only through pull requests.
