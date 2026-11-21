# Development

## Build the widget

Use Windows x64 with Visual Studio Build Tools, MSVC and the Windows SDK. Building the modified WPods backend also requires .NET 10.

```powershell
./scripts/Build-NativeWidget.ps1
out/native-widget/AirPodsCompanion.exe --preview
```

`--preview` uses labelled sample data and blocks device commands. `--widget-only` reads an existing compatible host without starting an engine or installing drivers. Normal startup looks for `engine/IslandAirPodsHost.exe` beside the widget.

The public build contains no Apple fonts, extracted system glyphs or copied product images. Separately held local assets are excluded from the repository and public package.

## Shared presentation

`src/Shared/AirPodsPanel.h` owns the layout, controls, hit regions, feedback and animations. `src/NativeWidget/` hosts that presentation as a Windows tray app. The optional Island adapter includes the same header.

The control window and state mappings retain their legacy `IslandAirPods` identifiers for compatibility. They refer to the AirPods host; the standalone frontend does not launch or require Windhawk.

Stock WPods does not publish this companion protocol. A matching modified host is required. Complete modified source is in `engine/`, together with its build instructions. Avoid running two AirPods controllers at the same time.

## Verification

```powershell
./scripts/Test-NativeWidget.ps1
```

This exercises local preview navigation. It does not prove Bluetooth operation, clean-machine compatibility or installer recovery. Hardware and packaging checks are tracked in the [release checklist](../RELEASE-CHECKLIST.md).

## Screenshots

The README images come from the public native preview. They contain sample readings, with no desktop capture or personal device data. `scripts/Save-SharedWidgetRender.ps1` saves only the app's own rendered window using `PrintWindow`. Start the preview before running it.

Keep preview labels visible. Replace these images with verified release screenshots when the matching public host and installer have passed testing.
