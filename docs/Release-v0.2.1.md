# Windows VPN Console v0.2.1

A cleaner interface with a new shield icon, clearer controls and better network-status explanations.

## Download and run

1. Download **Windows-VPN-Console-v0.2.1-win-x64.zip** from this release's Assets.
2. Right-click the ZIP and choose **Extract All**.
3. Open **Windows-VPN.exe** on Windows 11 x64.

No installer, SDK or separate .NET installation is required. The app is unsigned; verify the source and checksum if Windows asks about its publisher. Do not turn off Windows security protections.

## What's changed

- New app icon, navigation icons and readable dark dropdowns.
- Smaller dashboard and responsive forms, with advanced details and the activity log tucked away until needed.
- Clear **Open NCP / Open Shrew** buttons. Those clients still own their profiles, credentials and connection controls.
- Observed Windows addresses and private-network routes are shown separately from the VPN engine's reported status.
- Fixed profile switching and menu refreshes that could replace the selected engine.
- Preserved existing saved profiles, settings and remembered-credential identifiers.

## Updating

Disconnect and close the old console, extract this ZIP to a new folder, and open the new **Windows-VPN.exe** using the same Windows account. Keep your imported VPN configuration files in their original locations. Keep the previous app as a rollback copy. There is no automatic updater.

## Validation and limits

[The v0.2.1 acceptance build](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/38009185217) passed **138 Windows tests** and **44 interface checks** on both the normal build and extracted portable app. The tagged release workflow repeats these checks before publishing its ZIP and **SHA256SUMS.txt**.

The interface tests simulate VPN traffic. Real server authentication, internal-host access and encryption require checks against your own environment. External engines are not bundled; NCP still requires its own license. This update does not change router or NCP settings.
