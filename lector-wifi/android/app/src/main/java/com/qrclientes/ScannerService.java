package com.qrclientes;
import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.os.*;
import android.hardware.camera2.*;
import android.media.*;
import android.graphics.ImageFormat;
import android.util.Size;
import android.view.Surface;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import java.nio.ByteBuffer;
import java.net.*;
import java.io.*;
import java.util.*;
import java.util.concurrent.*;
import org.json.JSONObject;

public class ScannerService extends Service {
 HandlerThread thread; Handler handler; CameraDevice camera; CameraCaptureSession session; ImageReader images;
 PowerManager.WakeLock wake; android.net.wifi.WifiManager.WifiLock wifi;
 PendingDb db; ScheduledExecutorService network; volatile boolean stopped=false; boolean opening=false;
 long lastFrame=0,lastSeen=0;String lastCode="";
 Object previewOwner; volatile Surface previewSurface; volatile int previewWidth=640,previewHeight=480,sensorOrientation=90;
 int configuration=0;
 int afMode=CaptureRequest.CONTROL_AF_MODE_OFF;
 MultiFormatReader decoder=new MultiFormatReader();
 public class LocalBinder extends Binder { ScannerService getService(){return ScannerService.this;} }
 final IBinder binder=new LocalBinder();
 public IBinder onBind(Intent i){return binder;}
 void attachPreview(Object owner,Surface surface,boolean force){handler.post(()->{if(stopped||!surface.isValid())return;if(!force&&previewOwner==owner&&previewSurface==surface)return;previewOwner=owner;previewSurface=surface;if(camera!=null)setup();});}
 void detachPreview(Object owner,Runnable done){handler.post(()->{if(previewOwner==owner){previewOwner=null;previewSurface=null;if(!stopped&&camera!=null)setup();}done.run();});}
 public void onCreate(){
  super.onCreate();db=new PendingDb(this);thread=new HandlerThread("QR-Camera");thread.start();handler=new Handler(thread.getLooper());
  Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);hints.put(DecodeHintType.POSSIBLE_FORMATS,Collections.singletonList(BarcodeFormat.QR_CODE));hints.put(DecodeHintType.TRY_HARDER,true);decoder.setHints(hints);
  NotificationManager manager=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);manager.createNotificationChannel(new NotificationChannel("scanner","Cámara QR activa",NotificationManager.IMPORTANCE_LOW));
  Intent end=new Intent(this,ScannerService.class).setAction("STOP");PendingIntent stop=PendingIntent.getService(this,2,end,PendingIntent.FLAG_IMMUTABLE);
  PendingIntent open=PendingIntent.getActivity(this,1,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE);
  Notification n=new Notification.Builder(this,"scanner").setSmallIcon(android.R.drawable.ic_menu_camera).setContentTitle("QR Clientes · cámara trasera activa").setContentText("Leyendo QR en segundo plano. Toca para abrir.").setOngoing(true).setContentIntent(open).addAction(new Notification.Action.Builder(null,"Detener",stop).build()).build();
  if(Build.VERSION.SDK_INT>=29)startForeground(1,n,ServiceInfo.FOREGROUND_SERVICE_TYPE_CAMERA);else startForeground(1,n);
  wake=((PowerManager)getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"QRClientes:scanner");wake.acquire();
  wifi=((android.net.wifi.WifiManager)getApplicationContext().getSystemService(WIFI_SERVICE)).createWifiLock(android.net.wifi.WifiManager.WIFI_MODE_FULL_HIGH_PERF,"QRClientes:WiFi");wifi.acquire();
  network=Executors.newSingleThreadScheduledExecutor();network.scheduleWithFixedDelay(()->sendPending(),0,2,TimeUnit.SECONDS);
 }
 public int onStartCommand(Intent i,int flags,int id){if(i!=null&&"STOP".equals(i.getAction())){stopSelf();return START_NOT_STICKY;}handler.post(()->openCamera());return START_NOT_STICKY;}
 void state(String msg){getSharedPreferences("qr",0).edit().putString("estado",msg).apply();}
 void openCamera(){
  if(stopped||camera!=null||opening)return;opening=true;
  try{
   CameraManager m=(CameraManager)getSystemService(CAMERA_SERVICE);String cameraId=null;CameraCharacteristics characteristics=null;
   for(String id:m.getCameraIdList()){CameraCharacteristics cc=m.getCameraCharacteristics(id);Integer facing=cc.get(CameraCharacteristics.LENS_FACING);if(facing!=null&&facing==CameraCharacteristics.LENS_FACING_BACK){cameraId=id;characteristics=cc;break;}}
   if(cameraId==null)throw new Exception("Este celular no tiene cámara trasera");
   Integer orientation=characteristics.get(CameraCharacteristics.SENSOR_ORIENTATION);if(orientation!=null)sensorOrientation=orientation;
   int[] modes=characteristics.get(CameraCharacteristics.CONTROL_AF_AVAILABLE_MODES);
   if(modes!=null){for(int mode:modes)if(mode==CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_VIDEO)afMode=mode;for(int mode:modes)if(mode==CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_PICTURE)afMode=mode;}
   Size[] sizes=characteristics.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP).getOutputSizes(ImageFormat.YUV_420_888);
   Size[] previewSizes=characteristics.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP).getOutputSizes(android.view.SurfaceHolder.class);
   Set<String> supported=new HashSet<>();for(Size ps:previewSizes)supported.add(ps.toString());
   Size size=Arrays.stream(sizes).filter(s->supported.contains(s.toString())).min(Comparator.comparingLong(s->Math.abs((long)s.getWidth()*s.getHeight()-1280L*720))).get();
   previewWidth=size.getWidth();previewHeight=size.getHeight();
   images=ImageReader.newInstance(size.getWidth(),size.getHeight(),ImageFormat.YUV_420_888,2);images.setOnImageAvailableListener(r->analyze(r),handler);
   m.openCamera(cameraId,new CameraDevice.StateCallback(){
    public void onOpened(CameraDevice c){opening=false;if(stopped){c.close();return;}camera=c;setup();}
    public void onDisconnected(CameraDevice c){c.close();camera=null;opening=false;retry("Cámara interrumpida; reintentando");}
    public void onError(CameraDevice c,int error){c.close();camera=null;opening=false;retry("Cámara ocupada o bloqueada; abre la app si persiste");}
   },handler);
  }catch(Exception e){opening=false;retry("No se pudo abrir cámara: "+e.getMessage());}
 }
 void setup(){try{
  if(session!=null){session.close();session=null;}
  if(camera==null||images==null||stopped)return;
  final int generation=++configuration;
  List<Surface> surfaces=new ArrayList<>();surfaces.add(images.getSurface());final Surface shown=previewSurface;if(shown!=null&&shown.isValid())surfaces.add(shown);
  camera.createCaptureSession(surfaces,new CameraCaptureSession.StateCallback(){
   public void onConfigured(CameraCaptureSession s){if(stopped||camera==null||generation!=configuration){s.close();return;}session=s;try{CaptureRequest.Builder b=camera.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);b.addTarget(images.getSurface());if(shown!=null&&shown.isValid())b.addTarget(shown);b.set(CaptureRequest.CONTROL_AF_MODE,afMode);s.setRepeatingRequest(b.build(),null,handler);state("Cámara activa · esperando QR");}catch(Exception e){retry("Error de captura; reintentando");}}
   public void onConfigureFailed(CameraCaptureSession s){if(generation!=configuration||stopped)return;retry("Error de cámara; reintentando");}
  },handler);
 }catch(Exception e){retry("Cámara no disponible; reintentando");}}
 void retry(String msg){state(msg);if(stopped)return;handler.postDelayed(()->{if(stopped)return;closeCamera();openCamera();},5000);}
 void closeCamera(){if(session!=null){session.close();session=null;}if(camera!=null){camera.close();camera=null;}if(images!=null){images.close();images=null;}}
 void analyze(ImageReader r){
  Image image=null;try{
   image=r.acquireLatestImage();if(image==null)return;long now=SystemClock.elapsedRealtime();if(now-lastFrame<180)return;lastFrame=now;
   int w=image.getWidth(),h=image.getHeight();Image.Plane plane=image.getPlanes()[0];ByteBuffer buf=plane.getBuffer();int row=plane.getRowStride(),pixel=plane.getPixelStride();byte[] y=new byte[w*h];int offset=buf.position();
   for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)y[yy*w+xx]=buf.get(offset+yy*row+xx*pixel);
   LuminanceSource src=new PlanarYUVLuminanceSource(y,w,h,0,0,w,h,false);Result result;
   try{result=decoder.decodeWithState(new BinaryBitmap(new HybridBinarizer(src)));}catch(NotFoundException e){decoder.reset();result=decoder.decodeWithState(new BinaryBitmap(new HybridBinarizer(src.invert())));}
   String code=result.getText().trim();if(code.length()==0||code.length()>256){state("QR inválido: usa un código de cliente de hasta 256 caracteres");return;}
   boolean fresh=!code.equals(lastCode)||now-lastSeen>2000;lastSeen=now;
   if(fresh){if(db.add(code)){lastCode=code;getSharedPreferences("qr",0).edit().putString("ultimoQR",code).apply();state("QR guardado: "+code);if(network!=null&&!stopped)network.execute(()->sendPending());}else{state("Cola llena (1000). Conecta la PC para enviar pendientes.");}}
  }catch(NotFoundException e){}catch(Exception e){state("Error al leer QR: "+e.getMessage());}finally{decoder.reset();if(image!=null)image.close();}
 }
 void connection(String msg){getSharedPreferences("qr",0).edit().putString("conexion",msg).apply();}
 void sendPending(){
  if(stopped)return;
  HttpURLConnection c=null;
  try{
   String[] pending=db.first();if(pending==null)return;
   String url=getSharedPreferences("qr",0).getString("url","");String key=getSharedPreferences("qr",0).getString("token","");
   c=(HttpURLConnection)new URL(url+"/scan").openConnection();c.setRequestMethod("POST");c.setConnectTimeout(4000);c.setReadTimeout(4000);c.setDoOutput(true);c.setRequestProperty("Authorization","Bearer "+key);c.setRequestProperty("Content-Type","application/json; charset=utf-8");
   byte[] body=new JSONObject().put("codigo",pending[1]).put("evento",pending[0]).toString().getBytes("UTF-8");c.setFixedLengthStreamingMode(body.length);
   try(OutputStream out=c.getOutputStream()){out.write(body);}int status=c.getResponseCode();
   if(status==200){try(InputStream in=c.getInputStream()){ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] b=new byte[512];int n;while((n=in.read(b))!=-1){if(bytes.size()+n>4096)throw new IOException("Respuesta inválida");bytes.write(b,0,n);}JSONObject result=new JSONObject(bytes.toString("UTF-8"));if(!result.optBoolean("ok"))throw new IOException("Respuesta inválida");}db.remove(pending[0]);connection("Enviado a PC: "+pending[1]);}
   else connection(status==401?"Clave incorrecta. Revisa la configuración.":"PC respondió "+status+"; lectura pendiente");
  }catch(Exception e){if(!stopped)connection("PC sin conexión · lecturas guardadas, reintentando");}finally{if(c!=null)c.disconnect();}
 }
 public void onDestroy(){
  stopped=true;previewOwner=null;previewSurface=null;if(network!=null){network.shutdownNow();}
  if(handler!=null){handler.removeCallbacksAndMessages(null);handler.post(()->{closeCamera();thread.quitSafely();});}
  if(wake!=null&&wake.isHeld())wake.release();if(wifi!=null&&wifi.isHeld())wifi.release();state("Lector detenido");stopForeground(true);super.onDestroy();
 }
}
