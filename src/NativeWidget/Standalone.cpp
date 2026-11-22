#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <shellapi.h>
#include <d2d1.h>
#include <dwrite.h>
#include <dwmapi.h>
#include <wincodec.h>
#include <winrt/base.h>
#include <array>
#include <algorithm>
#include <cmath>
#include <string>
#include <vector>
#include <fstream>
#include <iterator>
#include <map>
#include <chrono>
#include "../Shared/AirPodsState.h"
#include "../Shared/AirPodsUx.h"
#include "EnglishHelp.h"
#include "TrayWheel.h"
#include "TrayGlyph.h"

static bool g_preview=false;
static std::wstring g_directory;
static constexpr UINT TrayCallback=WM_APP+41;
static D2D1_COLOR_F Fg(float a){return D2D1::ColorF(0xffffff,a);}
static D2D1_COLOR_F BgSolid(float a){return D2D1::ColorF(0x171719,a);}

// Hosting adapter only. All AirPods content, hit regions, commands, settings,
// state feedback and control animations come from the same shared presentation.
class Panel {
 winrt::com_ptr<ID2D1Factory> m_factory;
 winrt::com_ptr<ID2D1DCRenderTarget> m_target;
 winrt::com_ptr<IDWriteFactory> m_dwrite;
 HDC m_dc=nullptr;HBITMAP m_bitmap=nullptr;HGDIOBJ m_oldBitmap=nullptr;SIZE m_surface{};
 HWND m_backdrop=nullptr;bool m_visible=false,m_tracking=false,m_blur=false,m_dirty=true,m_updateAvailable=false;
 float m_width=0,m_height=0,m_opacity=0;int m_scroll=0;ULONGLONG m_lastFrame=0;
 NOTIFYICONDATAW m_tray{};UINT m_taskbarCreated=0;PodglowTrayWheel m_trayWheel;ULONGLONG m_trayWheelRead=0;UINT m_trayWheelEvents=0;int m_trayWheelRest=0;HICON m_trayGlyph=nullptr;int m_trayTint=-1;
protected:
 HWND m_hwnd=nullptr;float m_scale=1;SIZE m_size{};POINT m_contentOffset{};bool m_idleMotion=false;
 virtual void Prepare(float)=0;virtual void Draw(ID2D1RenderTarget*,ID2D1SolidColorBrush*)=0;
 virtual void OnOpen(){}virtual void OnClose(){}virtual void OnCaptureLost(){}virtual void OnSurfaceReset(){}
 virtual bool AnimateContents(double){return false;}
 virtual bool OnMouseMove(POINT){return false;}virtual bool OnMouseLeave(){return false;}
 virtual bool OnMouseDown(POINT){return false;}virtual bool OnMouseUp(POINT){return false;}virtual bool OnWheel(int){return false;}
 winrt::com_ptr<IDWriteTextFormat> MakeFormat(PCWSTR family,DWRITE_FONT_WEIGHT weight,float size,DWRITE_TEXT_ALIGNMENT alignment){
  winrt::com_ptr<IDWriteTextFormat> f;
  if(SUCCEEDED(m_dwrite->CreateTextFormat(family,nullptr,weight,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,size,L"",f.put()))){
   f->SetTextAlignment(alignment);f->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);f->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
   DWRITE_TRIMMING trim{DWRITE_TRIMMING_GRANULARITY_CHARACTER,0,0};winrt::com_ptr<IDWriteInlineObject> ellipsis;m_dwrite->CreateEllipsisTrimmingSign(f.get(),ellipsis.put());f->SetTrimming(&trim,ellipsis.get());
  }return f;
 }
 void Reformat(){Prepare(m_scale);m_scroll=0;m_dirty=true;InvalidateRect(m_hwnd,nullptr,FALSE);}
 void Close(){m_visible=false;OnClose();ShowWindow(m_hwnd,SW_HIDE);ShowWindow(m_backdrop,SW_HIDE);}
