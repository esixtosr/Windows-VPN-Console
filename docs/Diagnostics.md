# Diagnostics and evidence

Use Connect, reproduce the issue, then Test/Collect. Copy for ChatGPT produces a plain-text report with version, provider, policy, gateway, adapters, DNS, routes, logs, server output and suggested next checks. Pasted server output is sanitized. Do not paste unrelated configuration or account records.

A successful ping only proves an ICMP response. A timeout is UNKNOWN, because a gateway may block echo while accepting VPN traffic. Sending a UDP packet does not show that the destination port is open. Tunnel status comes from the provider; externally controlled products may remain UNKNOWN.

IPv4 route validation identifies the tunnel from provider interface/address observations, then compares actual routes. Split policy needs private routes on the tunnel and local default routing. Full policy accepts the VPN default or OpenVPN-style /1 pair, using route plus interface metrics. More-specific routes can affect individual destinations; IPv6, DNS policy and public gateway bypass need separate checks. The app never marks an unknown interface as a successful tunnel.

The analyzer separates transport, authentication, routing and DNS evidence. Firewall/return-path failures cannot generally be distinguished from client-only timeouts, so those suggestions are qualified. Local Test success followed by RADIUS failure points toward remote authentication, not a reason to rebuild the transport.

Checkoff results begin UNKNOWN. Manual PASS/FAIL choices are user attestations and should include an observation, screenshot reference or capture note. Exported ZIPs contain summary.txt, diagnostics.txt, routes.txt, interfaces.txt, vpn-status.txt, redacted-log.txt, checkoff-results.json and versions.txt. No capture is included automatically. Review the ZIP before sharing it.

For encryption checkoff, capture on the public interface and demonstrate the expected VPN transport plus absence of visible target cleartext. Keep payloads short and intentional. Use the generated capture/filter guidance; privileged capture is never launched silently.
