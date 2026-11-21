# Updates

Podglow checks its own [GitHub Releases](https://github.com/WendelinProfiler/podglow/releases). The inherited WPods automatic installer remains disabled.

## Using the updater

Right-click the Podglow icon in the Windows notification area and choose **Check for updates**. The update window shows your installed version and release channel. When an update is available, you can read its notes before choosing **Download and install**.

Automatic checks are enabled by default. The app checks shortly after startup, at most once every 24 hours after a successful check. An available update produces a tray notification and an update entry in the menu. Turn off **Check for updates automatically** in that menu to stop background checks. Manual checks still work.

Downloading and installation start only after you choose them. Windows asks for administrator permission, and the normal interactive setup wizard opens. Bluetooth may disconnect briefly while the service is updated. The updater passes `/NORESTART` to Setup, so an update does not restart Windows automatically.

## Release channels

Preview builds can receive newer preview or stable releases. Stable builds receive stable releases only. Versions are compared numerically, including preview identifiers; an older or identical version is never offered.

## Download checks

The updater reads public release metadata without a GitHub login or token. It accepts the exact installer name and URL under `WendelinProfiler/podglow`, with a published SHA-256 digest and a bounded file size. Redirects must stay on HTTPS at GitHub's approved download hosts. After downloading, the size and hash must match before Setup can start. The verified file stays locked through the administrator-permission handoff.

These checks protect against incomplete or substituted downloads relative to GitHub's release metadata. They do not provide an independent publisher signature. Our app and alpha installer are still unsigned, and trust in the GitHub account and release remains necessary.

Updates do not run through the Bluetooth service. Cancelling a download or the administrator prompt leaves the installed version in place.

## First update from 0.1.0

Version 0.1.0 was published as AirPods Companion and has no companion updater. Install Podglow 0.1.1 once manually. Later versions can use the built-in updater. The installation identifier and existing installation folder are retained for compatibility; the app, shortcuts and downloads are named Podglow.

## Verification

```powershell
dotnet run --project tests/UpdaterPolicy/UpdaterPolicy.csproj -c Release
```

The tests cover version and channel selection, asset validation, redirect restrictions, incorrect hashes, truncated and oversized responses, cancellation and the verified-file lock. Test downloads use synthetic bytes and never execute an installer.

Adding `-- --live` also checks public GitHub metadata and downloads the current installer to a temporary test location to verify its size and SHA-256. It deletes that test copy without running it.

Actual upgrade installation, rollback and reboot recovery remain untested. See the [release checklist](../RELEASE-CHECKLIST.md).
