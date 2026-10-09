# Contributing

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md) in all project spaces.
Use the [bug report or feature request templates](https://github.com/How-e/SnippyGrab/issues/new/choose)
when opening an issue. Search existing issues first and use synthetic, privacy-safe
evidence. Report security vulnerabilities through the private channel in
[SECURITY.md](SECURITY.md).

Use Windows, .NET 10 SDK and `pwsh ./scripts/provision-ocr.ps1`, then `dotnet restore`, `dotnet build`, `dotnet test`. Use `dotnet format --verify-no-changes` before submitting. Use synthetic screenshots only. Keep OS integration separate from Core. Include tests for lifecycle/geometry changes and manual desktop evidence for capture/dock changes. Do not introduce runtime network services or telemetry. See docs/ACCEPTANCE.md for the desktop matrix.

Interface changes should reuse `Views/Controls.xaml`, `Ui` theme resources and `Icons`; check Light/Dark/System, keyboard focus, high contrast, enlarged text and minimum window sizes. Follow [the desktop checklist](docs/USER-TESTING.md) for affected workflows and reopen relevant entries in `scripts/release-gates.json` when renewed acceptance is needed. Keep detailed test reports, planning notes and personal captures in ignored `artifacts/` or external storage. Commit only user/contributor documentation and synthetic README previews.
