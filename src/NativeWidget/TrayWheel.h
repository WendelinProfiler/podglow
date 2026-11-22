#pragma once
#include <atomic>

inline bool PodglowTrayWheelHit(bool enabled,const RECT& rect,ULONGLONG located,ULONGLONG now,POINT point,bool shellSurface){
 return enabled&&located&&now>=located&&now-located<500&&shellSurface&&PtInRect(&rect,point);
}

// Shell notification icons do not receive WM_MOUSEWHEEL callbacks. A dedicated
// message thread filters only wheel input over our visible shell icon. It never
// stores mouse history, injects into Explorer or does device work in the hook.
class PodglowTrayWheel {
 HWND m_owner=nullptr;UINT m_message=0;UINT m_id=0;
 HANDLE m_thread=nullptr;DWORD m_threadId=0;HHOOK m_hook=nullptr;
 RECT m_rect{};ULONGLONG m_rectAt=0;std::atomic<bool> m_enabled{false};
 inline static thread_local PodglowTrayWheel* s_current=nullptr;
 static bool ShellSurface(POINT point){
  HWND hit=WindowFromPoint(point);if(!hit)return false;
  HWND root=GetAncestor(hit,GA_ROOT);DWORD rootPid=0,shellPid=0;GetWindowThreadProcessId(root,&rootPid);GetWindowThreadProcessId(GetShellWindow(),&shellPid);
  if(!shellPid||rootPid!=shellPid)return false;
  WCHAR name[96]{};GetClassNameW(root,name,96);
  return wcscmp(name,L"Shell_TrayWnd")==0||wcscmp(name,L"Shell_SecondaryTrayWnd")==0||
   wcscmp(name,L"NotifyIconOverflowWindow")==0||wcscmp(name,L"TopLevelWindowForOverflowXamlIsland")==0;
 }
 void Locate(){
  NOTIFYICONIDENTIFIER id{sizeof(id)};id.hWnd=m_owner;id.uID=m_id;RECT rect{};
  if(SUCCEEDED(Shell_NotifyIconGetRect(&id,&rect))&&rect.right>rect.left&&rect.bottom>rect.top){m_rect=rect;m_rectAt=GetTickCount64();}
  else{m_rect={};m_rectAt=0;}
 }
 static LRESULT CALLBACK Hook(int code,WPARAM message,LPARAM data){
  auto self=s_current;
  if(code==HC_ACTION&&message==WM_MOUSEWHEEL&&self&&self->m_enabled.load(std::memory_order_relaxed)){
   const auto& mouse=*reinterpret_cast<const MSLLHOOKSTRUCT*>(data);
   if(PodglowTrayWheelHit(true,self->m_rect,self->m_rectAt,GetTickCount64(),mouse.pt,PtInRect(&self->m_rect,mouse.pt)&&ShellSurface(mouse.pt))){
    const int delta=static_cast<short>(HIWORD(mouse.mouseData));
    if(delta&&PostMessageW(self->m_owner,self->m_message,static_cast<WPARAM>(static_cast<INT_PTR>(delta)),0))return 1;
   }
  }
  return CallNextHookEx(nullptr,code,message,data);
 }
 static DWORD WINAPI Run(void* context){
  auto self=static_cast<PodglowTrayWheel*>(context);s_current=self;
  MSG message{};PeekMessageW(&message,nullptr,WM_USER,WM_USER,PM_NOREMOVE);
  self->m_hook=SetWindowsHookExW(WH_MOUSE_LL,Hook,GetModuleHandleW(nullptr),0);
  if(!self->m_hook)return 1;
  self->Locate();UINT_PTR timer=SetTimer(nullptr,0,100,nullptr);
  SetPropW(self->m_owner,L"PodglowTrayWheelActive",reinterpret_cast<HANDLE>(1));
  while(GetMessageW(&message,nullptr,0,0)>0){if(message.message==WM_TIMER)self->Locate();else{TranslateMessage(&message);DispatchMessageW(&message);}}
  if(timer)KillTimer(nullptr,timer);UnhookWindowsHookEx(self->m_hook);self->m_hook=nullptr;
  RemovePropW(self->m_owner,L"PodglowTrayWheelActive");s_current=nullptr;return 0;
 }
public:
 void Start(HWND owner,UINT id,UINT message){if(m_thread)return;m_owner=owner;m_id=id;m_message=message;m_thread=CreateThread(nullptr,0,Run,this,0,&m_threadId);}
 void Enable(bool enabled){m_enabled.store(enabled,std::memory_order_relaxed);}
 void Stop(){if(!m_thread)return;Enable(false);for(int n=0;n<50;n++){if(PostThreadMessageW(m_threadId,WM_QUIT,0,0))break;if(WaitForSingleObject(m_thread,10)==WAIT_OBJECT_0)break;}WaitForSingleObject(m_thread,INFINITE);CloseHandle(m_thread);m_thread=nullptr;}
 ~PodglowTrayWheel(){Stop();}
};