private:
 static LRESULT CALLBACK BackdropProc(HWND h,UINT m,WPARAM w,LPARAM l){if(m==WM_MOUSEACTIVATE)return MA_NOACTIVATE;if(m==WM_NCHITTEST)return HTTRANSPARENT;if(m==WM_ERASEBKGND)return 1;return DefWindowProcW(h,m,w,l);}
 static LRESULT CALLBACK WindowProc(HWND h,UINT m,WPARAM w,LPARAM l){auto self=reinterpret_cast<Panel*>(GetWindowLongPtrW(h,GWLP_USERDATA));if(m==WM_NCCREATE){self=static_cast<Panel*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams);SetWindowLongPtrW(h,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(self));self->m_hwnd=h;}return self?self->Message(m,w,l):DefWindowProcW(h,m,w,l);}
 void FreeSurface(){m_target=nullptr;if(m_dc){SelectObject(m_dc,m_oldBitmap);DeleteObject(m_bitmap);DeleteDC(m_dc);}m_dc=nullptr;m_bitmap=nullptr;m_oldBitmap=nullptr;m_surface={};}
 bool Surface(int w,int h){
  if(m_dc&&m_target&&m_surface.cx==w&&m_surface.cy==h)return true;if(m_dc){SelectObject(m_dc,m_oldBitmap);DeleteObject(m_bitmap);DeleteDC(m_dc);m_dc=nullptr;m_bitmap=nullptr;m_oldBitmap=nullptr;}
  BITMAPINFO info{};info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=w;info.bmiHeader.biHeight=-h;info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;info.bmiHeader.biCompression=BI_RGB;
  void* pixels=nullptr;m_dc=CreateCompatibleDC(nullptr);m_bitmap=CreateDIBSection(m_dc,&info,DIB_RGB_COLORS,&pixels,nullptr,0);if(!m_dc||!m_bitmap||!pixels){FreeSurface();return false;}
  m_oldBitmap=SelectObject(m_dc,m_bitmap);m_surface={w,h};
  auto props=D2D1::RenderTargetProperties(D2D1_RENDER_TARGET_TYPE_SOFTWARE,D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED),96,96,D2D1_RENDER_TARGET_USAGE_GDI_COMPATIBLE);
  if(!m_target&&FAILED(m_factory->CreateDCRenderTarget(&props,m_target.put()))){FreeSurface();return false;}m_target->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);return true;
 }
 void ConfigureBackdrop(){
  DWORD transparent=1,bytes=sizeof(transparent);RegGetValueW(HKEY_CURRENT_USER,L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",L"EnableTransparency",RRF_RT_REG_DWORD,nullptr,&transparent,&bytes);
  if(!transparent)return;
  struct Accent{int state,flags;DWORD color;int animation;};struct Composition{int attribute;void* data;SIZE_T size;};
  using SetComposition=BOOL(WINAPI*)(HWND,Composition*);auto set=reinterpret_cast<SetComposition>(GetProcAddress(GetModuleHandleW(L"user32.dll"),"SetWindowCompositionAttribute"));
  if(set){Accent accent{4,2,0xa61b1b1d,0};Composition data{19,&accent,sizeof(accent)};m_blur=set(m_backdrop,&data)!=FALSE;}
 }
 void UpdateTray(){
  AirPodsIslandState s{};bool connected=ReadAirPodsIslandState(s)&&AirPodsIslandFresh(s,GetTickCount64())&&s.connected;
  if(m_trayTint!=int(connected)){if(auto icon=PodglowTrayGlyph(m_factory.get(),connected)){auto previous=m_trayGlyph;m_trayGlyph=icon;m_tray.hIcon=icon;m_tray.uFlags=NIF_ICON|NIF_MESSAGE|NIF_TIP|NIF_SHOWTIP;Shell_NotifyIconW(NIM_MODIFY,&m_tray);if(previous)DestroyIcon(previous);m_trayTint=int(connected);}}
  auto tip=std::wstring(L"Podglow\n")+(connected?L"Connected":L"Not connected")+L"\nScroll to adjust volume"+(m_updateAvailable?L"\nUpdate available":L"");
  wcsncpy_s(m_tray.szTip,tip.c_str(),_TRUNCATE);Shell_NotifyIconW(NIM_MODIFY,&m_tray);
 }
 void AddTray(){m_tray={sizeof(m_tray)};m_tray.hWnd=m_hwnd;m_tray.uID=1;m_tray.uFlags=NIF_ICON|NIF_MESSAGE|NIF_TIP|NIF_SHOWTIP;m_tray.uCallbackMessage=TrayCallback;m_tray.hIcon=m_trayGlyph?m_trayGlyph:LoadIconW(nullptr,IDI_APPLICATION);wcscpy_s(m_tray.szTip,L"Podglow");Shell_NotifyIconW(NIM_ADD,&m_tray);m_tray.uVersion=NOTIFYICON_VERSION_4;Shell_NotifyIconW(NIM_SETVERSION,&m_tray);m_trayTint=-1;UpdateTray();}
 void Credits(){
  std::ifstream input(g_directory+L"\\CREDITS.md",std::ios::binary);std::string utf8((std::istreambuf_iterator<char>(input)),{});
  if(utf8.empty())utf8="Credits and license information is available in CREDITS.md beside the application.";
  int n=MultiByteToWideChar(CP_UTF8,0,utf8.data(),static_cast<int>(utf8.size()),nullptr,0);std::wstring text(n,L' ');MultiByteToWideChar(CP_UTF8,0,utf8.data(),static_cast<int>(utf8.size()),text.data(),n);
  std::wstring normalized;for(size_t i=0;i<text.size();i++){if(text[i]==L'\n'&&(i==0||text[i-1]!=L'\r'))normalized+=L'\r';normalized+=text[i];}text=std::move(normalized);
  auto h=CreateWindowExW(WS_EX_TOOLWINDOW,L"AirPodsCompanionCredits",L"Podglow — Credits & licenses",WS_OVERLAPPEDWINDOW|WS_VISIBLE,CW_USEDEFAULT,CW_USEDEFAULT,800,650,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
  auto edit=CreateWindowExW(0,L"EDIT",text.c_str(),WS_CHILD|WS_VISIBLE|WS_VSCROLL|ES_MULTILINE|ES_READONLY|ES_AUTOVSCROLL,0,0,780,610,h,nullptr,GetModuleHandleW(nullptr),nullptr);
  SendMessageW(edit,WM_SETFONT,reinterpret_cast<WPARAM>(GetStockObject(DEFAULT_GUI_FONT)),TRUE);SetWindowLongPtrW(h,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(edit));SetForegroundWindow(h);
 }
 bool AutomaticUpdates(){DWORD value=1,bytes=sizeof(value);RegGetValueW(HKEY_CURRENT_USER,L"Software\\AirPodsCompanion",L"CheckForUpdates",RRF_RT_REG_DWORD,nullptr,&value,&bytes);return value!=0;}
 void LaunchUpdater(bool background){
  if(g_preview)return;
  auto host=g_directory+L"\\IslandAirPodsHost.exe";if(GetFileAttributesW(host.c_str())==INVALID_FILE_ATTRIBUTES)host=g_directory+L"\\engine\\IslandAirPodsHost.exe";
  if(GetFileAttributesW(host.c_str())==INVALID_FILE_ATTRIBUTES){if(!background)MessageBoxW(m_hwnd,L"Install the complete companion package to check for updates.",L"Podglow",MB_OK|MB_ICONINFORMATION);return;}
  auto result=reinterpret_cast<INT_PTR>(ShellExecuteW(nullptr,L"open",host.c_str(),background?L"--companion-update --background":L"--companion-update",g_directory.c_str(),background?SW_HIDE:SW_SHOWNORMAL));
  if(result<=32&&!background)MessageBoxW(m_hwnd,L"The update assistant could not start. Please try again.",L"Podglow",MB_OK|MB_ICONINFORMATION);
 }
 void UpdateNotice(){
  if(g_preview)return;m_updateAvailable=true;m_tray.uFlags=NIF_INFO;wcscpy_s(m_tray.szInfoTitle,L"Podglow update");wcscpy_s(m_tray.szInfo,L"A new version is available. Click to review and install it.");m_tray.dwInfoFlags=NIIF_INFO;Shell_NotifyIconW(NIM_MODIFY,&m_tray);m_tray.uFlags=NIF_ICON|NIF_MESSAGE|NIF_TIP|NIF_SHOWTIP;UpdateTray();
 }
 void Menu(){POINT p{};GetCursorPos(&p);auto menu=CreatePopupMenu();AppendMenuW(menu,MF_STRING,1,L"Open widget");AppendMenuW(menu,MF_STRING|(g_preview?MF_GRAYED:0),4,m_updateAvailable?L"Update available...":L"Check for updates...");AppendMenuW(menu,MF_STRING|(AutomaticUpdates()?MF_CHECKED:0)|(g_preview?MF_GRAYED:0),5,L"Check for updates automatically");AppendMenuW(menu,MF_STRING,2,L"Credits & licenses");AppendMenuW(menu,MF_SEPARATOR,0,nullptr);AppendMenuW(menu,MF_STRING,3,L"Exit");SetForegroundWindow(m_hwnd);int selected=TrackPopupMenu(menu,TPM_RETURNCMD|TPM_RIGHTBUTTON,p.x,p.y,0,m_hwnd,nullptr);DestroyMenu(menu);if(selected==1)Open();else if(selected==2)Credits();else if(selected==3)DestroyWindow(m_hwnd);else if(selected==4)LaunchUpdater(false);else if(selected==5){DWORD value=AutomaticUpdates()?0:1;HKEY key=nullptr;if(RegCreateKeyExW(HKEY_CURRENT_USER,L"Software\\AirPodsCompanion",0,nullptr,0,KEY_SET_VALUE,nullptr,&key,nullptr)==ERROR_SUCCESS){RegSetValueExW(key,L"CheckForUpdates",0,REG_DWORD,reinterpret_cast<BYTE*>(&value),sizeof(value));RegCloseKey(key);if(value&&!g_preview)SetTimer(m_hwnd,2,1000,nullptr);}}}
 void Render(double dt){
  if(!m_visible)return;bool changed=AnimateContents(dt);
  BOOL motion=TRUE;SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION,0,&motion,0);
  const float speed=motion?1.f-std::exp(-static_cast<float>(dt)/.055f):1.f;
  float tw=m_size.cx+16*m_scale,th=m_size.cy+16*m_scale;
  bool moving=std::abs(tw-m_width)>.5f||std::abs(th-m_height)>.5f||m_opacity<.999f;if(!moving&&!changed&&!m_dirty)return;m_dirty=false;
  m_width+=(tw-m_width)*speed;m_height+=(th-m_height)*speed;m_opacity+=(1-m_opacity)*speed;
  if(std::abs(tw-m_width)<.5f)m_width=tw;if(std::abs(th-m_height)<.5f)m_height=th;
  HMONITOR monitor=MonitorFromWindow(m_hwnd,MONITOR_DEFAULTTOPRIMARY);MONITORINFO mi{sizeof(mi)};GetMonitorInfoW(monitor,&mi);
  int width=static_cast<int>(std::ceil(m_width)),height=std::min<int>(static_cast<int>(std::ceil(m_height)),mi.rcWork.bottom-mi.rcWork.top-24);
  if(width<=0||height<=0||!Surface(width,height))return;
  m_scroll=std::clamp<int>(m_scroll,0,std::max<int>(0,m_size.cy+static_cast<int>(16*m_scale)-height));
  POINT pos{mi.rcWork.right-width-static_cast<int>(12*m_scale),mi.rcWork.bottom-height-static_cast<int>(12*m_scale)};
  SIZE size{width,height};RECT rect{0,0,width,height};if(FAILED(m_target->BindDC(m_dc,&rect)))return;
  m_target->BeginDraw();m_target->Clear(D2D1::ColorF(0,0));
  winrt::com_ptr<ID2D1SolidColorBrush> brush;m_target->CreateSolidColorBrush(BgSolid(m_blur?.38f:.98f),brush.put());
  const float radius=24*m_scale;D2D1_ROUNDED_RECT frame{{1,1,width-1.f,height-1.f},radius,radius};m_target->FillRoundedRectangle(frame,brush.get());
  D2D1_GRADIENT_STOP stops[]{{0,D2D1::ColorF(0xffffff,.16f)},{.24f,D2D1::ColorF(0xffffff,0)},{1,D2D1::ColorF(0xffffff,.025f)}};
  winrt::com_ptr<ID2D1GradientStopCollection> collection;winrt::com_ptr<ID2D1LinearGradientBrush> gradient;
  if(SUCCEEDED(m_target->CreateGradientStopCollection(stops,3,collection.put()))&&SUCCEEDED(m_target->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties({0,0},{0,static_cast<float>(height)}),collection.get(),gradient.put())))m_target->FillRoundedRectangle(frame,gradient.get());
  brush->SetColor(Fg(.35f));m_target->DrawRoundedRectangle(frame,brush.get(),1.1f*m_scale);
  D2D1_ROUNDED_RECT inner{{4*m_scale,4*m_scale,width-4*m_scale,height-4*m_scale},radius-3*m_scale,radius-3*m_scale};brush->SetColor(Fg(.075f));m_target->DrawRoundedRectangle(inner,brush.get(),.65f*m_scale);
  m_contentOffset={static_cast<LONG>((width-m_size.cx)/2),static_cast<LONG>(8*m_scale)-m_scroll};
  winrt::com_ptr<ID2D1RoundedRectangleGeometry> clip;m_factory->CreateRoundedRectangleGeometry(inner,clip.put());winrt::com_ptr<ID2D1Layer> layer;m_target->CreateLayer(nullptr,layer.put());
  m_target->PushLayer(D2D1::LayerParameters(D2D1::InfiniteRect(),clip.get()),layer.get());m_target->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(m_contentOffset.x),static_cast<float>(m_contentOffset.y)));Draw(m_target.get(),brush.get());m_target->SetTransform(D2D1::IdentityMatrix());m_target->PopLayer();
  HRESULT hr=m_target->EndDraw();if(FAILED(hr)){OnSurfaceReset();m_target=nullptr;m_dirty=true;return;}
  SetWindowPos(m_backdrop,m_hwnd,pos.x,pos.y,width,height,SWP_NOACTIVATE|SWP_NOOWNERZORDER);
  auto region=CreateRoundRectRgn(0,0,width+1,height+1,static_cast<int>(2*radius),static_cast<int>(2*radius));if(!SetWindowRgn(m_backdrop,region,TRUE))DeleteObject(region);
  POINT origin{};BLENDFUNCTION blend{AC_SRC_OVER,0,static_cast<BYTE>(std::clamp(m_opacity,0.f,1.f)*255),AC_SRC_ALPHA};UpdateLayeredWindow(m_hwnd,nullptr,&pos,&size,m_dc,&origin,0,&blend,ULW_ALPHA);
 }
 LRESULT Message(UINT msg,WPARAM w,LPARAM l){
  if(msg==m_taskbarCreated){AddTray();return 0;}
  POINT p{GET_X_LPARAM(l)-m_contentOffset.x,GET_Y_LPARAM(l)-m_contentOffset.y};
  switch(msg){
   case WM_TIMER:{if(w==2){SetTimer(m_hwnd,2,3600000,nullptr);if(!g_preview&&AutomaticUpdates())LaunchUpdater(true);return 0;}ULONGLONG now=GetTickCount64();if(now-m_trayWheelRead>=100){AirPodsIslandState state{};m_trayWheel.Enable(g_preview||(ReadAirPodsIslandState(state)&&AirPodsIslandFresh(state,now)&&state.connected&&state.volume>=0));m_trayWheelRead=now;}double dt=m_lastFrame?std::clamp((now-m_lastFrame)/1000.0,.001,.05):.016;m_lastFrame=now;Render(dt);if(now/1500!=(now-static_cast<ULONGLONG>(dt*1000))/1500)UpdateTray();return 0;}
   case WM_MOUSEMOVE:if(!m_tracking){TRACKMOUSEEVENT t{sizeof(t),TME_LEAVE,m_hwnd,0};TrackMouseEvent(&t);m_tracking=true;}m_dirty=OnMouseMove(p)||m_dirty;return 0;
   case WM_MOUSELEAVE:m_tracking=false;m_dirty=OnMouseLeave()||m_dirty;return 0;
   case WM_LBUTTONDOWN:m_dirty=true;OnMouseDown(p);return 0;
   case WM_LBUTTONUP:m_dirty=true;OnMouseUp(p);return 0;
   case WM_CAPTURECHANGED:if(reinterpret_cast<HWND>(l)!=m_hwnd)OnCaptureLost();return 0;
   case WM_MOUSEWHEEL:m_dirty=true;if(!OnWheel(GET_WHEEL_DELTA_WPARAM(w)))m_scroll-=GET_WHEEL_DELTA_WPARAM(w)/WHEEL_DELTA*static_cast<int>(40*m_scale);return 0;
   case WM_KEYDOWN:if(w==VK_ESCAPE){Close();return 0;}break;
   case WM_ACTIVATE:if(LOWORD(w)==WA_INACTIVE&&!IsChild(m_hwnd,reinterpret_cast<HWND>(l)))Close();return 0;
   case WM_DPICHANGED:m_scale=HIWORD(w)/96.f;Reformat();return 0;
   case WM_PRINT:case WM_PRINTCLIENT:if(m_dc)BitBlt(reinterpret_cast<HDC>(w),0,0,m_surface.cx,m_surface.cy,m_dc,0,0,SRCCOPY);return 1;
   case WM_ERASEBKGND:return 1;
   case WM_CLOSE:Close();return 0;
   case WM_DESTROY:m_trayWheel.Stop();Shell_NotifyIconW(NIM_DELETE,&m_tray);PostQuitMessage(0);return 0;
   case WM_APP+193:Toggle();return 0;
   case WM_APP+194:Credits();return 0;
   case WM_APP+195:DestroyWindow(m_hwnd);return 0;
   case WM_APP+196:UpdateNotice();return 0;
   case WM_APP+197:m_updateAvailable=false;UpdateTray();return 0;
   case WM_APP+198:{int delta=static_cast<int>(static_cast<INT_PTR>(w));m_trayWheelRest+=delta;int notches=m_trayWheelRest/WHEEL_DELTA;m_trayWheelRest%=WHEEL_DELTA;bool changed=false;for(int i=0;i<std::min(20,std::abs(notches));i++)changed=OnWheel(notches>0?WHEEL_DELTA:-WHEEL_DELTA)||changed;if(changed){m_dirty=true;UpdateTray();}SetPropW(m_hwnd,L"PodglowTrayWheelEvents",reinterpret_cast<HANDLE>(static_cast<UINT_PTR>(++m_trayWheelEvents)));SetPropW(m_hwnd,L"PodglowTrayWheelDelta",reinterpret_cast<HANDLE>(static_cast<INT_PTR>(delta)));return 0;}
   case TrayCallback:if(LOWORD(l)==NIN_SELECT||LOWORD(l)==NIN_KEYSELECT)Toggle();else if(LOWORD(l)==WM_CONTEXTMENU)Menu();else if(LOWORD(l)==NIN_BALLOONUSERCLICK)LaunchUpdater(false);return 0;
  }return DefWindowProcW(m_hwnd,msg,w,l);
 }
