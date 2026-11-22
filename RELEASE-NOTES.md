# Podglow 0.1.3 experimental alpha

Volume adjustments no longer dim unrelated controls. The widget now accepts volume scrolling anywhere over its surface and keeps the scroll direction delivered by Windows or the input driver.

## New in this version

- Keep listening-mode and setting icons steady during brief volume writes. A busy command no longer means the device capabilities have disappeared.
- Retain the last valid shared-state snapshot through a transient read collision, until its heartbeat expires.
- Hold the committed slider value briefly while the host catches up, preventing it from jumping back on release.
- Adjust volume by scrolling anywhere over the widget, with a subtle hint beside Volume. Natural scrolling is not inverted a second time.
- Keep the original Podglow device and listening-mode vectors introduced in 0.1.2.

The native busy-volume regression and main/settings/help navigation passed. The owner confirmed that the installed Island integration no longer flickers and scrolling follows the selected input direction. That local confirmation is not a clean-machine installation test. The standalone installer remains unsigned and experimental.

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
