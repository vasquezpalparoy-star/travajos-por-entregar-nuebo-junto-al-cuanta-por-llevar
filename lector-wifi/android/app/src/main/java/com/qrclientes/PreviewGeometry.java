package com.qrclientes;

/** Geometry only: the SurfaceView itself handles camera-buffer rotation. */
final class PreviewGeometry {
 static int relativeRotation(int sensor, int displayDegrees){return (sensor+displayDegrees+360)%360;}
 static float[] fit(int viewWidth,int viewHeight,int bufferWidth,int bufferHeight,int rotation){
  if(viewWidth<=0||viewHeight<=0||bufferWidth<=0||bufferHeight<=0)return new float[]{1f,1f};
  int imageWidth=rotation%180==0?bufferWidth:bufferHeight;
  int imageHeight=rotation%180==0?bufferHeight:bufferWidth;
  float scale=Math.min((float)viewWidth/imageWidth,(float)viewHeight/imageHeight);
  return new float[]{imageWidth*scale/viewWidth,imageHeight*scale/viewHeight};
 }
}