public:
 bool Create(){
  if(FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,m_factory.put()))||FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,__uuidof(IDWriteFactory),reinterpret_cast<IUnknown**>(m_dwrite.put()))))return false;
  auto instance=GetModuleHandleW(nullptr);WNDCLASSW wc{};wc.hInstance=instance;wc.lpfnWndProc=BackdropProc;wc.lpszClassName=L"AirPodsCompanionBackdrop";RegisterClassW(&wc);
  wc.lpszClassName=L"AirPodsCompanionCredits";wc.lpfnWndProc=[](HWND h,UINT m,WPARAM w,LPARAM l)->LRESULT{if(m==WM_SIZE){auto edit=reinterpret_cast<HWND>(GetWindowLongPtrW(h,GWLP_USERDATA));if(edit)MoveWindow(edit,0,0,LOWORD(l),HIWORD(l),TRUE);return 0;}return DefWindowProcW(h,m,w,l);};RegisterClassW(&wc);
  wc.lpszClassName=L"AirPodsCompanionWidget";wc.lpfnWndProc=WindowProc;wc.hCursor=LoadCursorW(nullptr,IDC_ARROW);RegisterClassW(&wc);
  m_backdrop=CreateWindowExW(WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE|WS_EX_TRANSPARENT,L"AirPodsCompanionBackdrop",L"Podglow backdrop",WS_POPUP,0,0,1,1,nullptr,nullptr,instance,nullptr);
  m_hwnd=CreateWindowExW(WS_EX_LAYERED|WS_EX_TOOLWINDOW,L"AirPodsCompanionWidget",L"Podglow",WS_POPUP,0,0,1,1,m_backdrop,nullptr,instance,this);
  if(!m_hwnd||!m_backdrop)return false;ConfigureBackdrop();m_taskbarCreated=RegisterWindowMessageW(L"TaskbarCreated");AddTray();m_trayWheel.Start(m_hwnd,1,WM_APP+198);SetTimer(m_hwnd,1,16,nullptr);if(!g_preview)SetTimer(m_hwnd,2,15000,nullptr);return true;
 }
 void Open(){
  HMONITOR monitor=MonitorFromPoint(POINT{GetSystemMetrics(SM_CXSCREEN)-1,GetSystemMetrics(SM_CYSCREEN)-1},MONITOR_DEFAULTTOPRIMARY);
  MONITORINFO mi{sizeof(mi)};GetMonitorInfoW(monitor,&mi);SetWindowPos(m_hwnd,nullptr,mi.rcWork.right-2,mi.rcWork.bottom-2,1,1,SWP_NOZORDER|SWP_NOACTIVATE);
  m_scale=GetDpiForWindow(m_hwnd)/96.f;Prepare(m_scale);m_scroll=0;OnOpen();m_width=m_size.cx+16*m_scale;m_height=m_size.cy+16*m_scale;m_opacity=0;m_visible=true;m_dirty=true;
  ShowWindow(m_backdrop,SW_SHOWNOACTIVATE);ShowWindow(m_hwnd,SW_SHOW);SetForegroundWindow(m_hwnd);Render(.016);
 }
 void Toggle(){if(m_visible)Close();else Open();}
 ~Panel(){m_trayWheel.Stop();Shell_NotifyIconW(NIM_DELETE,&m_tray);FreeSurface();if(m_trayGlyph)DestroyIcon(m_trayGlyph);if(m_backdrop)DestroyWindow(m_backdrop);}
};

