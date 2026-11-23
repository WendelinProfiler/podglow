using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace WPods.Updates;

// Runs before the Bluetooth host is initialized. No service or device commands are used here.
internal static partial class CompanionUpdater
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirPodsCompanion", "updates");
    private static readonly HttpClient Http = CreateClient();
    private sealed record InstalledRelease(string Version);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AirPodsCompanion-Updater/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static (string Label, CompanionVersion Version) Installed()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "companion-release.json");
        if (!File.Exists(path) || new FileInfo(path).Length > 4096) throw new InvalidDataException("Installed release metadata is missing.");
        InstalledRelease? data = JsonSerializer.Deserialize<InstalledRelease>(File.ReadAllText(path));
        string label = data?.Version ?? throw new InvalidDataException("Installed release metadata is missing.");
        return (label, CompanionVersion.Parse(label));
    }

    public static void Run(string[] args)
    {
        bool background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        using var instance = new Mutex(true, @"Local\AirPodsCompanion.Update", out bool first);
        if (!first)
        {
            if (!background) MessageBox.Show("An update check or download is already running.", "Podglow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var installed = Installed();
            if (args.Contains("--inspect-updates", StringComparer.OrdinalIgnoreCase))
            {
                // Read-only integration diagnostic: no download, dialog, settings write or installation.
                CompanionRelease? release = CheckAsync(installed.Version, CancellationToken.None).GetAwaiter().GetResult();
                string report = JsonSerializer.Serialize(new { installed = installed.Label, available = release?.Tag, download = release?.Download.AbsoluteUri });
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "update-inspection.json"), report);
                return;
            }
            if (background)
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\AirPodsCompanion");
                if (key?.GetValue("CheckForUpdates") is int enabled && enabled == 0) return;
                Directory.CreateDirectory(Folder);
                string stamp = Path.Combine(Folder, "last-check.txt");
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (File.Exists(stamp) && long.TryParse(File.ReadAllText(stamp), out long last) && now >= last && now - last < 86400) return;
                CompanionRelease? release = CheckAsync(installed.Version, CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(stamp, now.ToString(System.Globalization.CultureInfo.InvariantCulture));
                PostMessage(FindWindow("AirPodsCompanionWidget", null), release is null ? 0x80c5u : 0x80c4u, 0, 0);
                return;
            }
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UpdateWindow(installed.Label, installed.Version));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or HttpRequestException or OperationCanceledException or InvalidOperationException or KeyNotFoundException)
        {
            if (!background) MessageBox.Show("Updates could not be checked. Please try again later or download the current version from the project's GitHub Releases page.", "Podglow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Environment.ExitCode = 1;
        }
    }

    private static async Task<CompanionRelease?> CheckAsync(CompanionVersion version, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        using HttpResponseMessage response = await Http.GetAsync(ReleasePolicy.Feed, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var data = new MemoryStream();
        byte[] buffer = new byte[16384];
        int count;
        while ((count = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (data.Length + count > 3 * 1024 * 1024) throw new InvalidDataException("Release feed is too large.");
            data.Write(buffer, 0, count);
        }
        return ReleasePolicy.Select(Encoding.UTF8.GetString(data.ToArray()), version);
    }

    internal static async Task<FileStream> DownloadAsync(CompanionRelease release, string file, Action<int> progress, CancellationToken cancellation, HttpClient? transport = null)
    {
        Uri uri = release.Download;
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            if (!ReleasePolicy.AllowedDownloadLocation(uri)) throw new InvalidDataException("Unexpected download location.");
            using HttpResponseMessage response = await (transport ?? Http).GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                uri = new Uri(uri, response.Headers.Location ?? throw new InvalidDataException("Missing redirect location."));
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != release.Size) throw new InvalidDataException("Download size does not match the release.");
            using Stream source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            // A writable handle prevents Windows from mapping an EXE (sharing violation).
            // After downloading, reopen read-only and verify again under the final lock.
            var target = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.Asynchronous);
            try
            {
                byte[] buffer = new byte[65536];
                long total = 0;
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int count, lastProgress = -1;
                while ((count = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
                {
                    total += count;
                    if (total > release.Size) throw new InvalidDataException("Download exceeded the published size.");
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellation).ConfigureAwait(false);
                    sha.AppendData(buffer, 0, count);
                    int percent = (int)(total * 100 / release.Size);
                    if (percent != lastProgress) { lastProgress = percent; progress(percent); }
                }
                if (total != release.Size || !CryptographicOperations.FixedTimeEquals(sha.GetHashAndReset(), Convert.FromHexString(release.Sha256)))
                    throw new InvalidDataException("Download checksum does not match the release.");
                await target.FlushAsync(cancellation).ConfigureAwait(false);
                target.Dispose();
                var verified = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                try
                {
                    // Recheck after reopening: a replacement during the handle transition
                    // must never inherit the successful check of the downloaded bytes.
                    byte[] digest = await SHA256.HashDataAsync(verified, cancellation).ConfigureAwait(false);
                    if (verified.Length != release.Size || !CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(release.Sha256)))
                        throw new InvalidDataException("Installer changed before launch.");
                    verified.Position = 0;
                    return verified;
                }
                catch { verified.Dispose(); throw; }
            }
            catch { target.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many download redirects.");
    }

    private sealed class UpdateWindow : Form
    {
        private readonly CompanionVersion _installed;
        private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, Text = "Checking GitHub Releases…" };
        private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 12, Style = ProgressBarStyle.Marquee };
        private readonly Button _action = new() { Text = "Check again", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Enabled = false };
        private readonly Button _notes = new() { Text = "Release notes", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Enabled = false };
        private readonly Button _close = new() { Text = "Close", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        private readonly CancellationTokenSource _cancel = new();
        private CompanionRelease? _release;
        private bool _busy;

        public UpdateWindow(string label, CompanionVersion installed)
        {
            SuspendLayout();
            _installed = installed;
            Text = "Podglow updates";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(28, 28, 30);
            ForeColor = Color.White;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.Controls.Add(new Label { Text = "Software update", Font = new Font("Segoe UI", 18, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(new Label { Text = $"Installed: {label} · {(installed.Preview.Length > 0 ? "Preview channel" : "Stable channel")}", ForeColor = Color.Silver, Dock = DockStyle.Fill }, 0, 1);
            layout.Controls.Add(_status, 0, 2);
            layout.Controls.Add(_progress, 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            buttons.Controls.AddRange([_close, _action, _notes]);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);
            foreach (Button button in new[] { _action, _notes, _close }) { button.FlatStyle = FlatStyle.Flat; button.ForeColor = Color.White; button.Padding = new Padding(5); }
            _action.BackColor = Color.FromArgb(0, 100, 210);
            _close.Click += (_, _) => Close();
            _notes.Click += (_, _) =>
            {
                string url = _release?.Notes.AbsoluteUri ?? $"https://github.com/{ReleasePolicy.Repository}/releases";
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException) { _status.Text = "The browser could not open. You can find release notes on the project's GitHub Releases page."; }
            };
            _action.Click += async (_, _) => { if (_release is null) await Check(); else await Install(); };
            Shown += async (_, _) => await Check();
            FormClosing += (_, _) => _cancel.Cancel();
            FormClosed += (_, _) => _cancel.Dispose();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(530, 310);
            ResumeLayout(true);
        }

        private async Task Check()
        {
            _busy = true; _action.Enabled = _notes.Enabled = false; _progress.Visible = true; _progress.Style = ProgressBarStyle.Marquee;
            _status.Text = "Checking GitHub Releases…";
            try
            {
                _release = await CheckAsync(_installed, _cancel.Token);
                if (IsDisposed) return;
                if (_release is null) PostMessage(FindWindow("AirPodsCompanionWidget", null), 0x80c5, 0, 0);
                _status.Text = _release is null ? "You're up to date. No newer release is available for this channel." : $"{_release.Tag} is available.\n\nSetup will ask for administrator permission. Bluetooth may disconnect briefly. Windows will not restart automatically.";
                _action.Text = _release is null ? "Check again" : "Download and install";
                _notes.Enabled = true;
            }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "The update check timed out. Please try again."; }
            catch (Exception e) when (e is HttpRequestException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or IOException)
            { if (!IsDisposed) _status.Text = "The update check failed. Check your connection and try again. No files were installed."; }
            finally { _busy = false; if (!IsDisposed) { _progress.Visible = false; _action.Enabled = _notes.Enabled = true; } }
        }

        private async Task Install()
        {
            if (_busy || _release is null) return;
            _busy = true; _action.Enabled = _notes.Enabled = false; _progress.Visible = true; _progress.Style = ProgressBarStyle.Continuous;
            _close.Text = "Cancel";
            string folder = Path.Combine(Folder, Guid.NewGuid().ToString("N"));
            string file = Path.Combine(folder, ReleasePolicy.AssetName);
            bool launched = false;
            bool downloadVerified = false;
            CancellationToken token = _cancel.Token;
            try
            {
                Directory.CreateDirectory(folder);
                _status.Text = "Downloading and verifying the installer…";
                var progress = new Progress<int>(value => { if (!IsDisposed) _progress.Value = value; });
                using FileStream verified = await DownloadAsync(_release, file, value => ((IProgress<int>)progress).Report(value), token);
                downloadVerified = true;
                token.ThrowIfCancellationRequested();
                if (IsDisposed) return;
                // Interactive installer only. Never bypass Bluetooth consent or reboot Windows.
                using Process? setup = Process.Start(new ProcessStartInfo(file, "/NORESTART") { UseShellExecute = true, Verb = "runas", WorkingDirectory = folder });
                if (setup is null) throw new IOException("Setup did not start.");
                launched = true;
                Close();
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1223) { if (!IsDisposed) _status.Text = "Administrator permission was cancelled. No update was started."; }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "Download cancelled. No update was started."; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or Win32Exception)
            { if (!IsDisposed) _status.Text = downloadVerified
                ? "The installer was verified, but Windows could not start it. No update was started. You can download it from Release notes."
                : "The installer could not be downloaded or verified. No update was started. Please try again."; }
            finally
            {
                _busy = false;
                if (!launched) { try { if (File.Exists(file)) File.Delete(file); if (Directory.Exists(folder)) Directory.Delete(folder); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
                if (!IsDisposed) { _progress.Visible = false; _action.Enabled = true; _notes.Enabled = true; _close.Text = "Close"; }
            }
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindow(string name, string? title);
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint window, uint message, nuint w, nint l);
}
