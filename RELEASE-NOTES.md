# Podglow 0.1.4 experimental alpha

Hover over the Podglow earbud icon in the normal Windows notification area and scroll to adjust volume. The widget stays closed. Windhawk is not required.

## New in this version

- Volume scrolling over the visible notification icon, including the open overflow panel. Windows/device scroll direction is preserved.
- Small wheel deltas accumulate before changing volume, rather than treating each touchpad event as a full wheel notch.
- A tooltip reminder: Scroll to adjust volume.
- Original Podglow earbud vectors replace the generic Windows application icon, with blue for connected and gray for disconnected.

A dedicated input thread checks only vertical wheel events over the current, visible shell icon rectangle. Other icons, covered windows and stale icon positions are excluded. No mouse history is saved and nothing is injected into Explorer.

Nine routing checks passed. The native integration test verifies that the hook starts, volume commands follow both directions, fractional input accumulates and scrolling keeps the widget closed. These are local automated checks, not a full manual test on stock Windows taskbars across other computers. Clean-machine installation remains untested. This is an unsigned experimental prerelease.

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
