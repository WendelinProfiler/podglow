# Credits

Podglow is an experimental companion UI built on other people's work. The Bluetooth engine is a modified WPods build, not a new Bluetooth driver written by this project.

| Project | Authors | What we use | License / source |
| --- | --- | --- | --- |
| [WPods](https://github.com/FPNDev/wpods) | FPNDev / fpndev (rimjoeh), and contributors | AirPods connection management, AAP controls, Bluetooth service, device capabilities and the base of our modified host | GPL-3.0; based on v0.4.2, commit `9ecc49311b814c9022b7737dd82c63487360bab0`. Complete modified source is in `engine/`. |
| [usbip-win2](https://github.com/vadimgrn/usbip-win2) | Vadym Hrynchyshyn, and contributors | WPods' signed USB/IP driver dependency | BSD-2-Clause. Original drivers are not modified or described as our own. Required notices are retained in `licenses/WPods-THIRD-PARTY-NOTICES.txt`. |
| [AirPodsDesktop](https://github.com/SpriteOvO/AirPodsDesktop) | SpriteOvO, and contributors | Presentation reference and an AirPods animation asset in the private prototype | GPL-3.0. The public widget does not bundle that animation asset or depend on AirPodsDesktop. |
| [stuffz/airpods](https://github.com/stuffz/airpods/blob/main/docs/ble.md) | stuffz, and contributors | Protocol documentation used to understand separate charging-case advertisements | Documentation reference. The case decoder added here is a C# implementation; this is not a claim of authorship of the protocol research. |
| [Windhawk](https://github.com/ramensoftware/windhawk) and [Mac Island & Dock](https://windhawk.net/mods/mac-island-dock) | Ramen Software; caliberda / [cesarkali](https://github.com/cesarkali), and contributors | Optional local Island integration and the original container used by the private prototype | Separate optional projects. Neither Windhawk nor the Island mod is bundled or required by the standalone widget. Preserve their notices if distributing an integration patch. |
| [.NET](https://github.com/dotnet/runtime) | .NET Foundation, Microsoft and contributors | Desktop runtime and Windows Forms | MIT and component-specific notices; retained when packaging a self-contained runtime. |

## Transitive dependencies

WPods uses additional libraries, including FFmpeg, RNNoise, DeepFilterNet, WebRTC audio processing, ONNX Runtime and WebView2. Their authors, versions, licenses and required notices are listed in [`licenses/WPods-THIRD-PARTY-NOTICES.txt`](licenses/WPods-THIRD-PARTY-NOTICES.txt). Keep that file with any distributed host or installer; this short credits page does not replace it.

## Additional development references

The installer is adapted from WPods' GPL-3.0 Inno Setup script, retaining its Bluetooth service recovery and dependency-removal approach. [Inno Setup](https://github.com/jrsoftware/issrc), by Jordan Russell, Martijn Laan and contributors, compiles the wizard. [innounp](https://innounp.sourceforge.net/) is used as a build-time extractor for the hash-pinned upstream archive. Neither build tool is bundled with the app. Public prose was edited using [Humanizer](https://github.com/blader/humanizer) by blader; it is an editorial tool, not a runtime dependency.

[MoOx/sf-symbols-svg](https://github.com/MoOx/sf-symbols-svg) supplied the symbol-name/Unicode mapping used to extract original glyphs for the private local prototype. [brendanballon/sfsymbols-svg](https://github.com/brendanballon/sfsymbols-svg) was consulted to check exported symbol data. These references do not grant permission to redistribute Apple's fonts or glyphs. Neither repository is a runtime dependency of the public widget.

## Apple assets and names

AirPods and Apple are Apple Inc. trademarks. This is an independent experimental project, not an Apple product.

The original SF Symbols outlines used in the owner's local Island prototype are not included in this public source/package. They are not relicensed under this project's GPL. The standalone public widget works without Apple fonts, copied system glyphs or product images. Do not add those assets to a release without appropriate permission.

## Changes in this fork

The companion adds one shared native presentation with a standalone Windows tray adapter and an optional Island adapter, an optional bounded local state/control bridge, automatic Windows microphone routing, per-component battery timestamps and caching, an independent charging-case decoder, and a user-approved updater for this repository. Modified engine files remain under WPods' GPL-3.0 license. Original copyright notices are retained.

## Podglow icon artwork

The device and listening-mode vectors in src/Shared/PodglowGlyphs.h were drawn for Podglow and are distributed under this project's GPL-3.0 license. They do not contain exported Apple font outlines. The public widget includes these icons by default; no Apple font download is required.
