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
