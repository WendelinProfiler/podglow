using WPods.Service;
using System.Buffers.Binary;
const ulong target=0x112233445566;
int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
byte[] Command(ulong address){var bytes=new byte[25];bytes[0]=0x0B;bytes[1]=0x04;bytes[2]=22;for(int i=0;i<6;i++)bytes[3+i]=(byte)(address>>(i*8));bytes.AsSpan(9).Fill(0xAA);return bytes;}
byte[] Event(int code,int size){var b=new byte[size];b[0]=(byte)code;b[1]=(byte)(size-2);return b;}
void SetAddress(byte[] bytes,int offset,ulong address){for(int i=0;i<6;i++)bytes[offset+i]=(byte)(address>>(8*i));}
var policy=new MouseBondRenewal();var original=Command(target);
Check(!policy.TryRewrite(original,0,out _),"Unarmed connections must be untouched");
Check(!policy.Arm(target,0,false),"Authorization is required");
Check(!policy.Arm(0,0,true),"Zero address rejected");
Check(!policy.Arm(0x1000000000000,0,true),"Oversize address rejected");
Check(policy.Arm(target,100,true),"Explicit arm accepted");
Check(!policy.TryRewrite(Command(target+1),101,out _),"Other devices untouched");
Check(!policy.TryRewrite(original.AsSpan(0,24),101,out _),"Malformed request untouched");
var connected=Event(3,13);SetAddress(connected,5,target);connected[3]=0x42;policy.Observe(connected,102);
Check(policy.TryRewrite(original,103,out var negative),"Target request rewritten");
Check(negative.Length==9&&negative[0]==0x0C&&negative[1]==4&&negative[2]==6,"Correct negative-reply wire shape");
Check(!negative.Contains((byte)0xAA),"Synthetic secret not copied into replacement");
Check(original.AsSpan(9).ToArray().All(b=>b==0xAA),"Policy does not read or mutate caller key storage");
Check(!policy.TryRewrite(original,104,out _),"Single-use renewal");
var ack=Event(0x0E,12);ack[2]=1;ack[3]=0x0C;ack[4]=4;SetAddress(ack,6,target+1);policy.Observe(ack,105);
Check(ack[3]==0x0C,"Other device acknowledgement untouched");SetAddress(ack,6,target);policy.Observe(ack,106);
Check(ack[3]==0x0B&&ack[4]==4,"Original command acknowledgement restored");
var key=Event(0x18,25);SetAddress(key,2,target+1);policy.Observe(key,107);
Check(!policy.Status(107).FreshKey,"Other device notification ignored");SetAddress(key,2,target);policy.Observe(key,108);
Check(policy.Status(108).FreshKey&&policy.Status(108).State!=MouseBondRenewal.Stage.Complete,"Fresh key alone is not success");
var auth=Event(6,5);auth[3]=0x42;policy.Observe(auth,109);
Check(policy.Status(109).State!=MouseBondRenewal.Stage.Complete,"Authentication alone is not encryption");
var encryption=Event(8,6);encryption[3]=0x42;encryption[5]=1;policy.Observe(encryption,110);
Check(policy.Status(110).State==MouseBondRenewal.Stage.Complete,"Fresh key, authentication and encryption required");
policy.Reset();Check(policy.Arm(target,0,true),"Reset allows next explicit renewal");
Check(!policy.TryRewrite(original,20001,out _)&&policy.Status(20001).State==MouseBondRenewal.Stage.Failed,"Renewal expires");
var rng=new Random(44);for(int i=0;i<10000;i++){var random=new byte[rng.Next(0,260)];rng.NextBytes(random);policy.Observe(random,21000);policy.TryRewrite(random,21000,out _);}checks++;
// An acknowledgement must still be translated after expiry so Windows can
// finish its outstanding command, but it must never resurrect a timed-out claim.
policy.Reset();Check(policy.Arm(target,0,true,0x42),"Known handle can seed an explicit claim");
Check(policy.TryRewrite(original,1,out _),"Second claim enters pairing");
policy.Observe(key,2);policy.Observe(auth,3);policy.Observe(encryption,4);
Check(policy.Status(4).State==MouseBondRenewal.Stage.Pairing,"Pending controller acknowledgement prevents success");
var lateAck=Event(0x0E,12);lateAck[2]=1;lateAck[3]=0x0C;lateAck[4]=4;SetAddress(lateAck,6,target);
policy.Observe(lateAck,20001);
Check(lateAck[3]==0x0B&&policy.Status(20001).State==MouseBondRenewal.Stage.Failed,"Late acknowledgement translated without reviving expired renewal");
policy.Reset();policy.Arm(target,0,true,0x42);policy.TryRewrite(original,1,out _);
var wrongHandle=(byte[])auth.Clone();wrongHandle[3]=0x43;policy.Observe(wrongHandle,2);
Check(!policy.Status(2).Authenticated,"Unrelated link cannot authenticate the mouse");
var refused=(byte[])auth.Clone();refused[2]=5;policy.Observe(refused,3);
Check(policy.Status(3).State==MouseBondRenewal.Stage.Failed&&policy.Status(3).Error==5,"Controller authentication error fails claim");
policy.Reset();
Check(policy.Arm(target,0,true,0x42,windowsOwnsSecurity:true),"Explicit Windows pairing accepted");
Check(policy.WindowsOwnsSecurity(1),"Direct authentication deferred while Windows owns pairing");
var foreignKey=(byte[])key.Clone();SetAddress(foreignKey,2,target+1);policy.Observe(foreignKey,2);
Check(!policy.Status(2).FreshKey,"Windows pairing still ignores another device's key event");
policy.Observe(key,3);policy.Observe(auth,4);policy.Observe(encryption,5);
Check(policy.Status(5).State==MouseBondRenewal.Stage.Complete,"Ordinary negative-key reply can complete fresh Windows pairing without rewriting a positive reply");
Check(!policy.WindowsOwnsSecurity(5),"Completed Windows pairing releases security ownership");
Check(!policy.TryRewrite(original,6,out _),"Newly established bond must not be invalidated on reconnection");
policy.Reset();policy.Arm(target,0,true,0x42,windowsOwnsSecurity:true);
Check(!policy.WindowsOwnsSecurity(20001),"Abandoned Windows pairing ownership expires");
policy.Reset();policy.Arm(target,0,true,0x42);
Check(!policy.WindowsOwnsSecurity(1),"Existing direct-HID renewal keeps its own security flow");
policy.Reset();policy.Arm(target,0,true,0x42,windowsOwnsSecurity:true);
var sspFailure=Event(0x36,9);sspFailure[2]=5;SetAddress(sspFailure,3,target+1);policy.Observe(sspFailure,1);
Check(policy.WindowsOwnsSecurity(1),"Unrelated SSP failure cannot interrupt Windows pairing");
SetAddress(sspFailure,3,target);policy.Observe(sspFailure,2);
Check(policy.Status(2).State==MouseBondRenewal.Stage.Failed&&policy.Status(2).Error==5&&!policy.WindowsOwnsSecurity(2),"Real target SSP error ends pairing ownership without waiting for timeout");
Console.WriteLine($"PASS {checks} renewal checks, including 10000 malformed/unrelated packets; no device, registry or real key accessed");
