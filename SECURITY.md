# Security
Report vulnerabilities privately using GitHub private vulnerability reporting when enabled; do not attach private screenshots to public issues. Supported development branch: main, 0.1.x alpha.

No accounts, telemetry, uploads, listeners, update polling or screenshot network requests. Images and OCR text are sensitive. Use synthetic assets in reports. The application does not log image contents, window titles or OCR text. See [threat model](docs/THREAT-MODEL.md).

Build-time NuGet/model downloads are the only network dependencies. Release signing keys belong in a protected external signing service or repository secrets, never source control. Enable GitHub secret scanning and private reporting in repository settings after publication.
