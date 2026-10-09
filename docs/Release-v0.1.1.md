# CNIT 455 VPN Console v0.1.1

Fixes unreadable dropdown selections and options in the dark Windows interface. This includes VPN type, authentication, tunnel policy, configuration template, and authentication backend fields.

## Download and run

1. Sign in to GitHub with access to this private repository and open this release's **Assets**.
2. Download `CNIT455-VPN-Console-v0.1.1-win-x64.zip` and `SHA256SUMS.txt`. Use the named Windows ZIP, not the **Source code** archives or **Code > Download ZIP**.
3. Compare `Get-FileHash .\CNIT455-VPN-Console-v0.1.1-win-x64.zip -Algorithm SHA256` with `SHA256SUMS.txt`.
4. Extract the ZIP and run `CNIT455-VPN.exe` on Windows 11 x64. No installer, Git, Visual Studio, .NET SDK, or separately installed .NET runtime is required.

GitHub hosts the project history, build checks, and release downloads. The console and its VPN operations run locally; a GitHub login is not needed to run the downloaded app.

## Update from v0.1.0

Disconnect the active VPN, close the console, and extract the new ZIP to a separate folder. Run the new executable. The app has no automatic updater.

Profiles and settings remain under `%LOCALAPPDATA%\CNIT455-VPN-Console` for the same Windows user. Imported provider configuration files remain linked at their original locations. Keep those files in place; if one is inside the old application folder, move it to a secure permanent location and update the profile's configuration path before removing that folder.

## Validation and limits

[Windows validation](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37511988054) passed all **122 tests** and **33 UI assertions** on both normal build output and the extracted self-contained executable. Selected text and open menus were visually reviewed across the affected fields. The tag workflow repeats these checks for this exact release.

See [PROJECT_STATUS.md](https://github.com/esixtosr/Windows-VPN-Console/blob/v0.1.1/PROJECT_STATUS.md) for this patch's validation record and [Acceptance.md](https://github.com/esixtosr/Windows-VPN-Console/blob/v0.1.1/docs/Acceptance.md) for the separate software and live-VM checks.

Live lab VPN handshakes and traffic remain unverified. OpenVPN, WireGuard, Shrew Soft, and NCP require separate official installations where applicable. Shrew/NCP hand off interactively and never equate launching a client with CONNECTED. Shrew's Windows 11 compatibility remains unverified.

The executable is unsigned. No real credentials, proprietary VPN clients, or course PDFs are included.
