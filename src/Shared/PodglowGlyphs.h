#pragma once
// Podglow's own device and listening-mode artwork. Coordinates are authored
// here, not exported from a font or traced from Apple symbol paths.
inline winrt::com_ptr<ID2D1PathGeometry> MakePodglowGlyph(ID2D1Factory* factory,int id){
 winrt::com_ptr<ID2D1PathGeometry> path;winrt::com_ptr<ID2D1GeometrySink> s;
 if(!factory||id<0||id>6||FAILED(factory->CreatePathGeometry(path.put()))||FAILED(path->Open(s.put())))return nullptr;
 s->SetFillMode(D2D1_FILL_MODE_ALTERNATE);
 auto begin=[&](float x,float y){s->BeginFigure({x,y},D2D1_FIGURE_BEGIN_FILLED);};
 auto line=[&](float x,float y){s->AddLine({x,y});};
 auto curve=[&](float a,float b,float c,float d,float e,float f){s->AddBezier({{a,b},{c,d},{e,f}});};
 auto end=[&](){s->EndFigure(D2D1_FIGURE_END_CLOSED);};
 auto oval=[&](float x,float y,float rx,float ry){float k=.55228475f;begin(x+rx,y);curve(x+rx,y+k*ry,x+k*rx,y+ry,x,y+ry);curve(x-k*rx,y+ry,x-rx,y+k*ry,x-rx,y);curve(x-rx,y-k*ry,x-k*rx,y-ry,x,y-ry);curve(x+k*rx,y-ry,x+rx,y-k*ry,x+rx,y);end();};
 auto box=[&](float x,float y,float w,float h,float r){begin(x+r,y);line(x+w-r,y);curve(x+w,y,x+w,y,x+w,y+r);line(x+w,y+h-r);curve(x+w,y+h,x+w,y+h,x+w-r,y+h);line(x+r,y+h);curve(x,y+h,x,y+h,x,y+h-r);line(x,y+r);curve(x,y,x,y,x+r,y);end();};
 if(id<2){
  // A slanted acoustic housing, short tapered stem and inset sound outlet.
  // Left and right share proportions but face in opposite directions.
  auto p=[&](float x,float y){return D2D1::Point2F(id?32-x:x,y);};
  s->BeginFigure(p(18,3),D2D1_FIGURE_BEGIN_FILLED);
  auto c=[&](float a,float b,float c,float d,float e,float f){s->AddBezier({p(a,b),p(c,d),p(e,f)});};
  c(25,1,30,6,29,12);c(29,17,26,21,22,23);
  s->AddLine(p(21,40));c(21,44,15,45,14,41);s->AddLine(p(13,23));
  c(8,23,3,20,2,16);c(0,11,4,7,9,7);c(12,7,14,4,18,3);end();
  oval(id?12:20,11,4.3f,5.1f);box(id?13:16,30,3,9,1.4f);
 }else if(id==2){
  box(1,4,46,33,11);box(4,7,40,27,8);
  box(3,15,42,2.4f,1);oval(24,26,1.6f,1.6f);
 }else if(id==3){
  // Open listening: ear contour with three separate outward sound rays.
  begin(18,3);curve(4,1,1,17,8,24);curve(12,28,10,36,17,37);line(20,34);curve(15,34,17,26,12,22);curve(5,16,9,6,17,7);curve(25,7,27,17,20,20);line(22,24);curve(34,19,31,4,18,3);end();
  box(33,9,9,2.6f,1.3f);box(35,19,10,2.6f,1.3f);box(33,29,9,2.6f,1.3f);
 }else if(id==4){
  // Adaptive listening: three level bars framed by opposing arcs.
  box(17,11,3.5f,20,1.7f);box(24,5,3.5f,32,1.7f);box(31,15,3.5f,12,1.7f);
  begin(12,5);curve(0,11,0,31,12,37);line(14,33);curve(5,28,5,14,14,9);end();
  begin(39,5);curve(51,11,51,31,39,37);line(37,33);curve(46,28,46,14,37,9);end();
 }else if(id==5){
  // Noise cancellation: closed acoustic cups and an uninterrupted arch.
  begin(3,25);line(3,19);curve(3,-3,37,-3,37,19);line(37,25);line(33,25);line(33,19);curve(33,3,7,3,7,19);line(7,25);end();
  box(0,21,9,17,4);box(31,21,9,17,4);box(14,27,12,3,1.5f);
 }else{oval(21,21,19,19);oval(21,21,15,15);box(10,19,22,4,2);}
 if(FAILED(s->Close()))return nullptr;return path;
}
