# Building the experimental installer

The build packages the matching native widget and modified WPods host and service. It does not install them on the build computer.

## Tools

- Windows x64 with MSVC and the Windows SDK
- .NET 10 SDK
- Inno Setup 6.6.1 or compatible later 6.x compiler
- innounp with support for Inno Setup 6.7 archives

Use tools from their original publishers. `InnoUnpacker` and `InnoCompiler` are paths to your build tools, not files shipped to users.

```powershell
./scripts/Build-Release.ps1 `
  -Version 0.1.1 `
  -InnoUnpacker C:/BuildTools/innounp/innounp.exe `
  -InnoCompiler 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

If your SDK is outside PATH, pass `-DotNet C:/BuildTools/dotnet/dotnet.exe`.

The script checks the pinned WPods 0.4.2 archive, extracts its native libraries, audio models and supporting web resources, then compiles this repository's modified managed host and service. It never copies an installed user's app files or settings. The .NET runtime is bundled. Microsoft WebView2 is installed from Microsoft's signed bootstrapper if missing.

The installer includes the unmodified usbip-win2 setup, checked against its pinned SHA-256. Its installation requires explicit consent. Silent installation also requires `/ACCEPTBLUETOOTH=1`; that flag is an administrator's acknowledgement, not a default.

## Outputs

`out/release-0.1.1/` contains:

- `Podglow-Setup.exe`
- `SHA256SUMS.txt`
- `payload-manifest.json`, with relative paths and each packaged file's hash
- `payload/`, the staged runtime files

An existing populated payload makes the build stop. Move it aside before rebuilding so files from older builds cannot silently enter the package.

## Release checks

`Test-ReleasePayload.ps1` checks required runtime files, unwanted settings and developer files, and known private data or credential patterns in text and binary files. These checks do not establish functional correctness or replace a broader security review.

Publish the setup, checksums and manifest as a **prerelease**, with the matching source commit tagged. Include [RELEASE-NOTES.md](../RELEASE-NOTES.md), which explicitly records the missing installation and hardware tests. Do not describe an untested alpha as a verified Windows installation.

## Pinned dependency provenance

| Artifact | Original release | SHA-256 |
| --- | --- | --- |
| WPods-Setup.exe | [WPods 0.4.2](https://github.com/FPNDev/wpods/releases/tag/v0.4.2) | `00dbdb220681931addc1ab6e0d90f5760ab0e3377099f6e10320c25aa76e5801` |
| USBip-0.9.8.1-x64.exe | [usbip-win2 0.9.8.1](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1) | `38cad6d4432b52d5bb9409d9ad03b72fdffc4ada4cd3a48fbeca1a2752a8518a` |

The local compiler used for this alpha was the original [Inno Setup 6.6.1](https://github.com/jrsoftware/issrc/releases/tag/is-6_6_1) installer, SHA-256 `d243ce440c02705530699554fb9612b9b2bd7a2a90629cdb7f41e66f5faeb91f`. It is a build tool and is not bundled.
