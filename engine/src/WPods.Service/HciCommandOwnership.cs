using System.Buffers.Binary;
namespace WPods.Service;

// One physical controller command credit shared with Windows. Own security
// commands carry only a connection handle; pairing keys stay in Windows.
internal sealed class HciCommandOwnership
{
    private readonly Dictionary<ushort,int> _windows=[];
    private ushort _ours;
    private int _credits=1;
    internal bool BeginWindows(ushort opcode)
    {
        if(_ours!=0)return false;
        if((opcode>>10)!=0x3F&&opcode!=0x0C35)_windows[opcode]=_windows.GetValueOrDefault(opcode)+1;
        return true;
    }
    internal bool BeginOwn(ushort opcode)
    {
        if(opcode is not (0x0411 or 0x0413)||_windows.Count!=0||_ours!=0||_credits<=0)return false;
        _ours=opcode;_credits--;return true;
    }
    internal void AbortOwn(ushort opcode){if(_ours==opcode)_ours=0;}
    internal bool Acknowledge(ReadOnlySpan<byte> packet,out ushort opcode,out byte status)
    {
        opcode=0;status=0;
        if(packet.Length<2||packet.Length!=packet[1]+2)return false;
        if(packet[0]==0x0F&&packet.Length>=6){opcode=BinaryPrimitives.ReadUInt16LittleEndian(packet[4..]);status=packet[2];_credits=packet[3];}
        else if(packet[0]==0x0E&&packet.Length>=5){opcode=BinaryPrimitives.ReadUInt16LittleEndian(packet[3..]);status=packet.Length>=6?packet[5]:(byte)0;_credits=packet[2];}
        else return false;
        if(_ours!=0&&opcode==_ours){_ours=0;return true;}
        if(_windows.TryGetValue(opcode,out int count)){if(count==1)_windows.Remove(opcode);else _windows[opcode]=count-1;}
        return false;
    }
    internal void Reset(){_windows.Clear();_ours=0;_credits=1;}
}
