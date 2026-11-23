using WPods.Service;
using System.Buffers.Binary;
const ulong target=0x112233445566;
var trace=new MouseNegotiationTrace();int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
void Address(byte[] p,int offset,ulong address){for(int i=0;i<6;i++)p[offset+i]=(byte)(address>>(8*i));}
byte[] Cmd(ushort opcode,int size){var p=new byte[size];BinaryPrimitives.WriteUInt16LittleEndian(p,opcode);p[2]=(byte)(size-3);Address(p,3,target);return p;}
byte[] Event(byte code,int size){var p=new byte[size];p[0]=code;p[1]=(byte)(size-2);return p;}
var key=Cmd(0x040B,25);key.AsSpan(9).Fill(0xAA);
Check(trace.Command(key,0)==null,"Unarmed traffic ignored");trace.Arm(target,0);
Check(trace.Command(key,1)=="Windows command 0x040B","Key bytes never formatted");
Address(key,3,target+1);Check(trace.Command(key,2)==null,"Other device key command ignored");Address(key,3,target);
var connect=Event(3,13);Address(connect,5,target);connect[3]=0x42;
Check(trace.Event(connect,3)=="Mouse connection result=0x00","Target connection associated");
var closed=Event(5,6);closed[3]=0x43;closed[5]=0x13;
Check(trace.Event(closed,4)==null,"Other handle ignored");closed[3]=0x42;
Check(trace.Event(closed,5)?.Contains("reason=0x13")==true,"Remote close reason visible");
var confirm=Event(0x33,12);Address(confirm,2,target);confirm.AsSpan(8).Fill(0xCC);
Check(trace.Event(confirm,6)=="Mouse event 0x33","Numeric confirmation value never formatted");
var notified=Event(0x18,25);Address(notified,2,target);notified.AsSpan(8).Fill(0xBB);
Check(trace.Event(notified,7)=="Mouse event 0x18","Notified key never formatted");
Check(trace.Command(key,90001)==null&&trace.Event(closed,90001)==null,"Trace window expires");
trace.Reset();Check(trace.Event(connect,8)==null,"Reset removes target");
trace.Arm(target,0);var random=new Random(16);
for(int i=0;i<10000;i++){var p=new byte[random.Next(0,260)];random.NextBytes(p);trace.Command(p,1);trace.Event(p,1);}checks++;
Console.WriteLine($"PASS {checks} metadata trace checks and 10000 malformed packets; no keys or devices accessed");
