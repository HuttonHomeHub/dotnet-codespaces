# PhotoMapper

A .NET 10 web app built with [.NET Aspire](https://aspire.dev): a Blazor frontend and a minimal API, run together by an Aspire AppHost.

## Getting started

Open the repo in GitHub Codespaces (or VS Code with the Dev Containers extension). The container installs the .NET 10 SDK, the Aspire CLI, Docker and the GitHub CLI, plus the VS Code extensions for C#, Aspire, containers, GitHub Actions and pull requests, and restores packages.

Run the app with hot reload:

```bash
scripts/run.sh      # aspire run, plus a guaranteed clean stop
scripts/stop.sh     # stop anything a previous run left behind and free the ports
```

Or use the VS Code tasks **run** and **stop**, or press F5 (**PhotoMapper (Aspire)**) to debug every service at once.

However the app is stopped (Ctrl+C, closing the terminal, terminating the task, or stopping the debugger), nothing is left running and ports 8080, 8081 and 15051 are free for the next start. If a run was killed outright, the next start cleans up first. Plain `aspire run` also works, but if its terminal is closed its hot-reload process can linger and keep ports open; `scripts/stop.sh` clears that.

The run prints a dashboard link with a login token. The dashboard lists every service, its logs, traces and metrics. In Codespaces, open the forwarded ports from the **Ports** tab:

| Port  | What                                                    |
|-------|---------------------------------------------------------|
| 15051 | Aspire dashboard                                        |
| 8081  | Web frontend (Blazor)                                   |
| 8080  | API (interactive API docs at `/scalar`, Development only) |
| 8025  | Mailpit: catches every email the app sends (account confirmation, password reset) |

The app runs PostgreSQL and Mailpit in Docker containers. To try accounts, register on the web app, then open the confirmation email in Mailpit. Database changes are applied automatically on start.

To make your account an admin (which adds a **Users** page for managing accounts), confirm it, then run `scripts/make-admin.sh you@example.com` while the app is running and sign in again.

Saving a file reloads the change into the running app. Edits that can't be hot-reloaded restart the affected service automatically.

## Project layout

```
src/
  PhotoMapper.AppHost/          Aspire orchestrator: starts and wires up the services
  PhotoMapper.ServiceDefaults/  Shared telemetry, health checks, resilience, service discovery
  PhotoMapper.ApiService/       Minimal API
  PhotoMapper.Web/              Blazor Web App (interactive server rendering), with the account pages
  PhotoMapper.Data/             EF Core database context, user model and migrations
  PhotoMapper.MigrationService/ Applies database migrations on start, then exits
tests/
  PhotoMapper.IntegrationTests/ xUnit v3 tests that start the whole app once via the AppHost
  PhotoMapper.Web.Tests/        Unit tests for the web app (bUnit for Blazor components)
  PhotoMapper.Data.Tests/       Checks the data model matches the migrations
```

## Build, test, format

```bash
dotnet build PhotoMapper.slnx
dotnet test --solution PhotoMapper.slnx
dotnet format PhotoMapper.slnx
```

CI runs the formatting check, build and tests on every push and pull request to `main`, plus CodeQL, dependency review and a lint of the workflows and shell scripts. Changes reach `main` only through pull requests.

Every pull request also builds the container images and smoke-tests the production Docker Compose stack; every merge to `main` publishes the images to GHCR and a deployment bundle. See [deploy/README.md](deploy/README.md).
