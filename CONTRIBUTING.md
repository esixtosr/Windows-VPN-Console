# Contributing

Windows VPN Console uses .NET 10 and WPF. Existing solution/project namespace names retain `CNIT455` to keep the rebrand small; the downloadable executable is `Windows-VPN.exe`.

On Windows with the .NET 10 SDK installed:

```powershell
dotnet restore CNIT455-VPN-Console.sln
dotnet build CNIT455-VPN-Console.sln -c Release --no-restore
dotnet test tests/CNIT455.VPN.Tests -c Release --no-build
powershell -File scripts/Publish.ps1 -Version v0.2.1
```

The app's `--smoke-test <report-path>` mode runs isolated UI checks with simulated VPN traffic. CI runs it for both the normal build and the extracted portable package. macOS/Linux can build reusable libraries and cross-compile WPF with `EnableWindowsTargeting`; only Windows can validate this desktop runtime.

Read README.md and PROJECT_STATUS.md before continuing development. Inspect git status and recent commits. Build and test the current state before changing it. Continue the first incomplete phase; do not restart the repository.

Use .NET 10. Keep platform boundaries in Providers and Diagnostics. Never add a VPN engine, custom cryptography, guessed vendor command, PPTP support, plaintext credential persistence, global firewall/IPsec changes, or commands that kill unrelated processes. Add source links and version applicability to new templates.

Tests must exercise behaviors: routing precedence, capability rejection, malformed imports, secret exclusion, redaction and realistic failure stages. Run the WPF smoke test on Windows. Real server integration needs separate documented VM tests; do not relabel mocks as integration evidence.

Use a branch prefixed `codex/` for follow-up changes. Commit completed phases, update PROJECT_STATUS.md with exact next steps and build/test evidence, and push. Never commit course PDFs, keys, PSKs, passwords, captures, real imported VPN files or exported user evidence. Report vulnerabilities privately to the repository owner instead of putting secrets in an issue.
