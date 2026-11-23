# Podglow 0.1.9 experimental alpha

Fixes the in-app update handoff on Windows. A downloaded installer previously
remained open for writing while Windows tried to execute it, causing sharing
violation 32 after a successful download and checksum verification.

The updater now reopens the file read-only, verifies its size and SHA-256 again
under that final lock, and keeps the lock through launch. The second verification
rejects replacement between the download handle and the final handle. Installer
launch errors are reported separately from download or verification failures.

44 updater regression checks pass, including launching a harmless Windows image
while the final download lock remains held. The previous public installer also
passed a live GitHub download, redirect, size and SHA-256 check. No installer was
executed by these tests. Clean-machine update installation remains unverified.

Users affected by the old updater must download and start Podglow-Setup.exe from
this release manually once; the old running updater cannot repair its own lock.
The stereo-call and optional GlideMouse integration changes from 0.1.8 are included.

---
# Podglow 0.1.8 experimental alpha

This release includes the matching Bluetooth-service support for GlideMouse 0.2.0
and its optional Mac companion. Normal AirPods operation remains available without
the mouse add-on, which must be explicitly configured for one mouse and owner.

- Automatic microphone mode routes supported AirPods to the virtual A2DP microphone and call output. HFP changes rebuild the audio link to avoid a stale communications endpoint. Older models without a virtual microphone retain HFP.
- Windows-owned mouse authentication is separated from the direct HID input channels.
- Channel negotiation, touch-mode activation and handoff release are bounded and scoped
  to the configured device. A pointer-only connection is not reported as full touch readiness.
- Input report 0x90 supplies real USB-C Magic Mouse charge, decoded after its power flags.
  Unsupported/truncated/unknown battery responses do not disable touch or invent a percentage.
- The latest valid report is published to the configured owner's existing mouse profile.
  Battery refresh runs outside the input/ACL lock and does not reconnect the device.
- Includes the prior stable connection feedback during volume changes and Bluetooth startup fixes.

Runtime hardware validation used one Windows 11 x64 Intel USB adapter, an Apple-silicon
Mac and USB-C Magic Mouse. The mouse supplied a real 99% report and the Island showed
that value. Bidirectional handoff, automatic Windows takeover after Mac shutdown and
Mac takeover with Windows's helper stopped and SSH closed were confirmed on that pair.
No universally successful twenty-switch series, clean-machine install, actual Windows
power-off round trip, other adapters or all sleep states are claimed.

The installer and payload checks do not replace clean-machine installation testing.
The companion is an independent optional download in GlideMouse's release. The full
Windhawk Island mod, personal settings, device identities and development logs are not
part of this Podglow installer. See the existing installation/recovery guidance below.

---
# Podglow 0.1.7 experimental alpha

Changing volume no longer makes the connection button flash between Disconnect and Please wait. The connection indicator now shows activity only while connecting or disconnecting. Volume, listening modes and other settings keep their own command feedback; command serialization remains in place.

This update includes the Bluetooth startup and GlideMouse coexistence fixes from 0.1.6. It does not change pairing behavior or add Magic Mouse switching between computers.

The host and installer were built locally. Regression checks cover connection activity during volume changes and real connection commands. Clean-machine installation and broader hardware validation remain untested.

---
# Podglow 0.1.6 experimental alpha

This patch fixes the startup path that could leave Bluetooth unavailable after a Windows restart, or leave a connected Magic Mouse without momentum and gestures when used with GlideMouse.

## What changed

- Intel USB Bluetooth hardware is initialized through its installed vendor driver before Podglow takes it over. A volatile marker makes this a once-per-Windows-boot step rather than repeating it on every service restart.
- The service reports Ready only after Windows starts the virtual radio and the radio returns its HCI version. Opening a USB/IP socket is no longer enough.
- A failed or missing virtual radio is detected. Recovery is bounded and only rebuilds the dedicated virtual controller after verifying that no USB/IP imports remain. A successful empty port listing is handled correctly.
- When an already scoped USBPcap filter is present, the physical Bluetooth USB port is re-enumerated after its function-driver change. This restores the child filter that GlideMouse needs. No capture filter is installed on unrelated hubs.
- The virtual microphone waits for radio readiness. Existing listening modes, tray controls and the user-approved updater remain available.

Use **GlideMouse 0.1.1 alpha or newer** for the matching installer discovery and mouse-filter ownership fix. See [Bluetooth startup and GlideMouse](docs/BLUETOOTH-STARTUP.md).

The runtime fixes were verified on one Windows 11 x64 Intel Bluetooth development system after a full restart. Both filter stacks and fresh mouse touch reports were checked, and the user confirmed momentum, gestures and AirPods worked together. The public installer has been built and checked, but not installed on a clean second computer. Other adapters and sleep/resume remain unverified.

This is an experimental prerelease. Read [how updates work](docs/UPDATES.md) before installing.

## Installation

1. Save your work and keep a wired keyboard or touchpad available. Setting up the Bluetooth service can briefly disconnect Bluetooth devices.
2. Run `Podglow-Setup.exe`. Choose whether to start the widget when you sign in.
3. Read and accept the Bluetooth setup page. The installer needs administrator permission for the service and signed upstream driver.
4. If a restart is required, finish your work and restart later. Setup does not change Secure Boot or Windows Test Mode.
5. Pair your AirPods in Windows Bluetooth settings, then open Podglow from Start or the notification area.

An existing WPods service belonging to another installation must be removed through that installation first. The alpha does not take over or silently replace it.

The app and installer are not Authenticode-signed. Windows may show an unknown-publisher or SmartScreen warning. The included upstream USB/IP dependency is preserved unmodified; this does not sign our own executables.

## Removal and recovery

Use Windows Settings > Apps > Installed apps > Podglow > Uninstall. The inherited WPods removal path attempts to restore the Bluetooth adapter before removing the USB/IP dependency. Keep settings unless you intentionally want to remove them. Recovery remains untested with this alpha package.

If Bluetooth is unavailable, use a touchpad or wired input to rerun Setup, then uninstall. Do not manually delete service or driver files. If the adapter cannot be restored, keep the USB/IP dependency installed and report the failure.

## Known limits

- Development hardware was one Windows 11 x64 Lenovo with AirPods Pro 3. Other models and adapters are unverified. The Bluetooth engine requires a USB-connected Bluetooth adapter.
- Advanced controls require the matching host and a working service. Preview mode uses sample data and never controls a device.
- Battery reports depend on what the AirPods send. Older readings remain labelled as cached.
- Apple fonts, extracted Apple glyphs and private product artwork are excluded. The public interface includes original Podglow device and listening-mode vectors.
- WPods' upstream automatic installer stays disabled. Podglow checks only its own repository and asks before downloading or installing.
- Multi-monitor, DPI, other Bluetooth adapters and broader sleep/resume validation remain open.

See CREDITS.md and licenses/ for upstream authors and required notices. WPods supplies the Bluetooth foundation; Podglow is an independent fork and interface.
