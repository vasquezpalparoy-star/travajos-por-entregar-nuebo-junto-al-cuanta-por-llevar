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
import java.util.concurrent.Executors;

public class MainActivity extends Activity {
 EditText address,token; TextView status; LinearLayout panel; boolean dark=false;
 Handler handler=new Handler();
 Runnable update=new Runnable(){public void run(){if(status!=null)status.setText(getSharedPreferences("qr",0).getString("estado","Lector detenido"));handler.postDelayed(this,1500);}};
 public void onCreate(Bundle saved){super.onCreate(saved);showForm();handler.post(update);}
 void showForm(){
  dark=false; getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
  WindowManager.LayoutParams p=getWindow().getAttributes();p.screenBrightness=-1;getWindow().setAttributes(p);
  panel=new LinearLayout(this);panel.setOrientation(1);panel.setPadding(28,30,28,20);
  ScrollView scroll=new ScrollView(this);scroll.addView(panel);setContentView(scroll);
  label("QR Clientes",26);label("Cámara trasera · conexión Wi-Fi a tu PC",16);
  label("Dirección que muestra el programa de PC",14);
  address=new EditText(this);address.setSingleLine();address.setInputType(17);address.setHint("http://192.168.1.10:8765");address.setText(getSharedPreferences("qr",0).getString("url",""));panel.addView(address);
  label("Clave de conexión de la PC",14);token=new EditText(this);token.setSingleLine();token.setText(getSharedPreferences("qr",0).getString("token",""));panel.addView(token);
  button("Probar conexión",()->test());
  button("Iniciar lector en segundo plano",()->start());
  button("Detener lector",()->{stopService(new Intent(this,ScannerService.class));getSharedPreferences("qr",0).edit().putString("estado","Lector detenido").apply();});
  button("Pantalla negra (toca para volver)",()->black());
  button("Ajustes de batería",()->{try{startActivity(new Intent(android.provider.Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS));}catch(Exception e){Toast.makeText(this,"Abre Ajustes > Aplicaciones > QR Clientes > Batería",1).show();}});
  status=label("",16);
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
  final String msg=result;runOnUiThread(()->Toast.makeText(this,msg,1).show());
 }).start();}
 void start(){
  if(!save())return;
  if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.CAMERA},1);return;}
  if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},2);return;}
  begin();
 }
 void begin(){
  try{startForegroundService(new Intent(this,ScannerService.class));Toast.makeText(this,"Lector iniciado; puedes cambiar de aplicación",1).show();}
  catch(Exception e){Toast.makeText(this,"No se pudo iniciar: "+e.getMessage(),1).show();}
 }
 public void onRequestPermissionsResult(int req,String[] p,int[] r){super.onRequestPermissionsResult(req,p,r);if(req==1&&r.length>0&&r[0]==PackageManager.PERMISSION_GRANTED)start();else if(req==2)begin();}
 void black(){dark=true;getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);WindowManager.LayoutParams p=getWindow().getAttributes();p.screenBrightness=0.01f;getWindow().setAttributes(p);TextView black=new TextView(this);black.setBackgroundColor(Color.BLACK);black.setOnClickListener(v->showForm());setContentView(black);}
 public void onBackPressed(){if(dark)showForm();else super.onBackPressed();}
 public void onDestroy(){handler.removeCallbacks(update);super.onDestroy();}
}
