#pragma once
// One presentation shared by the Island adapter and the standalone Windows adapter.
#ifndef AIRPODS_TEXT
#define AIRPODS_TEXT(value) (value)
#endif
#ifndef AIRPODS_WINDOW_TITLE
#define AIRPODS_WINDOW_TITLE L"Island AirPods"
#endif
#ifndef AIRPODS_HEADER_TITLE
#define AIRPODS_HEADER_TITLE L"AirPods Pro 3"
#endif
#ifndef AIRPODS_SEND_COMMAND
inline void DefaultAirPodsCommand(int command,int value){if(auto h=FindWindowW(nullptr,L"Island AirPods Control"))PostMessageW(h,WM_APP+2,command,value);}
#define AIRPODS_SEND_COMMAND(command,value) DefaultAirPodsCommand(command,value)
#endif
#ifndef AIRPODS_ASSET_FILE
inline std::wstring DefaultAirPodsAssetFile(){WCHAR dir[MAX_PATH]{};GetEnvironmentVariableW(L"LOCALAPPDATA",dir,MAX_PATH);return std::wstring(dir)+L"\\AirPodsIsland\\AirPods-Pro-3.png";}
#define AIRPODS_ASSET_FILE DefaultAirPodsAssetFile()
#endif
class AirPodsPanel : public Panel {
 winrt::com_ptr<IDWriteTextFormat> m_title,m_text,m_small,m_value,m_symbols;
 std::array<winrt::com_ptr<ID2D1PathGeometry>,7> m_originalGlyphs;ID2D1Factory* m_glyphFactory=nullptr;
 winrt::com_ptr<ID2D1Bitmap> m_product;ID2D1RenderTarget* m_productOwner=nullptr;ULONGLONG m_productRead=0;
 AirPodsIslandState m_state{};bool m_live=false,m_advanced=false,m_controls=false,m_more=false,m_uxLive=false,m_animations=true,m_motion=false;AirPodsUxState m_ux{};int m_help=-1,m_helpDraw=-1;float m_helpAlpha=0,m_pageAlpha=1,m_dt=.016f,m_toggleVisual[5]{-1,-1,-1,-1,-1},m_modeVisual[4]{};ULONGLONG m_pageAt=0,m_hoverAt=0,m_paintAt=0;int m_wheelVolume=-1,m_wheelTone=-1;ULONGLONG m_wheelVolumeUntil=0,m_wheelToneUntil=0;int m_hover=-1,m_pressed=-1,m_drag=-1,m_preview=-1;ULONGLONG m_lastSlider=0, m_read=0,m_legacyTick=0;std::wstring m_legacyTip;
 bool Ready()const{return m_live&&m_state.connected&&m_state.backend==2;}
 bool Cap(UINT bit)const{return Ready()&&(m_state.caps&bit)!=0;}
 void Refresh(){ReadAirPodsUxState(m_ux);m_uxLive=m_ux.tick>0&&m_ux.tick<=GetTickCount64()&&GetTickCount64()-m_ux.tick<4000;if(!m_uxLive)m_ux=AirPodsUxState{};ReadAirPodsIslandState(m_state);m_live=m_state.tick>0&&AirPodsIslandFresh(m_state,GetTickCount64());if(!m_live)m_state=AirPodsIslandState{};if(m_drag>=0&&(!m_live||!m_state.connected)){m_drag=-1;m_pressed=-1;m_preview=-1;if(GetCapture()==m_hwnd)ReleaseCapture();}m_read=GetTickCount64();if(m_wheelVolume>=0){if(!m_live||!m_state.connected||m_read>m_wheelVolumeUntil||m_state.volume==m_wheelVolume)m_wheelVolume=-1;else m_state.volume=m_wheelVolume;}if(m_wheelTone>=0){if(!Ready()||m_read>m_wheelToneUntil||int((m_state.reserved>>6)&255)-1==m_wheelTone)m_wheelTone=-1;else m_state.reserved=(m_state.reserved&~(255u<<6))|(UINT(m_wheelTone+1)<<6);}if(m_live&&m_state.backend!=2&&!m_legacyTip.empty()){auto parse=[&](std::initializer_list<PCWSTR> labels){for(auto label:labels){auto at=m_legacyTip.find(label);if(at!=m_legacyTip.npos){auto v=m_legacyTip.substr(at+wcslen(label));return std::clamp(_wtoi(v.c_str()),0,100);}}return -1;};m_state.left=parse({L"Links:",L"Left:"});m_state.right=parse({L"Rechts:",L"Right:"});m_state.caseBattery=parse({L"Case:",L"Ladecase:"});if(m_state.left>=0||m_state.right>=0||m_state.caseBattery>=0)m_state.batteryTick=m_legacyTick;}}
 int Height()const{return !m_advanced?454:m_controls?620:m_more?738:538;}
 bool Pending(int command)const{return m_uxLive&&m_ux.phase==1&&m_ux.command==command;}
 void Navigate(){m_help=m_helpDraw=-1;m_helpAlpha=0;for(auto& v:m_toggleVisual)v=-1;m_hover=-1;m_pageAt=GetTickCount64();Reformat();}
 int Hit(POINT p)const{float x=p.x/m_scale,y=p.y/m_scale;if(x<16||x>=374)return -1;if(y>=16&&y<=50){if(x>=342)return 0;if(x>=302)return 20;}if(GetTickCount64()-m_pageAt<280&&m_animations)return -1;if(m_help>=0)return 700;if(x<24||x>=366)return -1;
  if(!m_advanced){if(y>=392&&y<=430)return 20;if(y>=88&&y<=191)return 90+std::clamp(int((x-24)/114),0,2);if(!m_state.connected){if(y>=220&&y<=258&&m_live&&!m_state.busy&&!Pending(1))return 1;if(y>=341&&y<=377)return 22;return -1;}if(y>=199&&y<=221&&x>=334)return 70;if(y>=228&&y<=294&&Cap(1)&&!Pending(3)){int i=int((x-24)/(342.f/((m_state.reserved&(1<<14))?4:3)));if(i==0)return 10;if(i==1&&Cap(2))return 11;if(i==2)return 12;if(i==3)return 21;}if(y>=351&&y<=381&&m_live&&m_state.volume>=0)return 15;return -1;}
  if(m_controls){if(x>=332){if(y>=91&&y<=122)return 77;if(y>=225&&y<=256)return 78;if(y>=360&&y<=390)return 79;}if(y>=154&&y<=192&&Cap(256))return 40+int((x-24)/114);if(y>=288&&y<=326&&Cap(512))return 50+int((x-24)/114);if(y>=416&&y<=444&&Cap(1024))return 60;if(y>=480&&y<=544){if(x>=280&&x<310)return 80;if(Cap(64))return 61;}return -1;}
  if(y>=78&&y<=112&&x>=250&&m_live&&!m_state.busy&&!Pending(1)&&!Pending(2))return 1;
  if(y>=158&&y<=202){if(x>=280&&x<309)return 71;if(Cap(8)&&m_state.conversation>=0)return 13;}
  if(y>=205&&y<=249){if(x>=280&&x<309)return 72;if(Cap(32)&&m_state.personalized>=0)return 14;}
  if(y>=252&&y<=296){if(x>=280&&x<309)return 73;if(Cap(4)&&m_state.earDetection>=0)return 19;}
  if(y>=340&&y<=384){if(x>=280&&x<309)return 74;if(m_live&&m_state.micMuted>=0)return 16;}
  if(y>=387&&y<=431){if(x>=280&&x<309)return 75;if(m_live)return 17;}
  if(y>=458&&y<=496)return 64;
  if(m_more){if(y>=511&&y<=541&&x>=332)return 76;if(y>=548&&y<=586&&Cap(128))return 30+int((x-24)/114);if(y>=610&&y<=648)return 63;if(y>=660&&y<=698&&m_live&&m_state.connected)return 18;}return -1;}
 void Command(int command,int value=0){AIRPODS_SEND_COMMAND(command,value);}
 void Slider(POINT p,bool commit){if(m_drag==15){int value=int(std::round(std::clamp((p.x/m_scale-34)/322.f,0.f,1.f)*100));m_preview=value;if(commit||GetTickCount64()-m_lastSlider>60){Command(12,value);m_lastSlider=GetTickCount64();}}else if(m_drag==60){int value=int(std::round(15+std::clamp((p.x/m_scale-34)/322.f,0.f,1.f)*85));m_preview=value;if(commit)Command(10,value);}}
 void LoadProduct(ID2D1RenderTarget* target){if(m_productOwner==target&&(m_product||GetTickCount64()-m_productRead<5000))return;m_productRead=GetTickCount64();m_product=nullptr;m_productOwner=target;std::wstring file=AIRPODS_ASSET_FILE;winrt::com_ptr<IWICImagingFactory>wic;winrt::com_ptr<IWICBitmapDecoder>d;winrt::com_ptr<IWICBitmapFrameDecode>f;winrt::com_ptr<IWICFormatConverter>c;if(SUCCEEDED(CoCreateInstance(CLSID_WICImagingFactory,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(wic.put())))&&SUCCEEDED(wic->CreateDecoderFromFilename(file.c_str(),nullptr,GENERIC_READ,WICDecodeMetadataCacheOnLoad,d.put()))&&SUCCEEDED(d->GetFrame(0,f.put()))&&SUCCEEDED(wic->CreateFormatConverter(c.put()))&&SUCCEEDED(c->Initialize(f.get(),GUID_WICPixelFormat32bppPBGRA,WICBitmapDitherTypeNone,nullptr,0,WICBitmapPaletteTypeCustom))){auto props=D2D1::BitmapProperties(D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED),96,96);target->CreateBitmapFromWicBitmap(c.get(),&props,m_product.put());}}
 protected:
 void OnSurfaceReset(){m_product=nullptr;m_productOwner=nullptr;m_productRead=0;}
 void Prepare(float s)override{m_size={LONG(390*s),LONG(Height()*s)};m_title=MakeFormat(L"Segoe UI Variable Text",DWRITE_FONT_WEIGHT_SEMI_BOLD,18*s,DWRITE_TEXT_ALIGNMENT_LEADING);m_text=MakeFormat(L"Segoe UI Variable Text",DWRITE_FONT_WEIGHT_NORMAL,12*s,DWRITE_TEXT_ALIGNMENT_LEADING);m_small=MakeFormat(L"Segoe UI Variable Text",DWRITE_FONT_WEIGHT_NORMAL,10*s,DWRITE_TEXT_ALIGNMENT_LEADING);m_value=MakeFormat(L"Segoe UI Variable Display",DWRITE_FONT_WEIGHT_SEMI_BOLD,20*s,DWRITE_TEXT_ALIGNMENT_LEADING);m_symbols=MakeFormat(L"Segoe Fluent Icons",DWRITE_FONT_WEIGHT_NORMAL,17*s,DWRITE_TEXT_ALIGNMENT_CENTER);}
 void OnCaptureLost()override{m_drag=-1;m_pressed=-1;m_preview=-1;}
 void OnClose()override{m_wheelVolume=m_wheelTone=-1;m_drag=-1;m_pressed=-1;m_preview=-1;m_hover=-1;}
 void OnOpen()override{SetWindowTextW(m_hwnd,AIRPODS_WINDOW_TITLE);SetPropW(m_hwnd,L"IslandAirPodsPanel",reinterpret_cast<HANDLE>(1));m_advanced=false;m_controls=false;m_more=false;m_help=m_helpDraw=-1;m_helpAlpha=0;m_pageAt=GetTickCount64();m_paintAt=0;BOOL animation=TRUE;SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION,0,&animation,0);m_animations=animation!=FALSE;for(auto& v:m_toggleVisual)v=-1;m_wheelVolume=m_wheelTone=-1;m_drag=-1;m_hover=-1;Prepare(m_scale);Refresh();}
 bool AnimateContents(double)override{m_idleMotion=true;bool redraw=m_motion||Pending(1)||Pending(2)||Pending(3)||GetTickCount64()-m_pageAt<320||(m_hover>=10&&m_hover<=92&&GetTickCount64()-m_hoverAt<650);if(GetTickCount64()-m_read>180){Refresh();redraw=true;}return redraw;}
 bool OnMouseMove(POINT p)override{if(m_drag>=0){Slider(p,false);return true;}int h=Hit(p);if(h==m_hover)return false;m_hoverAt=GetTickCount64();m_hover=h;return true;}
 bool OnMouseLeave()override{m_hover=-1;m_hoverAt=0;return true;}
 // Use delivered WM_MOUSEWHEEL deltas. Windows/device natural-scroll inversion is already applied; do not invert again.
 bool OnWheel(int delta)override{if(!delta)return false;Refresh();if(m_live&&m_state.connected&&m_state.volume>=0){m_wheelVolume=std::clamp(m_state.volume+(delta>0?2:-2),0,100);m_wheelVolumeUntil=GetTickCount64()+900;m_state.volume=m_wheelVolume;Command(12,m_wheelVolume);return true;}return false;}
 bool OnMouseDown(POINT p)override{Refresh();m_pressed=Hit(p);if(m_pressed==15||m_pressed==60){m_drag=m_pressed;m_preview=m_drag==15?m_state.volume:int((m_state.reserved>>6)&255)-1;SetCapture(m_hwnd);}return m_pressed>=0;}
 bool OnMouseUp(POINT p)override{if(m_drag>=0){Slider(p,true);if(m_drag==15){m_wheelVolume=m_preview;m_wheelVolumeUntil=GetTickCount64()+900;m_state.volume=m_preview;}m_drag=-1;m_pressed=-1;ReleaseCapture();return true;}Refresh();int h=Hit(p),pressed=m_pressed;m_pressed=-1;if(h<0||h!=pressed)return false;if(!h){Panel::Close();return true;}if(h==700){m_help=-1;return true;}if((h>=70&&h<=80)||(h>=90&&h<=92)){m_help=m_helpDraw=h;return true;}if(h==20||h==62){if(m_controls)m_controls=false;else m_advanced=!m_advanced;Navigate();return true;}if(h==63){m_controls=true;Navigate();return true;}if(h==64){m_more=!m_more;Navigate();return true;}if(h==22){ShellExecuteW(nullptr,L"open",L"ms-settings:bluetooth",nullptr,nullptr,SW_SHOWNORMAL);return true;}if(h==1)Command(m_state.connected?2:1);else if(h==10)Command(3,3);else if(h==11)Command(3,4);else if(h==12)Command(3,2);else if(h==13)Command(4,!m_state.conversation);else if(h==14)Command(5,!m_state.personalized);else if(h==16)Command(13,!m_state.micMuted);else if(h==17)Command(15,!m_state.autoMic);else if(h==18)Command(14);else if(h==19)Command(6,!m_state.earDetection);else if(h==21)Command(3,1);else if(h>=30&&h<=32){const int sides[]{0,2,1};Command(7,sides[h-30]);}else if(h>=40&&h<=42)Command(8,h-40);else if(h>=50&&h<=52)Command(9,h-50);else if(h==61)Command(11,!(m_state.reserved&(1<<14)));return true;}
 void Draw(ID2D1RenderTarget* target,ID2D1SolidColorBrush* brush)override{
  const float s=m_scale;const ULONGLONG now=GetTickCount64();const uint64_t utc=AirPodsUnixMs();m_dt=m_paintAt?std::clamp(float(now-m_paintAt)/1000.f,.001f,.05f):.016f;m_paintAt=now;m_motion=false;
  float pageT=m_animations?std::clamp(float(now-m_pageAt)/220.f,0.f,1.f):1.f;m_pageAlpha=.55f+.45f*pageT*pageT*(3-2*pageT);
  auto ease=[&](float& v,float wanted){if(!m_animations)v=wanted;else v+=(wanted-v)*(1-std::exp(-m_dt/.055f));if(std::abs(v-wanted)>.008f)m_motion=true;else v=wanted;return v;};
  ease(m_helpAlpha,m_help>=0?1.f:0.f);
  auto prop=[&](PCWSTR n,LONG_PTR v){SetPropW(m_hwnd,n,reinterpret_cast<HANDLE>(v));};prop(L"AirPodsScale1000",std::lround(s*1000));prop(L"AirPodsOffsetX",m_contentOffset.x);prop(L"AirPodsOffsetY",m_contentOffset.y);prop(L"AirPodsPage",m_advanced?(m_controls?2:1):0);prop(L"AirPodsContentHeight",Height());prop(L"AirPodsMoreExpanded",m_more);prop(L"AirPodsHelpId",m_help+1);prop(L"AirPodsActionPhase",m_uxLive?m_ux.phase:0);prop(L"AirPodsActionRequest",m_uxLive?m_ux.requestId:0);
  auto white=Fg(.96f),muted=Fg(.55f),disabled=Fg(.28f);D2D1_COLOR_F blue=D2D1::ColorF(0x0a84ff),green=D2D1::ColorF(0x62d99c);bool connected=m_live&&m_state.connected;
  auto faded=[&](D2D1_COLOR_F c){c.a*=m_pageAlpha;return c;};
  auto text=[&](const std::wstring& v,IDWriteTextFormat*f,float x,float y,float w,float h,D2D1_COLOR_F c){brush->SetColor(faded(c));auto translated=AIRPODS_TEXT(v);target->DrawTextW(translated.c_str(),UINT32(translated.size()),f,{x*s,y*s,(x+w)*s,(y+h)*s},brush,D2D1_DRAW_TEXT_OPTIONS_CLIP);};
  auto card=[&](float x,float y,float w,float h,D2D1_COLOR_F c,float r=10){brush->SetColor(faded(c));target->FillRoundedRectangle({{x*s,y*s,(x+w)*s,(y+h)*s},r*s,r*s},brush);};
  auto line=[&](float x,float y,float xx,float yy,D2D1_COLOR_F c,float w=1){brush->SetColor(faded(c));target->DrawLine({x*s,y*s},{xx*s,yy*s},brush,w*s);};
  auto info=[&](float x,float y,int id){auto c=m_hover==id?blue:muted;brush->SetColor(faded(c));target->DrawEllipse({{x*s,y*s},7*s,7*s},brush,1.1f*s);target->FillEllipse({{x*s,(y-3.5f)*s},.9f*s,.9f*s},brush);line(x,y-1,x,y+4,c,1.3f);};
  auto toggle=[&](int key,float y,int value,bool enabled){float wanted=value>0?1.f:0.f;if(m_toggleVisual[key]<0)m_toggleVisual[key]=wanted;float v=ease(m_toggleVisual[key],wanted);card(318,y,42,24,Fg(enabled?.17f:.08f),12);if(enabled){auto c=blue;c.a*=v;card(318,y,42,24,c,12);}brush->SetColor(faded(enabled?D2D1::ColorF(0xffffff):Fg(.25f)));target->FillEllipse({{(330+18*v)*s,(y+12)*s},8*s,8*s},brush);};
  auto segments=[&](float y,const wchar_t*const* labels,int selected,bool enabled){card(24,y,342,38,Fg(.055f));for(int i=0;i<3;i++){if(i==selected)card(26+i*114,y+2,110,34,Fg(enabled?.15f:.06f),8);m_small->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);text(labels[i],m_small.get(),26+i*114,y+2,110,34,enabled?(i==selected?white:muted):disabled);m_small->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);}};
  auto slider=[&](float y,int value,int low,bool enabled){line(34,y,356,y,Fg(.17f),4);if(value<low)return;float x=34+322*std::clamp((value-low)/float(100-low),0.f,1.f);line(34,y,x,y,enabled?blue:disabled,4);brush->SetColor(faded(enabled?white:disabled));target->FillEllipse({{x*s,y*s},5*s,5*s},brush);};
  auto originalGlyph=[&](int id,float x,float y,float w,float h,D2D1_COLOR_F c){
   if(id<0||id>=7)return;winrt::com_ptr<ID2D1Factory> factory;target->GetFactory(factory.put());
   if(m_glyphFactory!=factory.get()){for(auto& g:m_originalGlyphs)g=nullptr;m_glyphFactory=factory.get();}
   auto& path=m_originalGlyphs[id];if(!path)path=MakeAirPodsOriginalGlyph(factory.get(),id);if(!path)return;
   D2D1_RECT_F bounds{};if(FAILED(path->GetBounds(nullptr,&bounds)))return;
   float bw=bounds.right-bounds.left,bh=bounds.bottom-bounds.top;if(bw<=0||bh<=0)return;
   float k=std::min(w*s/bw,h*s/bh);D2D1_MATRIX_3X2_F before;target->GetTransform(&before);
   auto transform=D2D1::Matrix3x2F::Scale(k,k)*D2D1::Matrix3x2F::Translation(x*s+(w*s-bw*k)*.5f-bounds.left*k,y*s+(h*s-bh*k)*.5f-bounds.top*k)*before;
   target->SetTransform(transform);brush->SetColor(faded(c));target->FillGeometry(path.get(),brush);target->SetTransform(before);
  };
  auto component=[&](int id,float x,float y,D2D1_COLOR_F c){originalGlyph(id,x,y,id==2?19.f:17.f,20,c);};
  auto modeGlyph=[&](int id,float cx,float cy,D2D1_COLOR_F c){originalGlyph(id+3,cx-12,cy-12,24,24,c);};
  int displayed=m_uxLive?m_ux.confirmedMode:m_state.mode;prop(L"AirPodsDisplayedMode",displayed+1);prop(L"AirPodsModePending",Pending(3));prop(L"AirPodsControlsReady",Ready());prop(L"AirPodsVolumeShown",m_drag==15?m_preview:m_state.volume);prop(L"AirPodsContentAlpha1000",std::lround(m_pageAlpha*1000));
  text(m_advanced?L"\uE72B":L"\uE713",m_symbols.get(),302,17,34,32,m_hover==20?blue:muted);text(L"\u00d7",m_title.get(),350,17,25,32,muted);
  if(!m_advanced){
   LoadProduct(target);if(m_product)target->DrawBitmap(m_product.get(),{24*s,23*s,108*s,65*s},(connected?1.f:.45f)*m_pageAlpha,D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
   text(AIRPODS_HEADER_TITLE,m_title.get(),m_product?121.f:24.f,20,m_product?178.f:274.f,28,white);text(Pending(1)?L"Verbindung wird hergestellt":Pending(2)?L"Wird getrennt":connected?L"Verbunden":L"Nicht verbunden",m_small.get(),m_product?122.f:24.f,47,m_product?174.f:274.f,20,connected?blue:muted);
   card(24,88,342,103,Fg(.035f));const wchar_t* names[]{L"Links",L"Rechts",L"Case"};int values[]{m_state.left,m_state.right,m_state.caseBattery};uint64_t seen[]{m_ux.leftSeen,m_ux.rightSeen,m_ux.caseSeen};
   for(int i=0;i<3;i++){float x=36+i*114.f;if(i)line(x-12,103,x-12,177,Fg(.075f));bool charging=(m_state.charging&(1<<i))!=0,cached=m_uxLive&&(m_ux.flags&(1<<i));component(i,x,98,charging?green:muted);text(names[i],m_small.get(),x+25,97,71,20,muted);int v=values[i];auto c=v<0?muted:v<=10?D2D1::ColorF(0xff453a):v<=20?D2D1::ColorF(0xffd60a):white;text(v<0?L"\u2014":std::to_wstring(v)+L"%",m_value.get(),x,120,95,29,c);
    std::wstring status;if(charging)status=cached?L"Zuletzt: l\u00e4dt":L"\u26a1 L\u00e4dt";else if(i<2){int ear=i?m_state.earRight:m_state.earLeft;status=ear==0?L"Im Ohr":ear==2?L"Im Case":ear==1?L"Au\u00dferhalb":L"";}else if(v>=0)status=L"Wird nicht geladen";text(status,m_small.get(),x,147,98,19,charging&&!cached?green:muted);
    text(v<0?L"Noch kein Wert":AirPodsAgeLabel(m_uxLive?seen[i]:0,utc,cached),m_small.get(),x,169,99,18,muted);
   }
   if(connected){
    text(L"H\u00f6rmodus",m_small.get(),24,201,310,20,muted);info(352,209,70);int count=(m_state.reserved&(1<<14))?4:3;float width=342.f/count;int selected=displayed==3?0:displayed==4?1:displayed==2?2:displayed==1?3:-1;const wchar_t* labels[]{L"Transparenz",L"Adaptiv",L"Ger\u00e4usch-\nunterdr\u00fcckung",L"Aus"};
    for(int i=0;i<count;i++){float x=24+i*width,cx=x+width/2;bool enabled=Cap(1)&&(i!=1||Cap(2));int hit=i==0?10:i==1?11:i==2?12:21;float selectedAlpha=ease(m_modeVisual[i],i==selected?1.f:0.f);if(selectedAlpha>.01f)card(x+1,228,width-2,66,Fg(.14f*selectedAlpha));if(m_hover==hit&&enabled&&!Pending(3))card(x+1,228,width-2,66,Fg(.05f));auto c=enabled?(i==selected?blue:muted):disabled;modeGlyph(i,cx,248,c);
     m_small->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);text(labels[i],m_small.get(),x+1,262,width-2,29,enabled?white:disabled);m_small->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
    }
    std::wstring feedback;
    if(Pending(3))feedback=std::wstring(AirPodsModeName(m_ux.requestedMode))+L" wird aktiviert \u2026";
    else if(m_uxLive&&m_ux.command==3&&m_ux.phase==2&&now>=m_ux.actionTick&&now-m_ux.actionTick<2600)feedback=std::wstring(L"\u2713 ")+AirPodsModeName(displayed)+L" aktiv";
    else if(m_hover>=10&&m_hover<=12&&now-m_hoverAt>450)feedback=AirPodsModeHint(m_hover==10?3:m_hover==11?4:2);
    text(feedback,m_small.get(),24,302,342,21,Pending(3)?blue:muted);
    text(L"Lautst\u00e4rke",m_small.get(),24,331,95,22,muted);text(L"Scrollen: lauter / leiser",m_small.get(),123,331,193,22,Fg(.38f));text(m_state.volume>=0?std::to_wstring(m_drag==15?m_preview:m_state.volume)+L"%":L"\u2014",m_small.get(),324,331,42,22,muted);slider(367,m_drag==15?m_preview:m_state.volume,0,m_live&&m_state.volume>=0);
   }else{
    bool failed=m_uxLive&&m_ux.command==1&&m_ux.phase==3;bool waiting=Pending(1)||m_state.busy;card(24,220,342,38,m_live&&!waiting?blue:Fg(.08f));m_text->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);text(waiting?L"Verbindung wird hergestellt \u2026":failed?L"Erneut verbinden":L"Verbinden",m_text.get(),24,226,342,26,white);m_text->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
    text(L"Case \u00f6ffnen und die AirPods einsetzen.\nFalls sie nicht reagieren: Case kurz schlie\u00dfen,\nwieder \u00f6ffnen und Bluetooth am Mac pr\u00fcfen.",m_small.get(),24,272,342,65,muted);card(24,341,342,36,Fg(m_hover==22?.1f:.04f));text(L"Windows-Bluetooth-Einstellungen \u00f6ffnen",m_small.get(),36,347,320,25,blue);
   }
   card(24,392,342,38,Fg(m_hover==20?.10f:.04f));text(L"AirPods-Einstellungen",m_text.get(),36,399,298,25,blue);text(L"\u203a",m_text.get(),343,399,20,25,muted);
  }else if(!m_controls){
   text(L"AirPods-Einstellungen",m_title.get(),24,21,275,30,white);text(m_state.name[0]?m_state.name:L"AirPods Pro 3",m_small.get(),24,53,342,21,muted);
   text(connected?L"Verbunden":L"Nicht verbunden",m_text.get(),24,83,210,28,connected?blue:muted);card(250,78,116,34,D2D1::ColorF(m_hover==1?0x126bb5:0x14568b,.84f));m_text->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);text(Pending(1)||Pending(2)||m_state.busy?L"Bitte warten":connected?L"Trennen":L"Verbinden",m_text.get(),250,82,116,27,m_live&&!m_state.busy?white:muted);m_text->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
   text(L"H\u00d6REN",m_small.get(),24,129,342,22,muted);card(16,155,358,145,Fg(.035f));
   text(L"Gespr\u00e4che erkennen",m_text.get(),30,171,243,25,Cap(8)?white:muted);info(294,179,71);toggle(0,167,m_state.conversation,Cap(8)&&m_state.conversation>=0);
   text(L"Lautst\u00e4rke automatisch anpassen",m_text.get(),30,218,243,25,Cap(32)?white:muted);info(294,226,72);toggle(1,214,m_state.personalized,Cap(32)&&m_state.personalized>=0);
   text(L"Beim Herausnehmen pausieren",m_text.get(),30,265,243,25,Cap(4)?white:muted);info(294,273,73);toggle(2,261,m_state.earDetection,Cap(4)&&m_state.earDetection>=0);
   text(L"MIKROFON",m_small.get(),24,315,342,22,muted);card(16,338,358,97,Fg(.035f));
   text(L"Mikrofon ausschalten",m_text.get(),30,353,243,25,white);info(294,361,74);toggle(3,349,m_state.micMuted,m_live&&m_state.micMuted>=0);
   text(L"AirPods-Mikrofon automatisch nutzen",m_text.get(),30,400,243,25,white);info(294,408,75);toggle(4,396,m_state.autoMic,m_live);
   card(24,458,342,38,Fg(m_hover==64?.10f:.04f));text(L"Weitere Optionen",m_text.get(),36,465,280,25,white);text(m_more?L"\u2304":L"\u203a",m_text.get(),343,464,20,25,muted);
   if(m_more){text(L"Mikrofonseite",m_text.get(),24,514,305,25,white);info(350,522,76);const wchar_t* sides[]{L"Automatisch",L"Links",L"Rechts"};segments(548,sides,m_state.micSide==2?1:m_state.micSide==1?2:m_state.micSide==0?0:-1,Cap(128));
    card(24,610,342,38,Fg(m_hover==63?.10f:.04f));text(L"Drucksteuerung & Hinweist\u00f6ne",m_text.get(),36,617,298,25,blue);text(L"\u203a",m_text.get(),343,617,20,25,muted);
    card(24,660,342,38,Fg(m_hover==18?.10f:.04f));text(m_state.isOutput==1?L"Ton wird \u00fcber die AirPods ausgegeben":L"Ton \u00fcber die AirPods ausgeben",m_text.get(),36,667,322,25,connected?blue:muted);
   }
  }else{
   text(L"Drucksteuerung & T\u00f6ne",m_title.get(),24,21,275,30,white);text(L"Die Drucksensoren sitzen an den AirPods-Stielen.",m_small.get(),24,53,342,23,muted);
   text(L"Tempo f\u00fcr Doppeldruck",m_text.get(),24,96,300,25,white);info(350,104,77);const wchar_t* speeds[]{L"Standard",L"Langsamer",L"Am langsamsten"};segments(154,speeds,int(m_state.reserved&7)-1,Cap(256));
   text(L"Dauer f\u00fcr Gedr\u00fcckthalten",m_text.get(),24,230,300,25,white);info(350,238,78);const wchar_t* durations[]{L"Standard",L"K\u00fcrzer",L"Am k\u00fcrzesten"};segments(288,durations,int((m_state.reserved>>3)&7)-1,Cap(512));
   text(L"Lautst\u00e4rke der Hinweist\u00f6ne",m_text.get(),24,364,270,25,white);info(350,375,79);int volume=m_drag==60?m_preview:int((m_state.reserved>>6)&255)-1;text(volume>=15?std::to_wstring(volume)+L"%":L"\u2014",m_small.get(),305,393,61,22,muted);slider(430,volume,15,Cap(1024));
   card(16,476,358,76,Fg(.035f));text(L"H\u00f6rmodus \u201eAus\u201c anbieten",m_text.get(),30,496,243,25,white);info(294,504,80);toggle(0,492,(m_state.reserved>>14)&1,Cap(64));
  }
  std::wstring footer=m_state.error?m_state.message:!m_live?L"AirPods-Anbindung startet":m_state.backend==3?L"Erweiterte Funktionen nach Windows-Neustart":m_state.backend==1?L"AirPods-Einstellungen werden gelesen":m_state.backend==4?L"AirPods antworten noch nicht":L"";if(!footer.empty())text(footer,m_small.get(),24,Height()-23,342,20,m_state.error?D2D1::ColorF(0xffcc48):muted);
  if(m_helpDraw>=0&&m_helpAlpha>.01f){std::wstring title,body;int id=m_helpDraw;
   switch(id){case 70:title=L"Wie m\u00f6chtest du h\u00f6ren?";body=L"Transparenz: Umgebung weiterhin h\u00f6ren.\nAdaptiv: automatisch an die Umgebung anpassen.\nGer\u00e4uschunterdr\u00fcckung: St\u00f6rger\u00e4usche ausblenden.";break;
    case 71:title=L"Gespr\u00e4che erkennen";body=L"Wenn du sprichst, wird die Musik leiser.\nNach dem Gespr\u00e4ch kehrt sie zur\nvorherigen Lautst\u00e4rke zur\u00fcck.";break;
    case 72:title=L"Automatische Lautst\u00e4rke";body=L"Die AirPods passen die Lautst\u00e4rke an deine\nUmgebung und deine H\u00f6rgewohnheiten an.\nDu kannst den Regler weiterhin selbst bedienen.";break;
    case 73:title=L"Beim Herausnehmen pausieren";body=L"Musik pausiert, wenn du einen H\u00f6rer\nherausnimmst. Die AirPods verwenden daf\u00fcr\nihre In-Ohr-Erkennung.";break;
    case 74:title=L"Mikrofon ausschalten";body=L"Schaltet das AirPods-Mikrofon in Windows stumm.\nMusik und andere T\u00f6ne h\u00f6rst du weiterhin.\nZum Sprechen den Schalter wieder ausschalten.";break;
    case 75:title=L"AirPods-Mikrofon automatisch nutzen";body=L"Beim Verbinden w\u00e4hlt Windows das\nAirPods-Mikrofon. Beim Trennen wird das\nvorherige verf\u00fcgbare Mikrofon wieder genutzt.";break;
    case 76:title=L"Welcher H\u00f6rer nimmt dich auf?";body=L"Automatisch w\u00e4hlt den passenden H\u00f6rer.\nLinks oder Rechts legt eine Seite fest.\nEmpfohlen: Automatisch.";break;
    case 77:title=L"Tempo f\u00fcr Doppeldruck";body=L"Legt fest, wie viel Zeit zwischen zwei\nDruckbewegungen am Stiel liegen darf.\nLangsamer erleichtert den Doppeldruck.";break;
    case 78:title=L"Dauer f\u00fcr Gedr\u00fcckthalten";body=L"Legt fest, wie lange du den Stiel gedr\u00fcckt\nhalten musst, bis die hinterlegte Funktion\nausgel\u00f6st wird. K\u00fcrzer reagiert fr\u00fcher.";break;
    case 79:title=L"Lautst\u00e4rke der Hinweist\u00f6ne";body=L"Betrifft die Signalt\u00f6ne der AirPods, etwa\nbeim Verbinden und beim H\u00f6rmoduswechsel.\nDie Musiklautst\u00e4rke bleibt unver\u00e4ndert.";break;
    case 80:title=L"H\u00f6rmodus \u201eAus\u201c anbieten";body=L"Erg\u00e4nzt in der Hauptansicht eine vierte Taste.\nDamit lassen sich die aktiven H\u00f6rmodi\nausschalten, ohne die AirPods zu trennen.";break;
    default:{int index=id-90;int values[]{m_state.left,m_state.right,m_state.caseBattery};uint64_t times[]{m_ux.leftSeen,m_ux.rightSeen,m_ux.caseSeen};if(index>=0&&index<3){title=index==0?L"Linker AirPod":index==1?L"Rechter AirPod":L"Ladecase";body=values[index]<0?L"Noch kein Akkustand empfangen.":std::to_wstring(values[index])+L" %\n"+AirPodsAgeLabel(m_uxLive?times[index]:0,utc,(m_ux.flags&(1<<index))!=0);if(index==2)body+=L"\nBeim Aufklappen wird ein neuer Case-Wert\n\u00fcbernommen, sobald das Case ihn sendet.";}}break;
   }
   float y=!m_advanced?218.f:m_controls?345.f:m_more?515.f:337.f;if(id>=77&&id<=78)y=270;float h=151;auto bg=BgSolid(.97f);bg.a*=m_helpAlpha;card(24,y,342,h,bg,12);auto border=Fg(.15f*m_helpAlpha);brush->SetColor(faded(border));target->DrawRoundedRectangle({{24*s,y*s,366*s,(y+h)*s},12*s,12*s},brush,1*s);auto fg=white,sub=muted;fg.a*=m_helpAlpha;sub.a*=m_helpAlpha;text(title,m_text.get(),38,y+13,313,29,fg);text(body,m_small.get(),38,y+45,314,77,sub);text(L"Klicken zum Schlie\u00dfen",m_small.get(),38,y+124,314,20,sub);
  }
 }
 public:
 void SetLegacyTip(const std::wstring& tip){m_legacyTip=tip;m_legacyTick=GetTickCount64();}
};