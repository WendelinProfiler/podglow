#pragma once
#include "../Shared/PodglowGlyphs.h"
// Rasterize our own vectors at icon resolution, retaining alpha for either
// taskbar theme. No font, product image or copied system asset is required.
inline HICON PodglowTrayGlyph(ID2D1Factory* factory,bool connected){
 constexpr int size=64;BITMAPINFO info{};info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=size;info.bmiHeader.biHeight=-size;info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;
 void* pixels=nullptr;HDC dc=CreateCompatibleDC(nullptr);HBITMAP bitmap=CreateDIBSection(dc,&info,DIB_RGB_COLORS,&pixels,nullptr,0);if(!dc||!bitmap){if(bitmap)DeleteObject(bitmap);if(dc)DeleteDC(dc);return nullptr;}
 auto old=SelectObject(dc,bitmap);winrt::com_ptr<ID2D1DCRenderTarget> target;winrt::com_ptr<ID2D1SolidColorBrush> brush;HICON icon=nullptr;RECT rect{0,0,size,size};
 auto props=D2D1::RenderTargetProperties(D2D1_RENDER_TARGET_TYPE_SOFTWARE,D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED),96,96);
 if(SUCCEEDED(factory->CreateDCRenderTarget(&props,target.put()))&&SUCCEEDED(target->BindDC(dc,&rect))&&SUCCEEDED(target->CreateSolidColorBrush(D2D1::ColorF(connected?0x0a84ff:0x919198),brush.put()))){
  target->BeginDraw();target->Clear(D2D1::ColorF(0,0));
  for(int side=0;side<2;side++){auto shape=MakePodglowGlyph(factory,side);D2D1_RECT_F b{};if(shape&&SUCCEEDED(shape->GetBounds(nullptr,&b))){float k=std::min(27.f/(b.right-b.left),53.f/(b.bottom-b.top));target->SetTransform(D2D1::Matrix3x2F(k,0,0,k,3+side*31+(27-(b.right-b.left)*k)/2-b.left*k,5+(53-(b.bottom-b.top)*k)/2-b.top*k));target->FillGeometry(shape.get(),brush.get());}}
  if(SUCCEEDED(target->EndDraw())){HBITMAP mask=CreateBitmap(size,size,1,1,nullptr);ICONINFO ii{};ii.fIcon=TRUE;ii.hbmColor=bitmap;ii.hbmMask=mask;if(mask){icon=CreateIconIndirect(&ii);DeleteObject(mask);}}
 }
 SelectObject(dc,old);DeleteObject(bitmap);DeleteDC(dc);return icon;
}
