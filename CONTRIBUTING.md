# Contributing

Use Windows, .NET 10 SDK and `pwsh ./scripts/provision-ocr.ps1`, then `dotnet restore`, `dotnet build`, `dotnet test`. Use `dotnet format --verify-no-changes` before submitting. Use synthetic screenshots only. Keep OS integration separate from Core. Include tests for lifecycle/geometry changes and manual desktop evidence for capture/dock changes. Do not introduce runtime network services or telemetry. See docs/ACCEPTANCE.md for the desktop matrix.
