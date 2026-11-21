#pragma once
#include <windows.h>
#include <cstdint>
#include <string>
struct alignas(8) AirPodsUxState {
 LONG sequence=0;uint32_t magic=0x41505531;uint64_t tick=0;
 uint64_t caseSeen=0,leftSeen=0,rightSeen=0;int32_t requestedMode=-1,confirmedMode=-1,phase=0;uint32_t requestId=0;
 uint64_t actionTick=0;int32_t command=0,value=0;uint32_t version=1,flags=0;
};
static_assert(sizeof(AirPodsUxState)==80);
inline bool ReadAirPodsUxState(AirPodsUxState& value,PCWSTR name=L"Local\\IslandAirPods.Ux.v1"){
 HANDLE map=OpenFileMappingW(FILE_MAP_READ,FALSE,name);if(!map)return false;
 auto p=static_cast<const AirPodsUxState*>(MapViewOfFile(map,FILE_MAP_READ,0,0,sizeof(value)));bool ok=false;
 if(p){for(int n=0;n<8;n++){LONG before=p->sequence;if(before&1)continue;MemoryBarrier();AirPodsUxState next;memcpy(&next,p,sizeof(next));MemoryBarrier();if(before==p->sequence){ok=next.magic==0x41505531&&next.version==1;if(ok)value=next;break;}}UnmapViewOfFile(p);}CloseHandle(map);return ok;
}
inline uint64_t AirPodsUnixMs(){FILETIME f;GetSystemTimeAsFileTime(&f);ULARGE_INTEGER n;n.LowPart=f.dwLowDateTime;n.HighPart=f.dwHighDateTime;return (n.QuadPart-116444736000000000ULL)/10000;}
inline std::wstring AirPodsAgeLabel(uint64_t seen,uint64_t now,bool cached=false){
 if(!seen||seen>now+60000)return L"Stand unbekannt";uint64_t age=now>seen?now-seen:0;
 if(age<15000&&!cached)return L"Gerade aktualisiert";
 if(age<1000&&cached)return L"Zuletzt: eben";
 std::wstring prefix=cached?L"Zuletzt vor ":L"Vor ";
 if(age<60000)return prefix+std::to_wstring(age/1000)+L" Sek.";
 if(age<3600000)return prefix+std::to_wstring(age/60000)+L" Min.";
 if(age<86400000)return prefix+std::to_wstring(age/3600000)+L" Std.";
 return prefix+std::to_wstring(age/86400000)+(age/86400000==1?L" Tag":L" Tagen");
}
inline PCWSTR AirPodsModeName(int mode){switch(mode){case 1:return L"Aus";case 2:return L"Geräuschunterdrückung";case 3:return L"Transparenz";case 4:return L"Adaptiv";default:return L"Hörmodus";}}
inline PCWSTR AirPodsModeHint(int mode){switch(mode){case 1:return L"Ohne aktive Geräuschkontrolle.";case 2:return L"Blendet störende Umgebungsgeräusche aus.";case 3:return L"Du hörst deine Umgebung weiterhin.";case 4:return L"Passt die Geräuschkontrolle an deine Umgebung an.";default:return L"Wähle, wie viel du von deiner Umgebung hörst.";}}