static bool ReadCompanionState(AirPodsIslandState& s){
 if(!g_preview)return ReadAirPodsIslandState(s);
 s={};s.sequence=2;s.tick=GetTickCount64();s.connected=1;s.backend=2;s.busy=wcsstr(GetCommandLineW(),L"--preview-busy-volume")?1:0;s.caps=0x7ff;s.left=78;s.right=76;s.caseBattery=42;s.charging=4;s.earLeft=s.earRight=0;s.mode=2;s.conversation=0;s.personalized=0;s.earDetection=1;s.autoMic=1;s.micMuted=0;s.volume=40;s.isOutput=1;s.reserved=1|(1<<3)|(51<<6);wcscpy_s(s.name,L"Podglow — Preview");return true;
}
static bool ReadCompanionUx(AirPodsUxState& s){if(!g_preview)return ReadAirPodsUxState(s);s={};s.sequence=2;s.tick=GetTickCount64();s.leftSeen=s.rightSeen=s.caseSeen=AirPodsUnixMs();s.confirmedMode=2;s.command=wcsstr(GetCommandLineW(),L"--preview-busy-volume")?12:0;return true;}
static void CompanionCommand(int command,int value){if(g_preview){if(auto h=FindWindowW(L"AirPodsCompanionWidget",L"Podglow — Preview")){SetPropW(h,L"PodglowPreviewLastCommand",reinterpret_cast<HANDLE>(static_cast<INT_PTR>(command)));SetPropW(h,L"PodglowPreviewLastValue",reinterpret_cast<HANDLE>(static_cast<INT_PTR>(value+1)));}return;}if(auto h=FindWindowW(nullptr,L"Island AirPods Control"))PostMessageW(h,WM_APP+2,command,value);}
static std::wstring CompanionText(const std::wstring& s);
static std::wstring CompanionAssetFile(){return g_directory+L"\\assets\\AirPods-Pro-3.png";}
#define AIRPODS_TEXT(value) CompanionText(value)
#define AIRPODS_WINDOW_TITLE (g_preview?L"Podglow — Preview":L"Podglow")
#define AIRPODS_HEADER_TITLE L"AirPods"
#define AIRPODS_SEND_COMMAND(command,value) CompanionCommand(command,value)
#define AIRPODS_ASSET_FILE CompanionAssetFile()
#define ReadAirPodsIslandState ReadCompanionState
#define ReadAirPodsUxState ReadCompanionUx
#ifdef COMPANION_LOCAL_GLYPHS
#include "LocalGlyphs.h"
#else
#include "../Shared/PodglowGlyphs.h"
inline winrt::com_ptr<ID2D1PathGeometry> MakeAirPodsOriginalGlyph(ID2D1Factory* f,int id){return MakePodglowGlyph(f,id);}
#endif
#include "../Shared/AirPodsPanel.h"
#undef ReadAirPodsIslandState
#undef ReadAirPodsUxState

