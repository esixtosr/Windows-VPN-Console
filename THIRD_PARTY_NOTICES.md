# Third-party notices

The application uses the .NET 10 runtime, WPF and standard .NET libraries from Microsoft/.NET Foundation, distributed under the MIT license and associated third-party notices. Self-contained releases retain the runtime license and notice files produced by `dotnet publish` where supplied. The .NET MIT license is also checked in under licenses/ and copied into Docs/RuntimeNotices in releases. Official sources: https://github.com/dotnet/runtime and https://github.com/dotnet/wpf.

Test-only NuGet packages (not shipped with the app):

- Microsoft.NET.Test.Sdk 17.14.1 — MIT; https://github.com/microsoft/vstest
- xunit 2.9.3 — Apache-2.0; https://github.com/xunit/xunit
- xunit.runner.visualstudio 3.1.5 — Apache-2.0; https://github.com/xunit/visualstudio.xunit

External engines are detected and invoked only when installed by the user: OpenVPN Community, WireGuard for Windows, Shrew Soft VPN Client and NCP Secure Entry Client. They are **not bundled**, relicensed or copied into this repository. NCP is commercial software requiring a valid license. Their names belong to their respective owners.

Configuration templates are independently written from linked official documentation; see docs/Sources.md and docs/Providers-Research.md. No vendor source code or course PDF has been copied into the product.
