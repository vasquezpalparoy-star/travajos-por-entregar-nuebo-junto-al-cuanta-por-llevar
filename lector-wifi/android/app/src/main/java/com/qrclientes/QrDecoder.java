package com.qrclientes;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import java.util.*;
/** Fast full-frame scan, with harder and inverted fallbacks. No image rotation/stretch. */
final class QrDecoder {
 private final MultiFormatReader reader=new MultiFormatReader();
 private int misses;
 Result decode(byte[] y,int w,int h) throws NotFoundException {
  LuminanceSource source=new PlanarYUVLuminanceSource(y,w,h,0,0,w,h,false);
  Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);
  hints.put(DecodeHintType.POSSIBLE_FORMATS,Collections.singletonList(BarcodeFormat.QR_CODE));
  try { Result result=read(source,hints);misses=0;return result; }
  catch(NotFoundException missing){
   if(++misses%3!=0)throw missing;
   hints.put(DecodeHintType.TRY_HARDER,true);
   try {return read(source,hints);}catch(NotFoundException e){return read(source.invert(),hints);}
  }
 }
 private Result read(LuminanceSource source,Map<DecodeHintType,Object> hints) throws NotFoundException {
  try{return reader.decode(new BinaryBitmap(new HybridBinarizer(source)),hints);}finally{reader.reset();}
 }
}
