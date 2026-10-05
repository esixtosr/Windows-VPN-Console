# Presets

Use **CNIT 455 - Lab 2** in the application to generate all four client profiles from the editable group/topology. The default group number is 33; addresses are derived in Core/LabPresets.cs and are never used as fixed engine endpoints.

`lab2-topology-template.json` is a human-readable template, not an importable connection profile. Its `{x}` tokens identify the group-number substitutions. The app's built-in preset creates typed profiles with the current group and policy.

HQ and Remote public endpoints must be confirmed separately. The specification reuses the VyOS `.4` address and provides an extra pfSense `.6` NAT address; neither proves the physical placement of the new routers. Enter real endpoints before connecting/generating a peer configuration. No secrets or lab PDF are included.
