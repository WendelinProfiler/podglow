# Public alpha release gates

- [x] Keep the complete GPL WPods source and third-party notices.
- [x] Add visible credits to README and the standalone app.
- [x] Separate the Windows tray frontend from Island/Windhawk.
- [x] Build the native standalone host with the shared Island presentation; test main/settings/help/credits navigation locally at200% DPI.
- [x] Remove the known fixed AirPods address and owner-name audio filter; public source scan has no matches for those development identifiers.
- [ ] Persist and restore battery caches per device, never between different pairs.
- [ ] Verify standard Windows startup, exit, reopen, DPI scaling and multiple monitors.
- [ ] Verify connect/disconnect, modes and microphone routing with the public host.
- [ ] Build an installer with explicit driver/restart consent, rollback and uninstall.
- [ ] Preserve signed upstream driver files and matching source/version notices.
- [ ] Scan the actual release payload for keys, addresses, logs, personal preferences and local paths.
- [ ] Test a clean Windows installation without Lenovo software, Island, Windhawk or developer runtimes.
- [ ] Add screenshots of the standalone app; label preview/synthetic data clearly.
- [ ] Publish as experimental, with tested hardware and known limitations.

No clean-machine, non-Lenovo or public-installer compatibility is claimed before these checks pass.
