# AirPods Companion

An experimental Windows tray widget for AirPods, built on [WPods](https://github.com/FPNDev/wpods).

**This project is being prepared for its first public alpha. The standalone preview and credits have been tested locally; there is no tested public installer yet.** Do not treat the development build as a finished release.

## A normal Windows app

The widget opens from the Windows notification area. It does not replace the taskbar and does not require Windhawk, Mac Island & Dock, a Lenovo computer or a particular Windows account. If you use an Island that shows tray applications, it can display the same app icon.

The companion shows the selected AirPods' connection, left/right/case battery values and measurement age. It provides listening modes, volume, connect/disconnect, microphone mute and automatic microphone selection. Less common controls live behind the settings button.

## Preview

These are sample readings, not live measurements. The public build uses the same layout as the Island version and contains no private Apple product assets.

<img src="docs/screenshots/widget-preview.png" width="380" alt="Standalone widget preview with sample battery values">
<img src="docs/screenshots/settings-preview.png" width="380" alt="AirPods settings in the shared native widget">

## What's experimental

- Hardware validation so far is limited to one Lenovo Windows 11 x64 computer and AirPods Pro 3. Other computers, Bluetooth adapters and AirPods models need testing.
- A charging-case value is refreshed when a valid report arrives. Cached values are marked as last seen; opening a case does not justify inventing a fresh timestamp.
- Advanced controls require the WPods service and its driver setup. On a new PC, that setup may require a user-approved restart. The widget never restarts Windows automatically.
- The public version is being separated from a private prototype. Cross-device cache behavior, the complete installer and clean-machine recovery are release gates, not claims of completed testing.

## Build

Requirements: Windows x64 and Visual Studio Build Tools with MSVC and the Windows SDK. Building the optional backend also needs .NET 10.

```powershell
./scripts/Build-NativeWidget.ps1
out/native-widget/AirPodsCompanion.exe --preview
```

The default public build does not include Apple fonts, copied product images or extracted system glyphs. Its layout still uses the shared renderer. The local development preview can load separately held assets; those assets are not included in this repository or a public release.

`--preview` uses clearly labelled sample data and never sends AirPods commands. `--widget-only` reads an already running compatible local host without starting an engine or installing a driver. Normal startup looks for `engine/IslandAirPodsHost.exe` beside the widget.

## One interface, two hosts

`src/Shared/AirPodsPanel.h` owns the AirPods layout, controls, hit regions, feedback and animations. The native standalone adapter in `src/NativeWidget/` hosts that same presentation above the normal Windows taskbar. The Island build adapter includes the same header. There is no separate WinForms redesign of the widget.

The stock WPods app does not publish this companion protocol; a matching modified host is required. The end-to-end installation flow is still being packaged. Do not run two AirPods controllers at once.

The modified WPods engine source is in `engine/`. Its native build instructions and third-party notices remain there. A public installer will package a matching host and dependencies; copying unknown DLLs from a user's machine is not the release process.

## Local protocol compatibility

The local control window and state mappings retain the legacy `IslandAirPods` names for compatibility with the existing prototype. Those names refer to the AirPods host, not to an Island process. The standalone frontend does not find, launch, modify or require Windhawk.

## Credits and license

The Bluetooth work comes primarily from **WPods and its contributors**. See [CREDITS.md](CREDITS.md) for the repositories, authors, versions and what each contributed. The same credits are available from the widget's settings and tray menu.

This fork is GPL-3.0. See [LICENSE](LICENSE) and the component notices in [licenses/](licenses/). Apple fonts and the private prototype's original Apple glyphs are not included in the public package or covered by the GPL.
