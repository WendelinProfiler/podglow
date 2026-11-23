using System.Buffers.Binary;

namespace WPods.Service;

// Metadata only, scoped to one explicitly claimed mouse and a bounded window.
// Never formats packet bytes, keys, PINs or numeric-comparison values.
internal sealed class MouseNegotiationTrace
{
    private ulong _target;
    private int _handle=-1;
    private long _until;
    internal void Arm(ulong target,long now,int handle=-1) { _target=target;_handle=handle;_until=now+90_000; }
    internal void Reset() { _target=0;_handle=-1;_until=0; }
    private bool Active(long now)=>_target!=0&&now<=_until;
    private bool Address(ReadOnlySpan<byte> data) { if(data.Length<6)return false;ulong address=0;for(int n=0;n<6;n++)address|=(ulong)data[n]<<(8*n);return address==_target; }
    private bool Handle(ReadOnlySpan<byte> data)=>data.Length>=2&&_handle>=0&&(BinaryPrimitives.ReadUInt16LittleEndian(data)&0xFFF)==_handle;

    internal string? Command(ReadOnlySpan<byte> p,long now)
    {
        if(!Active(now)||p.Length<3||p.Length!=p[2]+3)return null;
        ushort op=BinaryPrimitives.ReadUInt16LittleEndian(p);
        if(op is 0x0405 or 0x040B or 0x040C or 0x040D or 0x040E or 0x042B or 0x042C or 0x042D or 0x042E or 0x042F or 0x0430 or 0x0433 or 0x0434) {
            if(p.Length<9||!Address(p[3..9]))return null;
            if(op==0x042B&&p.Length==12)return $"Windows command 0x{op:X4}: io={p[9]} oob={p[10]} auth={p[11]}";
            if(op==0x0405&&p.Length==16)return $"Windows command 0x{op:X4}: pageMode={p[11]} clockHint={(p[14]&0x80)!=0} roleSwitch={p[15]}";
            return $"Windows command 0x{op:X4}";
        }
        if(op is 0x0406 or 0x0411 or 0x0413&&p.Length>=5&&Handle(p[3..5]))
            return op==0x0406&&p.Length>=6?$"Windows command 0x{op:X4}: reason=0x{p[5]:X2}":$"Windows command 0x{op:X4}";
        return null;
    }

    internal string? Event(ReadOnlySpan<byte> p,long now)
    {
        if(!Active(now)||p.Length<2||p.Length!=p[1]+2)return null;
        byte code=p[0];
        if(code==3&&p.Length>=13&&Address(p[5..11])) {
            if(p[2]==0)_handle=BinaryPrimitives.ReadUInt16LittleEndian(p[3..5])&0xFFF;
            return $"Mouse connection result=0x{p[2]:X2}";
        }
        if(code is 0x17 or 0x18 or 0x31 or 0x32 or 0x33 or 0x34 or 0x35&&p.Length>=8&&Address(p[2..8])) {
            if(code==0x32&&p.Length==11)return $"Mouse event 0x{code:X2}: io={p[8]} oob={p[9]} auth={p[10]}";
            return $"Mouse event 0x{code:X2}";
        }
        if(code==0x36&&p.Length==9&&Address(p[3..9]))return $"Mouse SSP result=0x{p[2]:X2}";
        if(code is 5 or 6 or 8 or 0x59&&p.Length>=5&&Handle(p[3..5]))
            return code==5&&p.Length>=6?$"Mouse disconnect status=0x{p[2]:X2} reason=0x{p[5]:X2}":$"Mouse event 0x{code:X2}: status=0x{p[2]:X2}";
        return null;
    }
}
