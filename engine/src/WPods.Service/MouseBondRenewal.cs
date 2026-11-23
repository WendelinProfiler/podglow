using System.Buffers.Binary;

namespace WPods.Service;

// An explicit, short-lived request to re-authenticate one owned Classic device.
// No link key is read, retained, returned or logged. The caller authenticates the
// local request before arming this policy; ordinary connections are untouched.
internal sealed class MouseBondRenewal
{
    internal enum Stage { Idle, Armed, Pairing, Complete, Failed }
    internal readonly record struct Snapshot(int Generation, Stage State, bool FreshKey, bool Authenticated, bool Encrypted, int Error);
    private ulong _address;
    private long _deadline;
    private int _generation, _handle = -1, _error;
    private Stage _state;
    private bool _awaitingAck, _freshKey, _authenticated, _encrypted, _windowsOwnsSecurity;

    internal bool Arm(ulong address, long now, bool authorized, int existingHandle = -1, bool windowsOwnsSecurity = false)
    {
        Expire(now);
        if (!authorized || address == 0 || address > 0xFFFFFFFFFFFF || _awaitingAck || _state is Stage.Armed or Stage.Pairing) return false;
        _address = address; _deadline = now + 20_000; _generation++; _windowsOwnsSecurity = windowsOwnsSecurity;
        _handle = existingHandle is >= 0 and <= 0x0FFF ? existingHandle : -1; _error = 0; _freshKey = _authenticated = _encrypted = false;
        _state = Stage.Armed; return true;
    }

    internal Snapshot Status(long now) { Expire(now); return new(_generation, _state, _freshKey, _authenticated, _encrypted, _error); }
    internal bool WindowsOwnsSecurity(long now) { Expire(now); return _windowsOwnsSecurity && _state is Stage.Armed or Stage.Pairing; }
    internal void Reset() { _state = Stage.Idle; _awaitingAck = _windowsOwnsSecurity = false; _handle = -1; _address = 0; }
    private void Expire(long now)
    {
        if (_state is Stage.Armed or Stage.Pairing && now > _deadline) { _state = Stage.Failed; _error = 1460; }
    }
    private static ulong Address(ReadOnlySpan<byte> data)
    {
        ulong address = 0; for (int i = 0; i < 6; i++) address |= (ulong)data[i] << (i * 8); return address;
    }

    internal bool TryRewrite(ReadOnlySpan<byte> command, long now, out byte[] replacement)
    {
        replacement = []; Expire(now);
        // Link_Key_Request_Reply: opcode(2), length(1), address(6), key(16).
        // Copy only the address. Deliberately never inspect the key bytes.
        if (_state != Stage.Armed || command.Length != 25 || command[2] != 22
            || BinaryPrimitives.ReadUInt16LittleEndian(command) != 0x040B || Address(command[3..9]) != _address) return false;
        replacement = new byte[9]; BinaryPrimitives.WriteUInt16LittleEndian(replacement, 0x040C);
        replacement[2] = 6; command[3..9].CopyTo(replacement.AsSpan(3));
        _state = Stage.Pairing; _awaitingAck = true; return true;
    }

    // Events still reach Windows, including its fresh-key notification. Only
    // the acknowledgement of the substituted command has its opcode restored.
    internal void Observe(Span<byte> packet, long now)
    {
        Expire(now);
        if (packet.Length < 2 || packet.Length != packet[1] + 2) return;
        if (_awaitingAck && packet[0] == 0x0E && packet.Length >= 12
            && BinaryPrimitives.ReadUInt16LittleEndian(packet[3..5]) == 0x040C && Address(packet[6..12]) == _address)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packet[3..5], 0x040B); _awaitingAck = false;
            if (packet[5] != 0) { _state = Stage.Failed; _error = packet[5]; }
            else if (_state == Stage.Pairing && _freshKey && _authenticated && _encrypted) _state = Stage.Complete;
            return;
        }
        if (_state is not (Stage.Armed or Stage.Pairing)) return;
        if (packet[0] == 0x36 && packet.Length == 9 && Address(packet[3..9]) == _address && packet[2] != 0)
        { _state = Stage.Failed; _error = packet[2]; }
        else if (packet[0] == 0x03 && packet.Length >= 13 && Address(packet[5..11]) == _address && packet[2] == 0)
            _handle = BinaryPrimitives.ReadUInt16LittleEndian(packet[3..5]) & 0x0FFF;
        else if (packet[0] == 0x18 && packet.Length >= 25 && Address(packet[2..8]) == _address)
        {
            // Windows may already have answered the key request negatively.
            // A genuine fresh-key event also starts that ordinary pairing path.
            _freshKey = true; _state = Stage.Pairing;
        }
        else if (_state == Stage.Pairing && packet[0] is 0x06 or 0x08 or 0x59 && packet.Length >= (packet[0] == 0x06 ? 5 : 6)
            && (BinaryPrimitives.ReadUInt16LittleEndian(packet[3..5]) & 0x0FFF) == _handle)
        {
            if (packet[2] != 0) { _state = Stage.Failed; _error = packet[2]; return; }
            if (packet[0] == 0x06) _authenticated = true;
            else _encrypted = packet[5] != 0;
        }
        else if (packet[0] == 0x05 && packet.Length >= 6 && (BinaryPrimitives.ReadUInt16LittleEndian(packet[3..5]) & 0x0FFF) == _handle)
        { _state = Stage.Failed; _error = packet[5]; }
        if (_freshKey && _authenticated && _encrypted && !_awaitingAck) _state = Stage.Complete;
    }
}
