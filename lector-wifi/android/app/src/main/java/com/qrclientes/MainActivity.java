package com.qrclientes;
import android.app.*;
import android.os.*;
import android.Manifest;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.view.*;
import android.widget.*;
import java.net.*;


public class MainActivity extends Activity {
 EditText address,token; TextView status,lastRead,connectionStatus; LinearLayout panel; boolean dark=false; boolean scannerScreen=false,bound=false,resumed=false; ScannerService scanner; Handler cameraCleanup; CameraPreview preview; Surface previewSurface;
 Handler handler=new Handler();
 Runnable update=new Runnable(){public void run(){if(scannerScreen&&scanner!=null&&resumed)attachPreview();if(lastRead!=null)lastRead.setText("Último QR: "+getSharedPreferences("qr",0).getString("ultimoQR","ninguno detectado"));if(connectionStatus!=null)connectionStatus.setText(getSharedPreferences("qr",0).getString("conexion","Conexión: pulsa Probar conexión"));if(status!=null)status.setText(getSharedPreferences("qr",0).getString("estado","Lector detenido"));handler.postDelayed(this,500);}};
 public void onCreate(Bundle saved){super.onCreate(saved);if(saved!=null&&saved.getBoolean("scannerScreen"))showScanner();else showForm();handler.post(update);}
 public void onSaveInstanceState(Bundle b){b.putBoolean("scannerScreen",scannerScreen);super.onSaveInstanceState(b);}
 void showForm(){
  detachPreview();scannerScreen=false;dark=false; getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  WindowManager.LayoutParams p=getWindow().getAttributes();p.screenBrightness=-1;getWindow().setAttributes(p);
  panel=new LinearLayout(this);panel.setOrientation(1);panel.setPadding(28,30,28,20);
  ScrollView scroll=new ScrollView(this);scroll.addView(panel);setContentView(scroll);
  label("QR Clientes · versión 1.7",26);label("Cámara trasera · conexión Wi-Fi a tu PC",16);
  label("Dirección que muestra el programa de PC",14);
  address=new EditText(this);address.setSingleLine();address.setInputType(17);address.setHint("http://192.168.1.10:8765");address.setText(getSharedPreferences("qr",0).getString("url",""));panel.addView(address);
  label("Clave de conexión de la PC",14);token=new EditText(this);token.setSingleLine();token.setText(getSharedPreferences("qr",0).getString("token",""));panel.addView(token);
  button("Probar conexión",()->test());
  button("Abrir cámara y escanear",()->start());
  button("Enviar QR de prueba 001",()->sendTest());
  button("Detener lector",()->{stopService(new Intent(this,ScannerService.class));getSharedPreferences("qr",0).edit().putString("estado","Lector detenido").apply();});
  button("Pantalla negra (toca para volver)",()->black());
  button("Ajustes de batería",()->{try{startActivity(new Intent(android.provider.Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS));}catch(Exception e){Toast.makeText(this,"Abre Ajustes > Aplicaciones > QR Clientes > Batería",1).show();}});
  status=label("",16);lastRead=label("",15);connectionStatus=label("",15);
  label("Inicia el lector antes de cambiar de aplicación o bloquear. La notificación indica que la cámara está activa. No se graban fotos ni video. Las lecturas pendientes se envían al volver el Wi-Fi. Después de reiniciar el teléfono, abre la app e inicia el lector otra vez.",14);
 }
 TextView label(String s,int size){TextView v=new TextView(this);v.setText(s);v.setTextSize(size);v.setPadding(0,10,0,10);panel.addView(v);return v;}
 void button(String s,Runnable action){Button b=new Button(this);b.setText(s);b.setOnClickListener(v->action.run());panel.addView(b);}
 boolean save(){
  try{
   String url=address.getText().toString().trim().replaceAll("/+$","");URI u=new URI(url);
   if(!"http".equals(u.getScheme())||u.getHost()==null||u.getUserInfo()!=null||(u.getPath()!=null&&!u.getPath().isEmpty())||u.getQuery()!=null||u.getFragment()!=null)throw new Exception();
   String key=token.getText().toString().trim();if(!key.matches("[a-fA-F0-9]{32}"))throw new Exception();
   getSharedPreferences("qr",0).edit().putString("url",url).putString("token",key).apply();return true;
  }catch(Exception e){Toast.makeText(this,"Copia la dirección http://IP:8765 y la clave de 32 caracteres de la PC",1).show();return false;}
 }
 void test(){if(!save())return;new Thread(()->{
  String result;HttpURLConnection c=null;
  try{c=(HttpURLConnection)new URL(getSharedPreferences("qr",0).getString("url","")+"/health").openConnection();c.setConnectTimeout(4000);c.setReadTimeout(4000);c.setRequestProperty("Authorization","Bearer "+getSharedPreferences("qr",0).getString("token",""));result=c.getResponseCode()==200?"Conexión correcta":"Clave incorrecta o servidor incompatible";}
  catch(Exception e){result="Sin conexión. Revisa Wi-Fi, IP y firewall de la PC.";}finally{if(c!=null)c.disconnect();}
  getSharedPreferences("qr",0).edit().putString("conexion",result).apply();final String msg=result;runOnUiThread(()->Toast.makeText(this,msg,1).show());
 }).start();}
 public void onResume(){super.onResume();resumed=true;if(scannerScreen)connectPreview();}
 public void onStop(){resumed=false;detachPreview();getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);super.onStop();}
 final ServiceConnection connection=new ServiceConnection(){
  public void onServiceConnected(ComponentName name,IBinder binder){scanner=((ScannerService.LocalBinder)binder).getService();cameraCleanup=scanner.handler;attachPreview();}
  public void onServiceDisconnected(ComponentName name){scanner=null;}
 };
 void connectPreview(){
  if(!scannerScreen||!resumed)return;
  getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  if(!bound)bound=bindService(new Intent(this,ScannerService.class),connection,0);
  else attachPreview();
 }
 void attachPreview(){
  if(resumed&&scannerScreen&&scanner!=null&&preview!=null&&preview.getHolder().getSurface().isValid()){
   transformPreview();previewSurface=preview.getHolder().getSurface();
   scanner.attachPreview(this,previewSurface,false);
  }
 }
 void detachPreview(){
  previewSurface=null;
  if(scanner!=null)scanner.detachPreview(this,()->{});
  if(bound){unbindService(connection);bound=false;}scanner=null;
 }
 void transformPreview(){
  if(scanner==null||preview==null)return;
  preview.configure(scanner.previewWidth,scanner.previewHeight,scanner.sensorOrientation,getWindowManager().getDefaultDisplay().getRotation()*90);
 }
 void showScanner(){
  scannerScreen=true;dark=false;getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  panel=new LinearLayout(this);panel.setOrientation(1);panel.setPadding(16,16,16,16);setContentView(panel);
  label("Cámara trasera · QR Clientes 1.7",20);
  FrameLayout viewfinder=new FrameLayout(this);viewfinder.setBackgroundColor(Color.BLACK);panel.addView(viewfinder,new LinearLayout.LayoutParams(-1,0,1));
  preview=new CameraPreview(this);viewfinder.addView(preview,new FrameLayout.LayoutParams(-1,-1,Gravity.CENTER));
  preview.getHolder().addCallback(new SurfaceHolder.Callback(){
   public void surfaceCreated(SurfaceHolder holder){connectPreview();attachPreview();}
   public void surfaceChanged(SurfaceHolder holder,int format,int w,int h){transformPreview();if(scanner!=null&&resumed)scanner.attachPreview(MainActivity.this,holder.getSurface(),true);}
   public void surfaceDestroyed(SurfaceHolder holder){
    detachPreview();
    if(cameraCleanup!=null){java.util.concurrent.CountDownLatch done=new java.util.concurrent.CountDownLatch(1);if(cameraCleanup.post(()->done.countDown())){try{done.await(500,java.util.concurrent.TimeUnit.MILLISECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}}}
   }
  });
  status=label("Abriendo cámara…",16);lastRead=label("",15);connectionStatus=label("",15);
  label("Sostén el QR a 15–25 cm, con buena luz. El resultado aparecerá en la PC. Puedes cambiar de aplicación; el lector seguirá activo con una notificación.",14);
  button("Enviar QR de prueba 001",()->sendTest());
  button("Seguir en segundo plano",()->{detachPreview();moveTaskToBack(true);});
  button("Detener y volver",()->{detachPreview();stopService(new Intent(this,ScannerService.class));showForm();});
  connectPreview();
 }
 void sendTest(){
  if(!scannerScreen&&!save())return;
  new Thread(()->{
   HttpURLConnection c=null;String result;
   try{
    String url=getSharedPreferences("qr",0).getString("url","");String key=getSharedPreferences("qr",0).getString("token","");
    c=(HttpURLConnection)new URL(url+"/scan").openConnection();c.setRequestMethod("POST");c.setDoOutput(true);c.setConnectTimeout(4000);c.setReadTimeout(4000);c.setRequestProperty("Authorization","Bearer "+key);c.setRequestProperty("Content-Type","application/json");
    byte[] data=new org.json.JSONObject().put("codigo","001").put("evento",java.util.UUID.randomUUID().toString()).toString().getBytes("UTF-8");c.setFixedLengthStreamingMode(data.length);try(java.io.OutputStream out=c.getOutputStream()){out.write(data);}
    int code=c.getResponseCode();result=code==200?"Prueba 001 enviada a PC. Revisa la ventana de Grafiplot.":code==401?"Clave incorrecta: copia la clave de la PC.":"PC respondió "+code;
   }catch(Exception e){result="Sin conexión con PC. Revisa dirección, Wi-Fi y firewall.";}finally{if(c!=null)c.disconnect();}
   getSharedPreferences("qr",0).edit().putString("conexion",result).apply();final String message=result;runOnUiThread(()->Toast.makeText(this,message,1).show());
  }).start();
 }
 void start(){
  if(!save())return;
  if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.CAMERA},1);return;}
  if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},2);return;}
  begin();
 }
 void begin(){
  try{startForegroundService(new Intent(this,ScannerService.class));showScanner();}
  catch(Exception e){Toast.makeText(this,"No se pudo iniciar: "+e.getMessage(),1).show();}
 }
 public void onRequestPermissionsResult(int req,String[] p,int[] r){super.onRequestPermissionsResult(req,p,r);if(req==1&&r.length>0&&r[0]==PackageManager.PERMISSION_GRANTED)start();else if(req==2)startAfterNotifications();}
 void startAfterNotifications(){begin();}
 void black(){dark=true;getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);WindowManager.LayoutParams p=getWindow().getAttributes();p.screenBrightness=0.01f;getWindow().setAttributes(p);TextView black=new TextView(this);black.setBackgroundColor(Color.BLACK);black.setOnClickListener(v->showForm());setContentView(black);}
 public void onBackPressed(){if(dark)showForm();else if(scannerScreen){detachPreview();moveTaskToBack(true);}else super.onBackPressed();}
 public void onDestroy(){detachPreview();handler.removeCallbacks(update);super.onDestroy();}
}
