package com.qrclientes;
import com.google.zxing.*;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;
public class QrDecoderTest {
 public static void main(String[] args)throws Exception{
  int cases=0;
  for(int size:new int[]{100,220})for(int rotation=0;rotation<4;rotation++)for(boolean inverted:new boolean[]{false,true}){
   int w=640,h=480;byte[] frame=new byte[w*h];java.util.Arrays.fill(frame,(byte)(inverted?0:255));
   BitMatrix qr=new QRCodeWriter().encode("CLIENTE-001",BarcodeFormat.QR_CODE,size,size);
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    int rx=x,ry=y;for(int t=0;t<rotation;t++){int old=rx;rx=size-1-ry;ry=old;}
    boolean black=qr.get(rx,ry);frame[(y+30)*w+x+370]=(byte)((black^inverted)?0:255);
   }
   QrDecoder d=new QrDecoder();Result result=null;
   for(int attempt=0;attempt<3;attempt++)try{result=d.decode(frame,w,h);break;}catch(NotFoundException e){}
   if(result==null||!"CLIENTE-001".equals(result.getText()))throw new AssertionError("size="+size+" rotation="+rotation+" inverted="+inverted);
   cases++;
  }
  System.out.println(cases+" QR cases passed (rotation, off-center, inverted)");
 }
}