static std::wstring CompanionText(const std::wstring& s){
 const auto& help=CompanionEnglishHelp();if(auto item=help.find(s);item!=help.end())return item->second;
 static const std::map<std::wstring,std::wstring> words{
  {L"Verbunden",L"Connected"},{L"Nicht verbunden",L"Not connected"},{L"Hörmodus",L"Listening mode"},{L"Transparenz",L"Transparency"},{L"Adaptiv",L"Adaptive"},{L"Geräusch-\nunterdrückung",L"Noise\ncancellation"},{L"Aus",L"Off"},{L"Scrollen: lauter / leiser",L"Scroll to adjust"},{L"Lautstärke",L"Volume"},{L"AirPods-Einstellungen",L"AirPods settings"},
  {L"Links",L"Left"},{L"Rechts",L"Right"},{L"Case",L"Case"},{L"Im Ohr",L"In ear"},{L"Im Case",L"In case"},{L"Außerhalb",L"Out of ear"},{L"⚡ Lädt",L"\u26a1 Charging"},{L"Zuletzt: lädt",L"Last seen charging"},{L"Wird nicht geladen",L"Not charging"},{L"Gerade aktualisiert",L"Updated just now"},{L"Stand unbekannt",L"Last seen unknown"},{L"Noch kein Wert",L"No reading yet"},
  {L"Trennen",L"Disconnect"},{L"Verbinden",L"Connect"},{L"Erneut verbinden",L"Try connecting again"},{L"Bitte warten",L"Please wait…"},{L"Verbindung wird hergestellt …",L"Connecting…"},{L"Verbindung wird hergestellt",L"Connecting…"},{L"Wird getrennt",L"Disconnecting…"},{L"HÖREN",L"LISTENING"},{L"MIKROFON",L"MICROPHONE"},
  {L"Gespräche erkennen",L"Conversation awareness"},{L"Lautstärke automatisch anpassen",L"Personalized volume"},{L"Beim Herausnehmen pausieren",L"Pause when removed"},{L"Mikrofon ausschalten",L"Mute microphone"},{L"AirPods-Mikrofon automatisch nutzen",L"Switch microphone automatically"},{L"Weitere Optionen",L"More options"},{L"Mikrofonseite",L"Microphone side"},{L"Automatisch",L"Automatic"},
  {L"Drucksteuerung & Hinweistöne",L"Press controls & tones"},{L"Drucksteuerung & Töne",L"Press controls & tones"},{L"Die Drucksensoren sitzen an den AirPods-Stielen.",L"The pressure sensors are on the AirPods stems."},{L"Tempo für Doppeldruck",L"Double-press speed"},{L"Dauer für Gedrückthalten",L"Press-and-hold duration"},{L"Lautstärke der Hinweistöne",L"Tone volume"},{L"Standard",L"Default"},{L"Langsamer",L"Slower"},{L"Am langsamsten",L"Slowest"},{L"Kürzer",L"Shorter"},{L"Am kürzesten",L"Shortest"},{L"Hörmodus „Aus“ anbieten",L"Show the Off listening mode"},{L"Ton wird über die AirPods ausgegeben",L"AirPods are the audio output"},{L"Ton über die AirPods ausgeben",L"Use AirPods for audio output"},
  {L"Windows-Bluetooth-Einstellungen öffnen",L"Open Windows Bluetooth settings"},{L"Case öffnen und die AirPods einsetzen.\nFalls sie nicht reagieren: Case kurz schließen,\nwieder öffnen und Bluetooth am Mac prüfen.",L"Open the case and put on your AirPods.\nIf they do not respond, close and reopen the case\nand check the connection on your other devices."},{L"Klicken zum Schließen",L"Click to dismiss"},{L"AirPods-Anbindung startet",L"The AirPods engine is starting"},{L"Erweiterte Funktionen nach Windows-Neustart",L"The driver setup needs a Windows restart"},{L"AirPods-Einstellungen werden gelesen",L"Reading AirPods settings"},{L"AirPods antworten noch nicht",L"Waiting for the AirPods to respond"}
 };
 if(s==L"Verbunden"&&g_preview)return L"Connected · Preview";
 auto found=words.find(s);if(found!=words.end())return found->second;
 auto replace=[&](std::wstring v,PCWSTR a,PCWSTR b){size_t at=0;while((at=v.find(a,at))!=v.npos){v.replace(at,wcslen(a),b);at+=wcslen(b);}return v;};
 std::wstring v=s;v=replace(v,L"Zuletzt vor ",L"Last seen ");v=replace(v,L"Vor ",L"Updated ");v=replace(v,L" Sek.",L"s ago");v=replace(v,L" Min.",L"m ago");v=replace(v,L" Std.",L"h ago");return v;
}

