#pragma once
#include <windows.h>
#include <cstdint>
struct alignas(8) AirPodsIslandState {LONG sequence;uint32_t magic=0x41504931;uint64_t tick;uint32_t connected,backend,caps,busy,error;int32_t left=-1,right=-1,caseBattery=-1,charging=0,earLeft=-1,earRight=-1,mode=-1,conversation=-1,personalized=-1,earDetection=-1,micSide=-1,autoMic=1,micMuted=-1,volume=-1,isOutput=0,stereoMic=0;uint32_t reserved=0;uint64_t batteryTick=0;wchar_t name[128]{},message[160]{};};
static_assert(sizeof(AirPodsIslandState)==688);
inline bool AirPodsIslandFresh(const AirPodsIslandState& s, uint64_t now){return s.tick<=now&&now-s.tick<4000;}
inline bool ReadAirPodsIslandState(AirPodsIslandState& s, PCWSTR mapName=L"Local\\IslandAirPods.v1"){
 HANDLE h=OpenFileMappingW(FILE_MAP_READ,FALSE,mapName);if(!h)return false;
 auto p=static_cast<const AirPodsIslandState*>(MapViewOfFile(h,FILE_MAP_READ,0,0,sizeof(s)));bool ok=false;
 if(p){for(int i=0;i<8;i++){
  LONG before=p->sequence;if(before&1)continue;MemoryBarrier();AirPodsIslandState next;memcpy(&next,p,sizeof(next));MemoryBarrier();
  if(before==p->sequence){ok=next.magic==0x41504931;if(ok){next.name[127]=0;next.message[159]=0;s=next;}break;}
 }UnmapViewOfFile(p);}CloseHandle(h);return ok;
}
