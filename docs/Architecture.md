# Architecture

The public product is Windows VPN Console and its executable is `Windows-VPN.exe`. Internal solution/project namespaces, data directory, Credential Manager target prefix and provider-owned identifiers retain the original `CNIT455` naming for compatibility. New user settings default to generic mode; persisted settings take precedence. Lab profiles are created only when lab mode is active or the user explicitly adds them.

`CNIT455.VPN.App` is the WPF composition root and MVVM user interface. `Core` contains profile models, interfaces, validation, topology, JSON persistence, secret redaction and safe process execution. `Providers` adapts vendor engines and Windows APIs. `Diagnostics` collects observations, analyzes routes/failure stages, formats sanitized reports and exports evidence. `ConfigGenerators` owns reviewed server templates, client text generation, PSKs and checkoff definitions. Tests reference the reusable projects and run independently of WPF.

The UI injects a shared redactor and provider registry. Secrets live in a separate VpnSecrets object whose fields are excluded from JSON serialization. Optional persistence is behind ISecretStore. Every provider exposes capabilities, installation detection, validation, lifecycle, status and logging through IVpnProvider. Unsupported operations return an explicit limitation; missing engines never prevent unrelated features from operating.

External processes use an absolute detected executable path and argument arrays, without a shell. Short tasks have bounded output and a cancellation deadline. Long-lived engine processes remain owned by their adapter; disconnect only targets the app-owned session. Windows PowerShell scripts are fixed code and input data is passed separately.

Diagnostic conclusions distinguish observations from expected policy. IPv4 route analysis uses prefix length, combined metrics, tunnel identity and route boundaries across a target prefix. Equal-cost choices across tunnel/local interfaces remain unknown. A working route cannot prove reachability, authorization, NAT exemption, authentication or encryption.

Security boundaries, privileged actions and provider-managed persistence are documented in Security.md. Developer mocks are intentionally separate from actual network evidence.
