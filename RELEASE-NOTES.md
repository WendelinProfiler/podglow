# Podglow 0.1.5 experimental alpha

This patch fixes an invalid USB-speed value in the Bluetooth proxy. It also includes a manual recovery tool for a virtual USB/IP controller that remains stuck after sleep.

## New in this version

- Bluetooth radios with bulk endpoints are advertised as USB full or high speed, never low speed. The previous translation could cause Windows device error 43 and make Bluetooth disappear.
- A narrowly scoped recovery script checks the installed Podglow service and exact usbip-win2 hardware identity before restarting only the virtual controller. It does not restart Windows or reset physical USB controllers.
- A [Bluetooth recovery guide](docs/BLUETOOTH-RECOVERY.md) explains when updating is enough and when the virtual controller also needs a reset.

Seven speed-policy checks passed. On the development Intel AX211 system, the patched service plus a virtual-controller reset restored Bluetooth, AirPods, ANC status and audio endpoints without a Windows restart. The recovery script passed its read-only target check. Other computers and a full sleep/resume validation of this package remain untested.

The native widget, tray scrolling and user-approved updater are retained. This is an unsigned experimental prerelease.

Read [how updates work](docs/UPDATES.md) before installing.

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
- Multi-monitor, DPI, cold-start and device-switch hardware validation are still open.

See CREDITS.md and licenses/ for upstream authors and required notices. WPods supplies the Bluetooth foundation; Podglow is an independent fork and interface.
