# Windows VPN Console

A free, portable tool for managing VPN profiles and checking network settings on Windows 11 x64.

Use it to save connections, start supported VPN clients, inspect routes, and collect a troubleshooting report. It connects to a VPN server you already have. It does not provide a VPN service or automatically monitor every machine in your network.

**[Download the latest Windows app](https://github.com/esixtosr/Windows-VPN-Console/releases/latest)** · [Easy user guide](docs/User-Guide.md) · [Troubleshooting](docs/User-Guide.md#when-something-goes-wrong)

The development branch includes the **0.2.1 interface preview**: a new app icon, dark dropdowns, a compact dashboard and clearer external-client actions. The latest published stable release remains v0.2.0. In the preview, **VPN engines** replaces the Dependencies label and **Check settings** replaces Validate. See [preview startup instructions](docs/Start-Here.txt).

## 1. Download and open it

1. Open the download link above on your Windows VM or PC. No GitHub account is needed.
2. Under **Assets**, download **Windows-VPN-Console-v0.2.0-win-x64.zip**, or the Windows ZIP listed in the newest release.
3. Right-click the ZIP and choose **Extract All**.
4. Open the extracted folder and run **Windows-VPN.exe**.

That is the installation: extract and run. You do not need Git, Visual Studio, or a separate .NET installation. You can use the same release ZIP on multiple Windows VMs. Each VM keeps its own profiles and credentials.

![Windows VPN Console interface preview starting in general-purpose mode](docs/images/dashboard.png)

On first start, **My VPN** is a blank profile. Fill in your connection details before connecting; **Not checked** (UNKNOWN in v0.2.0) is normal until status can be checked.

Choose the named Windows ZIP, not **Source code** or **Code → Download ZIP**. Those contain development files. The app runs on Windows; there is no native Mac or Linux app.

The executable is unsigned, so Windows may show an unknown-publisher message. Download from this repository and check the checksum if needed; do not turn off Windows security protections.

## 2. Have your VPN details ready

Your VPN administrator or your own VPN server must supply:

- The VPN type, such as L2TP/IPsec, IKEv2, OpenVPN, or WireGuard.
- The server address, also called the **gateway**.
- The required username/password, shared key, certificates, or configuration file.
- Which private networks you should be able to reach.

Do not guess these values. A saved profile alone cannot create a working VPN server.

## 3. Set up a connection

1. Open **VPN engines** (Dependencies in v0.2.0) and click **Refresh detection**. Install the required official client if it is missing.
2. Open **Connections**. Edit **My VPN**, or click **New profile**.
3. Give it a useful name, such as **Office VPN** or **Test VM VPN**.
4. Choose the **VPN type** and matching **VPN engine**.
5. Enter the gateway and the settings provided by your administrator. For OpenVPN or WireGuard, use **Browse configuration** to select a supported configuration file.
6. Choose the expected tunnel policy: **Split** sends selected private networks through the VPN; **Full** sends Internet traffic through it too. Enter the permitted networks if required. External client settings must match this expectation.
7. Click **Save profile**, then **Check settings** (Validate in v0.2.0).
8. Click **Connect**, or **Open NCP / Open Shrew** for those clients. Complete sign-in inside the client that owns the connection.
9. Use **Dashboard → Check connection**, then open a known internal service to confirm access. Checking profile fields does not prove the VPN works.

Some engine actions need administrator rights. If the app reports that requirement, close it and use **Run as administrator** for that action.

## Which VPN engine do I need?

| VPN type | What to use | What to know |
|---|---|---|
| L2TP/IPsec | Windows native VPN | Built into Windows. This adapter uses PSK and MSCHAPv2. |
| IKEv2 or SSTP | Windows native VPN | Certificate trust/EAP settings may need administrator setup. |
| OpenVPN | OpenVPN Community | Install separately and import a supported `.ovpn` file. OpenVPN Connect is a different client. |
| WireGuard | WireGuard for Windows | Install separately; use a supported configuration. Service changes need administrator rights. |
| External IPsec | Compatible Shrew Soft or NCP client | Configure, sign in, and disconnect inside that client. NCP requires its own license; Shrew's Windows 11 compatibility is unverified. |
| Mock | Built-in simulation | Available in Developer Mode for UI practice. It does not create a real VPN. |

Use **VPN engines → Open official download** for vendor links. These clients are not bundled with the app. Detailed restrictions are in the [user guide](docs/User-Guide.md) and [provider notes](docs/Providers-Research.md).

## Check a connection or get help

Open **Diagnostics → Run diagnostics** to collect local routes, adapters, DNS settings, and available VPN status. It does not test every remote machine.

**UNKNOWN** means the app does not have enough evidence. In particular, opening Shrew/NCP does not mean a tunnel connected. Check the external client and a real destination.

Use **Copy for ChatGPT** to copy a report, or **Export evidence ZIP** to save it. Copying does not upload anything. Reports hide recognized secrets, but addresses, usernames, and hostnames can remain. Read the report before sharing it, and never post passwords, private keys, or original secret-bearing VPN files in a public issue.

## Update or move to another VM

There is no automatic updater. Disconnect, close the old app, download the new Windows ZIP, and extract it to a new folder. Then run the new `Windows-VPN.exe`.

Existing users of **CNIT455 VPN Console v0.1.x** keep their saved profiles and settings when using the same Windows account. The old data-folder and credential identifiers remain for compatibility. Imported VPN files must stay at their recorded paths, or you must select their new locations.

For another VM, copy the release ZIP and extract it there. To reuse a profile, use **Connections → More profile actions → Export profile**, then **Import profile** on the other VM (Export JSON / Import JSON in v0.2.0). Exported JSON does not contain passwords, PSKs, or private keys, and its imported-file path is cleared. Enter credentials and reselect the original configuration file on each VM. Do not share a WireGuard identity across active machines unless your VPN administrator explicitly designed that setup.

## Optional lab mode

New installations open in general-purpose mode. For the original CNIT 455 exercises, enable **Settings → Lab Mode**, save settings, and open **Lab 2**. Confirm the topology before adding lab profiles. Existing profiles are not rewritten when you change the group number. [Lab instructions](docs/Lab2-Guide.md)

## Verify a download

`SHA256SUMS.txt` contains the expected fingerprint of the release ZIP. In PowerShell, from your download folder:

```powershell
Get-FileHash .\Windows-VPN-Console-v0.2.0-win-x64.zip -Algorithm SHA256
```

Compare it with the matching entry in `SHA256SUMS.txt`. The values should match; capitalization does not matter. This checks file integrity, not whether a VPN connection works.

## What has been tested?

[Windows validation for the interface preview](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/38004040500) passed **138 tests** and **44 UI checks** on both the normal build and extracted portable app. Windows-rendered screens were inspected, and profile switching now has regression coverage so refreshing the editor does not replace the selected engine. This preview is on `codex/interface-polish`; it is not yet the stable download.

[Windows validation for v0.2.0](https://github.com/esixtosr/Windows-VPN-Console/actions/runs/37878883551) passed **122 tests** and **34 UI checks** on both the normal build and extracted portable app. This includes real native profile create/remove behavior, Windows Credential Manager, and the new general-purpose starting screen. The [acceptance record](docs/Acceptance.md) separates verified software behavior from live VPN checks. External client compatibility, real server authentication, routing, and encryption still need testing against your own environment.

## More information

- [Easy user guide](docs/User-Guide.md): page-by-page help, errors, updates, and privacy.
- [Security and local storage](docs/Security.md).
- [Project status and validation](PROJECT_STATUS.md).
- [Engine capability matrix](docs/Engine-Capability-Matrix.md).
- [Authentication compatibility matrix](docs/Authentication-Compatibility-Matrix.md).
- [v1.0.0 implementation plan](docs/Release-v1.0.0-Plan.md).
- [Build and contribute](CONTRIBUTING.md).

GitHub holds the source code, build checks, and release downloads. The application and your VPN connections run locally on your computer.

MIT-licensed independent utility. Not affiliated with Microsoft, Purdue University, VyOS, Netgate/pfSense, OpenVPN, WireGuard, NCP, or Shrew Soft. External clients retain their own licenses. No course PDFs, VPN services, or commercial client binaries are included.
