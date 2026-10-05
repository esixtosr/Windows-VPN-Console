# Contributing

Read README.md and PROJECT_STATUS.md before continuing development. Inspect git status and recent commits. Build and test the current state before changing it. Continue the first incomplete phase; do not restart the repository.

Use .NET 10. Keep platform boundaries in Providers and Diagnostics. Never add a VPN engine, custom cryptography, guessed vendor command, PPTP support, plaintext credential persistence, global firewall/IPsec changes, or commands that kill unrelated processes. Add source links and version applicability to new templates.

Tests must exercise behaviors: routing precedence, capability rejection, malformed imports, secret exclusion, redaction and realistic failure stages. Run the WPF smoke test on Windows. Real server integration needs separate documented VM tests; do not relabel mocks as integration evidence.

Use a branch prefixed `codex/` for follow-up changes. Commit completed phases, update PROJECT_STATUS.md with exact next steps and build/test evidence, and push. Never commit course PDFs, keys, PSKs, passwords, captures, real imported VPN files or exported user evidence. Report vulnerabilities privately to the repository owner instead of putting secrets in an issue.
