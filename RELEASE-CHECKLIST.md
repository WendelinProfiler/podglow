# Experimental alpha checklist

## Prepared for 0.1.3-alpha.1

- [x] Preserve complete modified WPods source and required third-party notices.
- [x] Provide one shared native interface for the Windows tray and optional Island host.
- [x] Build the matching managed host and service with the .NET runtime bundled.
- [x] Package hash-checked upstream native dependencies and original USB/IP setup.
- [x] Compile a modern installer with Bluetooth consent, optional startup and uninstall entry.
- [x] Preserve the inherited service recovery and adapter-restoration code; do not change boot security.
- [x] Refuse to overwrite a service belonging to another installation.
- [x] Disable upstream automatic updates in this fork.
- [x] Implement per-pair battery-cache storage with address checks and selection resets.
- [x] Check the staged runtime for required files, developer artifacts, private build paths and credential patterns.
- [x] Include labelled preview screenshots, credits and experimental release notes.
- [x] Add a user-approved updater for this repository, with an opt-out for background checks.
- [x] Check version selection, download validation, cancellation and file locking with synthetic data; verify a real GitHub download without running it.
- [x] Check the updater window layout at 200% scaling and retain the existing installation identity under the Podglow name.

## Not verified by this release

These checks were deferred at the owner's request. Implementation or compilation does not count as passing them.

- [ ] Install, repair, uninstall and recover on clean Windows.
- [ ] Verify actual rollback after interrupted installation or prerequisite failure.
- [ ] Verify a complete installed upgrade, including the 0.1.0 to Podglow rename and administrator handoff.
- [ ] Verify packaged connection, reconnection, listening modes and microphone routing.
- [ ] Verify cold start, sign-in startup and post-restart Bluetooth recovery.
- [ ] Verify battery caches with multiple real AirPods pairs.
- [ ] Verify scaling and positioning across multiple monitors.
- [ ] Test other computers, Bluetooth adapters and AirPods models.

This is an unsigned experimental prerelease. No clean-machine, cross-hardware or installer-recovery compatibility is claimed.