int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){
 SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
 WCHAR file[32768]{};GetModuleFileNameW(nullptr,file,32768);g_directory=file;g_directory.resize(g_directory.find_last_of(L"\\/"));g_preview=wcsstr(GetCommandLineW(),L"--preview")!=nullptr;
 if(wcsstr(GetCommandLineW(),L"--register-startup")){
  HKEY key=nullptr;if(RegCreateKeyExW(HKEY_CURRENT_USER,L"Software\\Microsoft\\Windows\\CurrentVersion\\Run",0,nullptr,0,KEY_SET_VALUE,nullptr,&key,nullptr)!=ERROR_SUCCESS)return 1;
  std::wstring value=L"\""+g_directory+L"\\Podglow.exe\" --startup";
  auto result=RegSetValueExW(key,L"AirPodsCompanion",0,REG_SZ,reinterpret_cast<const BYTE*>(value.c_str()),static_cast<DWORD>((value.size()+1)*sizeof(wchar_t)));RegCloseKey(key);return result==ERROR_SUCCESS?0:1;
 }
 HANDLE mutex=CreateMutexW(nullptr,TRUE,g_preview?L"Local\\AirPodsCompanion.Native.Preview":L"Local\\AirPodsCompanion.Native");if(GetLastError()==ERROR_ALREADY_EXISTS){if(auto h=FindWindowW(L"AirPodsCompanionWidget",nullptr))PostMessageW(h,WM_APP+193,0,0);CloseHandle(mutex);return 0;}
 AirPodsPanel panel;if(!panel.Create()){MessageBoxW(nullptr,L"The widget could not create its Windows graphics surface.",L"Podglow",MB_ICONERROR);return 1;}
 if(!g_preview&&!wcsstr(GetCommandLineW(),L"--widget-only")&&!FindWindowW(nullptr,L"Island AirPods Control")){
  auto engine=g_directory+L"\\engine\\IslandAirPodsHost.exe";if(GetFileAttributesW(engine.c_str())==INVALID_FILE_ATTRIBUTES)engine=g_directory+L"\\IslandAirPodsHost.exe";if(GetFileAttributesW(engine.c_str())!=INVALID_FILE_ATTRIBUTES){
   std::wstring arguments=L"\""+engine+L"\" --startup";STARTUPINFOW startup{sizeof(startup)};startup.dwFlags=STARTF_USESHOWWINDOW;startup.wShowWindow=SW_HIDE;PROCESS_INFORMATION process{};
   if(CreateProcessW(engine.c_str(),arguments.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&startup,&process)){CloseHandle(process.hThread);CloseHandle(process.hProcess);}
  }
 }
 if(!wcsstr(GetCommandLineW(),L"--startup"))panel.Open();MSG msg{};while(GetMessageW(&msg,nullptr,0,0)>0){TranslateMessage(&msg);DispatchMessageW(&msg);}CloseHandle(mutex);return 0;
}
