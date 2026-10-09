# Windows VPN Console v0.2.0

The former CNIT455 VPN Console is now **Windows VPN Console**, with a general-purpose starting screen and a public GitHub repository.

- Download without signing in to GitHub, including from fresh Windows VMs.
- Extract `Windows-VPN-Console-v0.2.0-win-x64.zip` and run `Windows-VPN.exe`.
- No installer, Git, SDK, or separately installed .NET runtime is required.
- New users start with a blank general-purpose profile. The course-specific topology remains an optional Lab Mode.
- Existing saved profiles, settings, and remembered credentials retain their storage identifiers for compatibility.
- The README and bundled user guide explain setup, multiple VMs, common errors, updates, and checksums in plain language.

## Updating

Disconnect, close the old app, and extract this ZIP to a new folder. Run `Windows-VPN.exe`. Keep imported VPN files in their original locations. There is no automatic updater.

## Validation and limitations

Windows build/test and portable UI acceptance are required before this release is published. See the [acceptance record](https://github.com/esixtosr/Windows-VPN-Console/blob/v0.2.0/docs/Acceptance.md) for results.

Real VPN server connectivity still depends on your own server, credentials, client software, routes, and firewall rules. External engines are installed separately. Shrew/NCP use interactive handoff and never count a launched window as CONNECTED. The EXE is unsigned.
