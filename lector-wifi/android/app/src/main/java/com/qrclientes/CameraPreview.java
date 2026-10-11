package com.qrclientes;

import android.content.Context;
import android.view.SurfaceView;

/** Fit the full camera image without stretching or applying a second rotation. */
final class CameraPreview extends SurfaceView {
 int bufferWidth=640,bufferHeight=480,sensorOrientation=90,displayDegrees=0;
 CameraPreview(Context context){super(context);getHolder().setFixedSize(bufferWidth,bufferHeight);}
 void configure(int w,int h,int sensor,int display){
  boolean sizeChanged=w!=bufferWidth||h!=bufferHeight;
  boolean geometryChanged=sizeChanged||sensor!=sensorOrientation||display!=displayDegrees;
  bufferWidth=w;bufferHeight=h;sensorOrientation=sensor;displayDegrees=display;
  if(sizeChanged)getHolder().setFixedSize(w,h);
  if(geometryChanged){applyFit(getWidth(),getHeight());requestLayout();}
 }
 void applyFit(int w,int h){
  float[] scale=PreviewGeometry.fit(w,h,bufferWidth,bufferHeight,PreviewGeometry.relativeRotation(sensorOrientation,displayDegrees));
  setPivotX(w/2f);setPivotY(h/2f);setScaleX(scale[0]);setScaleY(scale[1]);
 }
 @Override protected void onMeasure(int widthSpec,int heightSpec){
  int w=MeasureSpec.getSize(widthSpec),h=MeasureSpec.getSize(heightSpec);
  setMeasuredDimension(w,h);applyFit(w,h);
 }
}
