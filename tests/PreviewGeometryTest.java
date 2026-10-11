package com.qrclientes;

public final class PreviewGeometryTest {
 static void near(float actual,float expected){if(Math.abs(actual-expected)>0.002f)throw new AssertionError(actual+" != "+expected);}
 public static void main(String[] args){
  float[] portrait=PreviewGeometry.fit(400,500,1280,720,90);
  near(portrait[0]*400,281.25f);near(portrait[1]*500,500f);
  float[] landscape=PreviewGeometry.fit(800,300,1280,720,180);
  near(landscape[0]*800,533.3333f);near(landscape[1]*300,300f);
  if(PreviewGeometry.relativeRotation(90,90)!=180||PreviewGeometry.relativeRotation(270,90)!=0)throw new AssertionError("Back-camera rotation sign");
  int count=0;
  for(int sensor:new int[]{0,90,180,270})for(int display:new int[]{0,90,180,270})for(int[] view:new int[][]{{400,700},{800,300},{350,350},{1000,170}}){
   int r=PreviewGeometry.relativeRotation(sensor,display);float[] fit=PreviewGeometry.fit(view[0],view[1],1280,720,r);
   float width=view[0]*fit[0],height=view[1]*fit[1];
   if(width>view[0]+0.002f||height>view[1]+0.002f)throw new AssertionError("Preview cropped");
   near(width/height,r%180==0?1280f/720f:720f/1280f);
   if(Math.abs(width-view[0])>0.002f&&Math.abs(height-view[1])>0.002f)throw new AssertionError("Preview not fitted");
   count++;
  }
  System.out.println("Preview proportions verified in "+count+" sensor/display/window combinations");
 }
}
