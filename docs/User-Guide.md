# Windows VPN Console: easy user guide

## Start here

Download the Windows ZIP from the [latest release](https://github.com/esixtosr/Windows-VPN-Console/releases/latest). Extract it and open **Windows-VPN.exe** on Windows 11 x64. There is no installer and no GitHub login is needed. The app needs an existing VPN server and the correct connection details.

A VM needs a working network connection that can reach the VPN gateway. Downloading the app does not connect that VM to any private network by itself.

## What each page does

| Page | Use it for |
|---|---|
| Dashboard | Connect/disconnect the selected profile and read the available status. |
| Connections | Create, edit, import, export, or delete saved profiles. |
| Server Config | Preview configuration examples and checklists for supported servers. Commands are not applied to routers automatically. Review every address and placeholder first. |
| Diagnostics | Read this computer's adapters, routes, DNS details, logs, and available provider evidence. |
| Dependencies | Find installed VPN clients and open their official download pages. |
| Settings | Manage lab/developer modes, log retention, and certificate inventory. |
| About | Read the app version and limitations. |
| Lab 2 / Check-Off | Optional course topology and manual evidence checklists, visible in Lab Mode. |

## Common words in the app

- **Profile:** saved connection settings, such as a gateway and VPN type.
- **Gateway:** the VPN server's hostname or IP address.
- **Engine/provider:** Windows or the external program that creates the tunnel.
- **PSK:** a pre-shared key. Enter it only where the chosen VPN client requires it.
- **Split tunnel:** only specified private destinations use the VPN.
- **Full tunnel:** Internet traffic is also expected to use the VPN.
- **Permitted networks:** destination ranges you expect to reach, such as `10.20.0.0/24`. Ask your administrator for the real values.
- **UNKNOWN:** the app cannot verify that result. It is not an automatic success or failure.

## Create your first profile

1. In **Dependencies**, click **Refresh detection**. Install only the official client your VPN needs.
2. In **Connections**, edit **My VPN** or choose **New profile**.
3. Enter a name, VPN type, engine, gateway, and authentication settings from your administrator.
4. If required, click **Browse configuration** and choose the provided client file.
5. Set the expected split/full policy and private network ranges. For imported or external profiles, the actual VPN settings must also implement that policy.
6. Click **Save profile → Validate → Connect**.
7. Finish any client sign-in. Then test a known private host or service.
8. Click **Diagnostics → Run diagnostics** and compare the observed routes with the expected policy.

The app's profile validator checks the information entered. It does not contact every protected destination, change remote firewall rules, or certify encryption. A green connection indicator is not proof that every service is reachable.

### Windows native VPN

L2TP/IPsec uses Windows with a shared key and MSCHAPv2 user authentication. A PAP-only server is not supported by this adapter. Native IKEv2 uses the Windows sign-in flow; an administrator may need to configure certificate trust and EAP policy separately. Never substitute a different VPN protocol just to make a profile validate.

The app creates temporary profiles with its own identifiers. Disconnect using the console when finished. If Windows or the app crashes, inspect the remaining app-owned Windows VPN entry before removing it.

### OpenVPN and WireGuard

Install the matching official Windows engine first. The OpenVPN adapter uses **OpenVPN Community**, not the separately designed OpenVPN Connect client. The supported OpenVPN import is a self-contained TLS profile; scripts, plugins, external file references, encrypted private-key prompts, and interactive MFA are not handled by this adapter.

WireGuard needs valid keys, addresses, and peer settings. A recent handshake is useful evidence, but it does not prove that every destination works. Imported files can contain secrets: keep the originals secure. Do not publish them on GitHub.

### Shrew Soft and NCP

Configure the actual VPN inside the external program. The console does not transfer its password or PSK to that program.

For Shrew, import a valid `.vpn` file in VPN Access Manager, then enter its exact site name under **Named profile in external client**. Shrew's Windows 11 support is unverified.

For NCP, the console opens the monitor; select the profile and connect there. NCP is separately licensed.

Both providers can remain **UNKNOWN** in the console even when their own windows show a state. Verify the client, routes, and target access. Disconnect in the external client.

## Use the app on several VMs

The same public release ZIP can be downloaded on each Windows VM without signing in to GitHub. Each VM has its own local settings.

To reuse non-secret settings, export a profile as JSON on the first machine and import that JSON on another. Then enter credentials and select any provider configuration file on the new VM. Copying the application folder alone does not copy saved profiles. Windows Credential Manager secrets are not portable JSON exports.

Give each simultaneously active client the identity and address assigned by your administrator. For WireGuard, that normally means a distinct client keypair and registered peer for each VM. Starting copies of the same client identity can disrupt traffic.

## When something goes wrong

| What you see | What to do next |
|---|---|
| Missing dependency | Open Dependencies and install the correct official client. Refresh detection afterward. |
| Administrator rights required | Disconnect/close the app, then start it with Run as administrator for that action. |
| Profile validation error | Read the named field. Confirm the gateway, VPN type, configuration path, and network ranges. |
| Authentication failed | Check the selected authentication method, account, shared key/certificates, and server logs. Do not change every setting at once. |
| Connected, but a private service fails | Check the service itself, client routes, server forwarding/firewall rules, and return routes. |
| UNKNOWN | Read the external client and gather more evidence. Do not mark the result PASS without observation. |
| No ping reply | A host may block ping. Test a known available service; a timeout alone does not prove VPN or firewall behavior. |
| Windows warns about the publisher | The app is unsigned. Check that it came from the official release and compare its checksum. Do not disable Windows security. |

Use **Diagnostics → Run diagnostics → Copy for ChatGPT** to collect a report. Review it before sharing. This copies locally; it does not send data to any service. You can also save an **Export evidence ZIP**.

## Update from an earlier version

Disconnect first, close the old app, and extract the new release to a separate folder. Run **Windows-VPN.exe**. Keep the old folder until you have checked the update. There is no automatic updater.

Version 0.2.0 renames the former CNIT455 VPN Console. The existing local data folder remains `%LOCALAPPDATA%\CNIT455-VPN-Console` for compatibility. Existing profiles, settings, and remembered-secret identifiers remain usable under the same Windows account. Internal Windows profile/service identifiers may also retain the earlier name.

Do not delete original imported VPN files. If their location changes, select the new path in the profile. **Settings → LOCAL DATA** shows where this account's app data is stored.

## Optional lab and practice modes

**Lab Mode** adds the original CNIT 455 topology and checklists. Enable it in Settings and save. In Lab 2, verify addresses before using **Add four lab profiles**. That button adds another set; it does not rewrite existing profiles. Read the [lab guide](Lab2-Guide.md) for course requirements.

**Developer Mode** makes the Mock engine available. Use it to explore success/failure screens without a VPN server. Mock events and routes are simulated and cannot prove a real connection works.

## Privacy and cleanup

Passwords and keys are not stored in ordinary profile JSON. **Remember secrets on this Windows account** is optional and uses Windows Credential Manager. Imported VPN files and provider-managed runtime state may still contain protected secrets.

Before removing the app, disconnect its tunnels and use **Forget secrets** for credentials you no longer want stored. Deleting the EXE folder does not delete the separate local data folder or Credential Manager entries. See [Security](Security.md) for crash cleanup and provider-specific details.
