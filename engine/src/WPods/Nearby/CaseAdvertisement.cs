// Independent case advertiser, distinct from the 27-byte bud format.
// Protocol reference: https://github.com/stuffz/airpods/blob/main/docs/ble.md
// Only paired encryption keys are used; no keys or raw/decrypted packets are logged.
using System.Buffers.Binary;
using System.Security.Cryptography;
using WPods.Core;

namespace WPods.Nearby;

internal readonly record struct CaseReport(Battery Case, Battery Left, Battery Right,
    bool LidOpen, byte OpenCount, uint Counter, ushort Header, ushort Variant)
{
    public bool BudInside => Left.Percent.HasValue || Right.Percent.HasValue;
}

internal static class CaseAdvertisement
{
    public static bool IsFormat(ReadOnlySpan<byte> data) =>
        data.Length == 19 && data[0] == 0x07 && data[1] == 0x11;

    public static bool TryRead(ReadOnlySpan<byte> data, Aes pairedCipher, out CaseReport report) => TryRead(data, pairedCipher, out report, out _);

    public static bool TryRead(ReadOnlySpan<byte> data, Aes pairedCipher, out CaseReport report, out byte checks)
    {
        report = default; checks = 0;
        if (!IsFormat(data)) return false;
        Span<byte> plain = stackalloc byte[16];
        try
        {
            pairedCipher.DecryptEcb(data[3..], plain, PaddingMode.None);
            // Firmware-dependent header/variant and the ticking counter are not constants.
            bool fixedFields = plain[8] == 0 && plain[10] == 0 && plain[11] == 0;
            bool levels = ValidLevel(plain[3]) && plain[3] != 0xFF && ValidLevel(plain[4]) && ValidLevel(plain[5]);
            checks = (byte)((fixedFields ? 1 : 0) | (levels ? 2 : 0) | (plain[0] == 0x29 && plain[1] == 0x20 ? 4 : 0));
            bool knownCaseHeader = plain[0] == 0x29 && plain[1] == 0x20;
            if ((!fixedFields && !knownCaseHeader) || !levels) return false;
            report = new(Decode(plain[3]), Decode(plain[4]), Decode(plain[5]),
                (plain[2] & 8) == 0, (byte)(plain[2] & 7), BinaryPrimitives.ReadUInt32LittleEndian(plain[12..]),
                BinaryPrimitives.ReadUInt16LittleEndian(plain), BinaryPrimitives.ReadUInt16LittleEndian(plain[6..]));
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static bool ValidLevel(byte value) => value == 0xFF || (value & 0x7F) <= 100;
    private static Battery Decode(byte value) => value == 0xFF ? default
        : new((byte)(value & 0x7F), (value & 0x80) != 0 ? BatteryStatus.Charging : BatteryStatus.Discharging);
}

// ECB has no authentication tag. Require two structurally valid, advancing reports with the
// same paired key before publishing, reject ambiguity across pairs, and do not freshen duplicates.
internal sealed class CaseReportGate
{
    private bool _seen, _confirmed;
    private CaseReport _last;
    public bool Observe(CaseReport report)
    {
        if (!_seen) { _seen = true; _last = report; return false; }
        if (report == _last) return false;
        int advance = unchecked((int)(report.Counter - _last.Counter));
        if (advance < 0) { _confirmed = false; _last = report; return false; }
        if (advance > 0) _confirmed = true;
        _last = report;
        return _confirmed;
    }
}
