[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.Lucide.Runners.Enums.Icons/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/Soenneker.Lucide.Runners.Enums.Icons/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.Lucide.Runners.Enums.Icons/daily-automatic-update.yml?style=for-the-badge&label=Daily%20Update)](https://github.com/soenneker/Soenneker.Lucide.Runners.Enums.Icons/actions/workflows/daily-automatic-update.yml)

# Soenneker.Lucide.Runners.Enums.Icons

Defines the file operations util contract.

> This is an automation runner, not a package intended for application consumption.

## What the runner does

- `IFileOperationsUtil.Process(cancellationToken)` — Processes the pending work managed by the file operations.
- `Constants.TargetRepository` — The target repository.
- `Constants.UpstreamRepositoryUrl` — The upstream repository url.
- `Constants.Library` — The library.
- `Constants.EnumFileName` — The enum file name.

## What you get

- `IFileOperationsUtil` — Defines the file operations util contract.
- `Constants` — Represents the constants.
- `ConsoleHostedService` — Represents the console hosted service.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IFileOperationsUtil.Process(cancellationToken)` | Processes the pending work managed by the file operations. | A task that completes when the full processing workflow has finished. |
| `ConsoleHostedService.StartAsync(cancellationToken)` | Starts the console hosted service and begins its background work. | A task that completes after the console hosted service has started. |
| `ConsoleHostedService.StopAsync(cancellationToken)` | Stops the console hosted service and waits for its background work to finish. | A task that completes after the console hosted service has stopped. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
