# 0.1.0 experimental alpha

This is the first packaged build of AirPods Companion. It includes the native Windows tray widget, the matching modified WPods host and service, the .NET runtime and the original usbip-win2 installer.

The package has been compiled and checked locally. Installation, uninstall, rollback and operation on a clean or different Windows computer have not been tested. Bluetooth hardware behavior with this packaged build is unverified. Use it only if you are comfortable troubleshooting an experimental Bluetooth tool.

## Installation

1. Save your work and keep a wired keyboard or touchpad available. Setting up the Bluetooth service can briefly disconnect Bluetooth devices.
2. Run `AirPodsCompanion-Setup.exe`. Choose whether to start the widget when you sign in.
3. Read and accept the Bluetooth setup page. The installer needs administrator permission for the service and signed upstream driver.
4. If a restart is required, finish your work and restart later. Setup does not change Secure Boot or Windows Test Mode.
5. Pair your AirPods in Windows Bluetooth settings, then open AirPods Companion from Start or the notification area.

An existing WPods service belonging to another installation must be removed through that installation first. The alpha does not take over or silently replace it.

The app and installer are not Authenticode-signed. Windows may show an unknown-publisher or SmartScreen warning. The included upstream USB/IP dependency is preserved unmodified; this does not sign our own executables.

## Removal and recovery

Use Windows Settings > Apps > Installed apps > AirPods Companion > Uninstall. The inherited WPods removal path attempts to restore the Bluetooth adapter before removing the USB/IP dependency. Keep settings unless you intentionally want to remove them. Recovery remains untested with this alpha package.

If Bluetooth is unavailable, use a touchpad or wired input to rerun Setup, then uninstall. Do not manually delete service or driver files. If the adapter cannot be restored, keep the USB/IP dependency installed and report the failure.

## Known limits

- Development hardware was one Windows 11 x64 Lenovo with AirPods Pro 3. Other models and adapters are unverified. The Bluetooth engine requires a USB-connected Bluetooth adapter.
- Advanced controls require the matching host and a working service. Preview mode uses sample data and never controls a device.
- Battery reports depend on what the AirPods send. Older readings remain labelled as cached.
- Apple fonts, extracted Apple glyphs and private product artwork are excluded. Some icon positions in the public interface are intentionally empty.
- Automatic updates are disabled. Install future companion releases manually.
- Multi-monitor, DPI, cold-start and device-switch hardware validation are still open.

See CREDITS.md and licenses/ for upstream authors and required notices. WPods supplies the Bluetooth foundation; AirPods Companion is an independent fork and interface.
