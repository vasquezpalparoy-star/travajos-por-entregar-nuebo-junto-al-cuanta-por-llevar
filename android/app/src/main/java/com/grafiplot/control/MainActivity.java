package com.grafiplot.control;
import android.Manifest;
import android.app.*;
import android.os.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.webkit.*;
import android.widget.*;
import android.graphics.Color;
import android.print.*;
import java.io.*;
import java.util.*;

public class MainActivity extends Activity {
    private WebView web;
    private PermissionRequest camera;
    private byte[] pending;
    private static final String HOST="appassets.androidplatform.net";
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        LinearLayout frame=new LinearLayout(this);frame.setOrientation(LinearLayout.VERTICAL);frame.setBackgroundColor(Color.rgb(245,246,250));
        frame.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets.consumeSystemWindowInsets();});
        web=new WebView(this);frame.addView(web,new LinearLayout.LayoutParams(-1,-1));setContentView(frame);frame.requestApplyInsets();
        WebSettings ws=web.getSettings();ws.setJavaScriptEnabled(true);ws.setDomStorageEnabled(true);ws.setAllowFileAccess(false);ws.setAllowContentAccess(false);ws.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);ws.setMediaPlaybackRequiresUserGesture(true);ws.setSupportMultipleWindows(false);
        web.addJavascriptInterface(new Bridge(),"Android");
        web.setWebViewClient(new WebViewClient(){
            @Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest req){
                Uri u=req.getUrl();if(!HOST.equals(u.getHost()))return null;
                String path=u.getPath();if(path==null||path.contains(".."))return null;
                if(path.equals("/"))path="/index.html";
                String mime=path.endsWith(".html")?"text/html":path.endsWith(".js")?"application/javascript":path.endsWith(".css")?"text/css":path.endsWith(".png")?"image/png":"application/octet-stream";
                try {return new WebResourceResponse(mime,"UTF-8",getAssets().open("web"+path));}catch(IOException e){return new WebResourceResponse("text/plain","UTF-8",404,"Not Found",Collections.emptyMap(),new ByteArrayInputStream(new byte[0]));}
            }
            @Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest req){if(HOST.equals(req.getUrl().getHost()))return false;external(req.getUrl());return true;}
        });
        web.setWebChromeClient(new WebChromeClient(){
            @Override public void onPermissionRequest(PermissionRequest req){runOnUiThread(()->{if(!("https://"+HOST).equals(req.getOrigin().toString().replaceAll("/$",""))){req.deny();return;}boolean video=Arrays.asList(req.getResources()).contains(PermissionRequest.RESOURCE_VIDEO_CAPTURE);if(!video){req.deny();return;}camera=req;if(checkSelfPermission(Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED){req.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});camera=null;}else requestPermissions(new String[]{Manifest.permission.CAMERA},10);});}
            @Override public void onPermissionRequestCanceled(PermissionRequest req){runOnUiThread(()->{if(camera==req)camera=null;});}
            @Override public boolean onJsAlert(WebView v,String url,String message,JsResult result){new AlertDialog.Builder(MainActivity.this).setMessage(message).setPositiveButton("Aceptar",(d,w)->result.confirm()).setOnCancelListener(d->result.cancel()).show();return true;}
        });
        web.loadUrl("https://"+HOST+"/index.html");
    }
    private void external(Uri uri){String s=uri.getScheme();if(!Arrays.asList("https","mailto","tel").contains(s))return;try{startActivity(new Intent(Intent.ACTION_VIEW,uri));}catch(ActivityNotFoundException e){toast("No hay una aplicación para abrir este enlace");}}
    @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] grants){super.onRequestPermissionsResult(code,permissions,grants);if(code==10&&camera!=null){if(grants.length>0&&grants[0]==PackageManager.PERMISSION_GRANTED)camera.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});else{camera.deny();toast("Activa el permiso de cámara para escanear QR");}camera=null;}}
    private void toast(String text){Toast.makeText(this,text,Toast.LENGTH_LONG).show();}
    public class Bridge {
        @JavascriptInterface public void exportFile(String base64,String filename,String mime,String mode){
            if(base64==null||base64.length()>40000000)return;
            final byte[] bytes;try{bytes=android.util.Base64.decode(base64,android.util.Base64.DEFAULT);}catch(Exception e){return;}
            String clean=filename.replaceAll("[^a-zA-Z0-9_.-]","_");
            runOnUiThread(()->{if("print".equals(mode)&&"application/pdf".equals(mime)){printPdf(bytes,clean);return;}if(pending!=null){toast("Termina de guardar el archivo anterior");return;}pending=bytes;Intent save=new Intent(Intent.ACTION_CREATE_DOCUMENT);save.addCategory(Intent.CATEGORY_OPENABLE);save.setType(mime);save.putExtra(Intent.EXTRA_TITLE,clean);try{startActivityForResult(save,20);}catch(ActivityNotFoundException e){pending=null;toast("No se pudo abrir el selector de archivos");}});
        }
    }
    @Override protected void onActivityResult(int code,int result,Intent data){super.onActivityResult(code,result,data);if(code!=20)return;final byte[] bytes=pending;pending=null;if(result!=RESULT_OK||data==null||bytes==null)return;Uri uri=data.getData();new Thread(()->{try(OutputStream out=getContentResolver().openOutputStream(uri)){if(out==null)throw new IOException();out.write(bytes);runOnUiThread(()->toast("Archivo guardado"));}catch(Exception e){runOnUiThread(()->toast("No se pudo guardar el archivo"));}}).start();}
    private void printPdf(byte[] bytes,String name){
        PrintManager pm=(PrintManager)getSystemService(PRINT_SERVICE);
        pm.print(name,new PrintDocumentAdapter(){
            public void onLayout(PrintAttributes old,PrintAttributes current,CancellationSignal cancel,LayoutResultCallback cb,Bundle extras){if(cancel.isCanceled()){cb.onLayoutCancelled();return;}cb.onLayoutFinished(new PrintDocumentInfo.Builder(name).setContentType(PrintDocumentInfo.CONTENT_TYPE_DOCUMENT).build(),true);}
            public void onWrite(PageRange[] pages,ParcelFileDescriptor dest,CancellationSignal cancel,WriteResultCallback cb){if(cancel.isCanceled()){cb.onWriteCancelled();return;}try(FileOutputStream out=new FileOutputStream(dest.getFileDescriptor())){out.write(bytes);cb.onWriteFinished(new PageRange[]{PageRange.ALL_PAGES});}catch(IOException e){cb.onWriteFailed("No se pudo imprimir");}}
        },null);
    }
    @Override public void onBackPressed(){web.evaluateJavascript("window.androidBack ? androidBack() : false",value->{if(!"true".equals(value))new AlertDialog.Builder(this).setMessage("¿Salir de Grafiplot?").setPositiveButton("Salir",(d,w)->finish()).setNegativeButton("Continuar",null).show();});}
    @Override protected void onPause(){web.evaluateJavascript("if(window.toggleScanner && document.getElementById('scanner-btn-text')?.textContent==='Cerrar Cámara') toggleScanner()",null);web.onPause();super.onPause();}
    @Override protected void onResume(){super.onResume();if(web!=null)web.onResume();}
    @Override protected void onDestroy(){if(camera!=null)camera.deny();if(web!=null){web.removeJavascriptInterface("Android");web.destroy();}super.onDestroy();}
}
