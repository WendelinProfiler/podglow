using System.Text.Json;
using WPods.Updates;

int passed = 0;
void Test(string name, Action check) { check(); Console.WriteLine("PASS " + name); passed++; }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
void Reject(Action check) { try { check(); } catch (InvalidDataException) { return; } throw new Exception("Unsafe release accepted"); }
Dictionary<string, object?> Release(string tag, bool preview = true, bool draft = false, string? url = null, string? digest = null, long size = 100)
{
    return new() { ["tag_name"] = tag, ["prerelease"] = preview, ["draft"] = draft, ["assets"] = new object[] { new { name = ReleasePolicy.AssetName, state = "uploaded", size, browser_download_url = url ?? $"https://github.com/{ReleasePolicy.Repository}/releases/download/{tag}/{ReleasePolicy.AssetName}", digest = digest ?? ("sha256:" + new string('a', 64)) } } };
}
CompanionRelease? Select(CompanionVersion installed, params object[] releases) => ReleasePolicy.Select(JsonSerializer.Serialize(releases), installed);
var current = CompanionVersion.Parse("v0.1.1-alpha.1");
Test("numeric version ordering", () => Equal(true, CompanionVersion.Parse("0.1.10").CompareTo(CompanionVersion.Parse("0.1.9")) > 0));
Test("numeric preview ordering", () => Equal(true, CompanionVersion.Parse("0.1.1-alpha.10").CompareTo(current) > 0));
Test("stable supersedes preview", () => Equal(true, CompanionVersion.Parse("0.1.1").CompareTo(current) > 0));
Test("preview cannot replace stable", () => Equal<CompanionRelease?>(null, Select(CompanionVersion.Parse("0.1.1"), Release("v0.2.0-alpha.1"))));
Test("same version is not an update", () => Equal<CompanionRelease?>(null, Select(current, Release("v0.1.1-alpha.1"))));
Test("downgrades are ignored", () => Equal<CompanionRelease?>(null, Select(current, Release("v0.1.0-alpha.9"))));
Test("drafts are ignored", () => Equal<CompanionRelease?>(null, Select(current, Release("v9.0.0-alpha.1", draft: true))));
Test("newest version wins independent of feed order", () => Equal("v0.2.0-alpha.2", Select(current, Release("v0.2.0-alpha.1"), Release("v0.2.0-alpha.2"))?.Tag));
Test("older malformed package does not obscure newest valid package", () => Equal("v0.2.0-alpha.2", Select(current, Release("v0.2.0-alpha.1", digest: ""), Release("v0.2.0-alpha.2"))?.Tag));
Test("newest malformed package blocks fallback", () => Reject(() => Select(current, Release("v0.2.0-alpha.2", digest: ""), Release("v0.2.0-alpha.1"))));
Test("foreign repository rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", url: "https://github.com/other/repo/releases/download/v0.2.0-alpha.1/AirPodsCompanion-Setup.exe"))));
Test("wrong tag in asset URL rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", url: $"https://github.com/{ReleasePolicy.Repository}/releases/download/v0.1.0-alpha.1/{ReleasePolicy.AssetName}"))));
Test("missing digest rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", digest: ""))));
Test("non-SHA256 digest rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", digest: "sha1:" + new string('a', 40)))));
Test("empty download rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", size: 0))));
Test("oversized download rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", size: ReleasePolicy.MaximumInstallerBytes + 1))));
Test("preview metadata must match version", () => Equal<CompanionRelease?>(null, Select(current, Release("v0.2.0", preview: true))));
Test("duplicate installers rejected", () => { var row = Release("v0.2.0-alpha.1"); var assets = (object[])row["assets"]!; row["assets"] = new[] { assets[0], assets[0] }; Reject(() => Select(current, row)); });
Test("missing installer rejected", () => { var row = Release("v0.2.0-alpha.1"); row["assets"] = Array.Empty<object>(); Reject(() => Select(current, row)); });
Test("numeric preview identifiers cannot have leading zeros", () => Reject(() => CompanionVersion.Parse("0.2.0-alpha.01")));
Test("version overflow rejected", () => Reject(() => CompanionVersion.Parse("99999999999999999999999.0.0")));
Test("malicious tag rejected", () => Reject(() => CompanionVersion.Parse("0.2.0/../../payload")));
Test("newline in version rejected", () => Reject(() => CompanionVersion.Parse("0.2.0\n")));
Test("newline in digest rejected", () => Reject(() => Select(current, Release("v0.2.0-alpha.1", digest: "sha256:" + new string('a', 64) + "\n"))));
Test("relative download location rejected", () => Equal(false, ReleasePolicy.AllowedDownloadLocation(new Uri("/payload", UriKind.Relative))));
Test("invalid size type rejected", () => { var row = Release("v0.2.0-alpha.1"); row["assets"] = new object[] { new { name = ReleasePolicy.AssetName, state = "uploaded", size = "100", browser_download_url = $"https://github.com/{ReleasePolicy.Repository}/releases/download/v0.2.0-alpha.1/{ReleasePolicy.AssetName}", digest = "sha256:" + new string('a', 64) } }; Reject(() => Select(current, row)); });
foreach (string url in new[] { "http://github.com/file", "https://github.com.evil.example/file", "https://evil.example/file", "https://user@github.com/file", "https://github.com:444/file", "https://release-assets.githubusercontent.com/file#fragment" })
    Test("unsafe download redirect rejected: " + url, () => Equal(false, ReleasePolicy.AllowedDownloadLocation(new Uri(url))));
