package com.qrclientes;

import android.content.*;
import android.graphics.*;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import android.widget.*;

/** Visible camera overlay. Closing it also stops the scanner. */
final class FloatingCamera implements TextureView.SurfaceTextureListener {
 final ScannerService service;
 final WindowManager manager;
 final Handler main = new Handler(Looper.getMainLooper());
 LinearLayout box; TextureView preview; TextView status;
 WindowManager.LayoutParams params;
 Surface surface; boolean removed;
 FloatingCamera(ScannerService s){service=s;manager=(WindowManager)s.getSystemService(Context.WINDOW_SERVICE);}
 int dp(int n){return (int)(n*service.getResources().getDisplayMetrics().density);}
 void show(){
  if(box!=null||!Settings.canDrawOverlays(service))return;
  box=new LinearLayout(service);box.setOrientation(1);box.setPadding(dp(6),dp(6),dp(6),dp(6));box.setBackgroundColor(Color.rgb(20,25,35));
  TextView title=new TextView(service);title.setText("QR · cámara trasera ↕ arrastra");title.setTextColor(Color.WHITE);title.setPadding(0,dp(8),0,dp(8));box.addView(title);
  preview=new TextureView(service);box.addView(preview,new LinearLayout.LayoutParams(-1,dp(230)));preview.setSurfaceTextureListener(this);
  status=new TextView(service);status.setTextColor(Color.WHITE);status.setTextSize(12);box.addView(status);
  LinearLayout buttons=new LinearLayout(service);box.addView(buttons);
  Button open=new Button(service);open.setText("Abrir app");open.setTextSize(11);buttons.addView(open,new LinearLayout.LayoutParams(0,dp(48),1));
  open.setOnClickListener(v->service.startActivity(new Intent(service,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)));
  Button stop=new Button(service);stop.setText("Detener");stop.setTextSize(11);buttons.addView(stop,new LinearLayout.LayoutParams(0,dp(48),1));stop.setOnClickListener(v->service.stopSelf());
  params=new WindowManager.LayoutParams(dp(235),-2,WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE,PixelFormat.TRANSLUCENT);
  params.gravity=Gravity.TOP|Gravity.LEFT;params.x=dp(12);params.y=dp(80);
  title.setOnTouchListener(new View.OnTouchListener(){float x,y;int px,py;
   public boolean onTouch(View v,MotionEvent e){if(e.getAction()==MotionEvent.ACTION_DOWN){x=e.getRawX();y=e.getRawY();px=params.x;py=params.y;return true;}
    if(e.getAction()==MotionEvent.ACTION_MOVE){params.x=Math.max(0,Math.min(service.getResources().getDisplayMetrics().widthPixels-dp(235),px+(int)(e.getRawX()-x)));params.y=Math.max(0,Math.min(service.getResources().getDisplayMetrics().heightPixels-dp(100),py+(int)(e.getRawY()-y)));manager.updateViewLayout(box,params);return true;}return true;}});
  try{manager.addView(box,params);}catch(Exception e){box=null;service.state("No se pudo mostrar ventana flotante. Revisa permiso Mostrar sobre otras apps.");}
 }
 void state(String s){main.post(()->{if(status!=null&&!removed)status.setText(s);});}
 void updatePreview(){main.post(()->{if(!removed&&preview!=null&&preview.isAvailable()){preview.getSurfaceTexture().setDefaultBufferSize(service.previewWidth,service.previewHeight);transform(preview.getWidth(),preview.getHeight());service.handler.post(()->{if(!service.stopped&&service.camera!=null)service.setup();});}});}
 public void onSurfaceTextureAvailable(SurfaceTexture t,int w,int h){
  t.setDefaultBufferSize(service.previewWidth,service.previewHeight);surface=new Surface(t);
  transform(w,h);service.handler.post(()->{service.previewSurface=surface;if(service.camera!=null)service.setup();});
 }
 void transform(int w,int h){
  int displayRotation=manager.getDefaultDisplay().getRotation()*90;
  int rotation=(service.sensorOrientation-displayRotation+360)%360;
  Matrix matrix=new Matrix();matrix.postScale((float)service.previewWidth/w,(float)service.previewHeight/h);matrix.postRotate(rotation,service.previewWidth/2f,service.previewHeight/2f);
  RectF bounds=new RectF(0,0,w,h);matrix.mapRect(bounds);matrix.postTranslate(-bounds.left,-bounds.top);matrix.postScale(w/bounds.width(),h/bounds.height());preview.setTransform(matrix);
 }
 public void onSurfaceTextureSizeChanged(SurfaceTexture t,int w,int h){transform(w,h);}
 public boolean onSurfaceTextureDestroyed(SurfaceTexture t){service.handler.post(()->{service.previewSurface=null;if(!service.stopped&&service.camera!=null)service.setup();});return true;}
 public void onSurfaceTextureUpdated(SurfaceTexture t){}
 void remove(){removed=true;if(box!=null){try{manager.removeView(box);}catch(Exception ignored){}box=null;}if(surface!=null){surface.release();surface=null;}}
}
