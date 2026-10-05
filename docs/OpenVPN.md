# OpenVPN

The Windows provider uses a separately installed OpenVPN Community executable. Import a trusted `.ovpn` exported for the correct server; its CA, TLS authentication settings and routes must match. Importing an unknown profile is a trust decision because OpenVPN configuration can request privileged behavior. Provider validation explains unsupported automation.

## Client access

The lab client uses UDP to HQ pfSense with AD LDAP authentication and **full** tunneling. A server certificate/CA is still required when the user has no individual client certificate. Choose user authentication or certificate + user authentication according to the server. See [Netgate remote-access setup](https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-ra.html).

Set the [authentication backend](https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-backend.html) after testing LDAP in pfSense. Use trusted TLS to the directory, the correct search/bind identity and an explicit group restriction. Test a permitted and a denied account. [LDAP field reference](https://docs.netgate.com/pfsense/en/latest/usermanager/ldap.html).

Enable redirect-gateway for full tunnel, assign VPN DNS and provide Internet egress NAT for the client pool. Preserve native addresses for private intersite traffic. Reaching pfSense Remote through IPsec also requires pool selectors on both IPsec peers and a return path. Validate effective defaults (including paired `/1` routes), endpoint bypass, DNS and IPv6. [Tunnel settings](https://docs.netgate.com/pfsense/en/latest/vpn/openvpn/configure-server-tunnel.html).

## Site-to-site DMZ service

Use a separate UDP TLS service with pfSense as CA/server and VyOS as certificate-authenticated client. The helper assumes a routed client/server tunnel pool larger than `/30`. On pfSense, configure the VyOS DMZ as a remote network and in a client-specific override matching the VyOS certificate CN. On VyOS, route the pfSense DMZ through the OpenVPN interface. Disable Internet redirection on this DMZ service. [Netgate TLS site-to-site guide](https://docs.netgate.com/pfsense/en/latest/recipes/openvpn-s2s-tls.html).

The generator supplies only documented [VyOS OpenVPN](https://docs.vyos.io/en/1.5/configuration/interfaces/openvpn.html) client/PKI bindings and route/NAT exclusions. Import the pfSense CA and unique client identity first. Align data ciphers and TLS-auth/TLS-crypt with the actual exported profile; these are explicitly left for deployment review. Do not use deprecated static-key mode for the certificate-based lab requirement.

Use `show openvpn client` on VyOS and Status > OpenVPN on pfSense. Confirm bidirectional DMZ connectivity, native addresses and correlated public-side encrypted traffic. Certificate private keys and inline secret blocks must never enter the evidence bundle.

The generated client template uses inline PEM placeholders for CA and optional client certificate/key material, plus an explicit expected server certificate name. Replace these locally before use; the template itself is not a runnable credential set. Lab imports require UDP and auth-user-pass. Explicit unencrypted cipher selections are rejected.
