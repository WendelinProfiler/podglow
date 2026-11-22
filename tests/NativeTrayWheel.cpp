#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <cwchar>
#include <cstdio>
#include "../src/NativeWidget/TrayWheel.h"
int main(){
 RECT rect{-80,100,-40,140};int count=0;
 auto check=[&](bool value,const char* label){if(!value){std::printf("FAIL %s\n",label);ExitProcess(1);}count++;};
 check(PodglowTrayWheelHit(true,rect,1000,1100,{-60,120},true),"own visible tray icon on negative-coordinate monitor");
 check(PodglowTrayWheelHit(true,rect,1000,1100,{-80,100},true),"left/top edges included");
 check(!PodglowTrayWheelHit(true,rect,1000,1100,{-40,120},true),"neighboring icon excluded");
 check(!PodglowTrayWheelHit(true,rect,1000,1100,{-60,140},true),"bottom edge excluded");
 check(!PodglowTrayWheelHit(true,rect,1000,1500,{-60,120},true),"stale icon placement excluded");
 check(!PodglowTrayWheelHit(true,rect,0,1100,{-60,120},true),"missing icon excluded");
 check(!PodglowTrayWheelHit(false,rect,1000,1100,{-60,120},true),"disconnected input excluded");
 check(!PodglowTrayWheelHit(true,rect,1000,1100,{-60,120},false),"covered or hidden shell icon excluded");
 check(!PodglowTrayWheelHit(true,rect,1200,1100,{-60,120},true),"invalid timestamp excluded");
 std::printf("PASS %d tray wheel boundary and routing checks\n",count);
}
