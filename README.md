# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Documentation

Detailed, up-to-date information about the project lives in [`info/`](info/):

- [Current Status](info/current-status.md) — what's implemented today versus what's still planned.
- [Architecture](info/architecture.md) — the solution's layers and projects, and how it's deployed
  locally (Docker Compose) and in production (Azure Container Apps).
- [Functionality](info/functionality.md) — a detailed description of every implemented feature:
  authentication, notes and tasks (including sharing them with another user), calendar (including
  reminders and event sharing), contacts and end-to-end-encrypted chat, the dashboard, and push
  notifications.
- [Testing and Running Locally](info/testing-and-running-locally.md) — automated test coverage, and
  how to build and run the whole stack, including from another device on your network.
- [Future Plan](info/future-plan.md) — planned features, known first-version scope cuts, and testing
  gaps.

Two additional setup guides also live in `info/`:
[`build.md`](info/build.md) (full machine setup and first build) and
[`instructions.md`](info/instructions.md) (trusting the local TLS certificate in Chrome on Windows).

## License

All Rights Reserved — see [LICENSE](LICENSE).