Test("official release CDN accepted", () => Equal(true, ReleasePolicy.AllowedDownloadLocation(new Uri("https://release-assets.githubusercontent.com/asset?signature=test"))));
byte[] payload = System.Text.Encoding.UTF8.GetBytes("test installer bytes, never executable");
string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
var package = new CompanionRelease("v0.2.0-alpha.1", CompanionVersion.Parse("0.2.0-alpha.1"), new Uri($"https://github.com/{ReleasePolicy.Repository}/releases/download/v0.2.0-alpha.1/{ReleasePolicy.AssetName}"), hash, payload.Length, new Uri("https://github.com/"));
string temp = Path.Combine(Path.GetTempPath(), "CompanionUpdaterTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
void DownloadTest(string name, Func<Uri, HttpResponseMessage> reply, CompanionRelease release, bool reject, bool cancelled = false)
{
    Test(name, () =>
    {
        string file = Path.Combine(temp, Guid.NewGuid().ToString("N") + ".bin");
        using var http = new HttpClient(new FakeHttp(reply));
        try
        {
            try
            {
                using FileStream verified = CompanionUpdater.DownloadAsync(release, file, _ => { }, new CancellationToken(cancelled), http).GetAwaiter().GetResult();
                if (reject) throw new Exception("Unsafe download accepted");
                Equal(payload.Length, (int)verified.Length);
                try { File.WriteAllBytes(file, [0]); throw new Exception("Verified file was replaceable before launch"); } catch (IOException) { }
            }
            catch (Exception e) when (reject && e is InvalidDataException or HttpRequestException or OperationCanceledException) { }
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    });
}
HttpResponseMessage Bytes(byte[] data) => new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
try
{
    DownloadTest("verified download remains locked through handoff", _ => Bytes(payload), package, false);
    DownloadTest("checksum mismatch blocks download", _ => Bytes(payload), package with { Sha256 = new string('0', 64) }, true);
    DownloadTest("truncated download rejected", _ => Bytes(payload[..^1]), package, true);
    DownloadTest("oversized response rejected", _ => Bytes([.. payload, 0]), package, true);
    DownloadTest("network errors rejected", _ => new(System.Net.HttpStatusCode.ServiceUnavailable), package, true);
    DownloadTest("download cancellation prevents success", _ => Bytes(payload), package, true, cancelled: true);
    DownloadTest("foreign redirect rejected", _ => new(System.Net.HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.example/payload.exe") } }, package, true);
    DownloadTest("HTTP downgrade rejected", _ => new(System.Net.HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://github.com/payload.exe") } }, package, true);
    DownloadTest("redirect loop bounded", _ => new(System.Net.HttpStatusCode.Redirect) { Headers = { Location = package.Download } }, package, true);
    int redirects = 0;
    DownloadTest("official CDN redirect verified", _ => redirects++ == 0 ? new(System.Net.HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://release-assets.githubusercontent.com/file?signature=test") } } : Bytes(payload), package, false);
}
finally { Directory.Delete(temp); }
if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    using var live = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    live.DefaultRequestHeaders.UserAgent.ParseAdd("AirPodsCompanion-UpdaterTests/0.1");
    live.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    string feed = live.GetStringAsync(ReleasePolicy.Feed).GetAwaiter().GetResult();
    CompanionRelease release = ReleasePolicy.Select(feed, CompanionVersion.Parse("0.0.0-alpha.1")) ?? throw new Exception("No public release available for read-only verification");
    string directory = Path.Combine(Path.GetTempPath(), "CompanionUpdaterLive-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    string file = Path.Combine(directory, "download.bin");
    try
    {
        using (FileStream verified = CompanionUpdater.DownloadAsync(release, file, _ => { }, CancellationToken.None, live).GetAwaiter().GetResult())
            Equal(release.Size, verified.Length);
        Console.WriteLine($"PASS live GitHub metadata, redirect, size and SHA-256: {release.Tag}. Installer was not executed.");
        passed++;
    }
    finally { if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); }
}
Console.WriteLine($"{passed} updater checks passed. No service or installation was started.");

sealed class FakeHttp(Func<Uri, HttpResponseMessage> reply) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(reply(request.RequestUri!));
    }
}
