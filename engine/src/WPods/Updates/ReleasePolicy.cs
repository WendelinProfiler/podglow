using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WPods.Updates;

internal sealed record CompanionVersion(int Major, int Minor, int Patch, string[] Preview) : IComparable<CompanionVersion>
{
    public static CompanionVersion Parse(string value)
    {
        if (value.Length > 100) throw new InvalidDataException("Unsupported release version.");
        Match match = Regex.Match(value, @"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("Unsupported release version.");
        string[] preview = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        foreach (string part in preview)
            if (part.All(char.IsAsciiDigit) && part.Length > 1 && part[0] == '0') throw new InvalidDataException("Invalid preview version.");
        try { return new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), preview); }
        catch (OverflowException e) { throw new InvalidDataException("Release version is too large.", e); }
    }

    public int CompareTo(CompanionVersion? other)
    {
        if (other is null) return 1;
        int n = Major.CompareTo(other.Major); if (n != 0) return n;
        n = Minor.CompareTo(other.Minor); if (n != 0) return n;
        n = Patch.CompareTo(other.Patch); if (n != 0) return n;
        if (Preview.Length == 0 || other.Preview.Length == 0) return other.Preview.Length.CompareTo(Preview.Length);
        for (int i = 0; i < Math.Min(Preview.Length, other.Preview.Length); i++)
        {
            string a = Preview[i], b = other.Preview[i];
            bool an = a.All(char.IsAsciiDigit), bn = b.All(char.IsAsciiDigit);
            n = an && bn ? a.Length.CompareTo(b.Length) : an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a, b);
            if (n == 0) n = string.CompareOrdinal(a, b);
            if (n != 0) return n;
        }
        return Preview.Length.CompareTo(other.Preview.Length);
    }
}

internal sealed record CompanionRelease(string Tag, CompanionVersion Version, Uri Download, string Sha256, long Size, Uri Notes);

internal static class ReleasePolicy
{
    public const string Repository = "WendelinProfiler/podglow";
    public const string AssetName = "Podglow-Setup.exe";
    public const long MaximumInstallerBytes = 512L * 1024 * 1024;
    public static readonly Uri Feed = new($"https://api.github.com/repos/{Repository}/releases?per_page=100");

    public static CompanionRelease? Select(string json, CompanionVersion installed)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release feed.");
        JsonElement? selected = null;
        CompanionVersion? selectedVersion = null;
        foreach (JsonElement row in doc.RootElement.EnumerateArray())
        {
            if (row.GetProperty("draft").GetBoolean()) continue;
            bool preview = row.GetProperty("prerelease").GetBoolean();
            if (preview && installed.Preview.Length == 0) continue;
            string candidate = row.GetProperty("tag_name").GetString() ?? "";
            CompanionVersion version;
            try { version = CompanionVersion.Parse(candidate); } catch (InvalidDataException) { continue; }
            if (preview != (version.Preview.Length > 0) || version.CompareTo(installed) <= 0) continue;
            if (selectedVersion is null || version.CompareTo(selectedVersion) > 0) { selected = row; selectedVersion = version; }
        }
        if (selected is not { } release || selectedVersion is null) return null;
        string tag = release.GetProperty("tag_name").GetString() ?? "";
        {
            JsonElement[] assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == AssetName).ToArray();
            if (assets.Length != 1) throw new InvalidDataException("The release needs exactly one companion installer.");
            JsonElement asset = assets[0];
            string expected = $"https://github.com/{Repository}/releases/download/{tag}/{AssetName}";
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            string digest = asset.GetProperty("digest").GetString() ?? "";
            JsonElement sizeValue = asset.GetProperty("size");
            if (sizeValue.ValueKind != JsonValueKind.Number || !sizeValue.TryGetInt64(out long size)) throw new InvalidDataException("Invalid installer size.");
            if (url != expected || !Regex.IsMatch(digest, @"\Asha256:[0-9a-f]{64}\z", RegexOptions.CultureInvariant) || size <= 0 || size > MaximumInstallerBytes || asset.GetProperty("state").GetString() != "uploaded")
                throw new InvalidDataException("The update package could not be verified.");
            return new(tag, selectedVersion, new Uri(expected), digest[7..], size, new Uri($"https://github.com/{Repository}/releases/tag/{tag}"));
        }
    }

    public static bool AllowedDownloadLocation(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
        uri.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
}
