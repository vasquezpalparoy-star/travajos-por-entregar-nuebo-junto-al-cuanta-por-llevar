package com.grafiplot.trabajosqr;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;
import android.os.SystemClock;
import androidx.lifecycle.LifecycleService;
import androidx.camera.core.*;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.core.content.ContextCompat;
import com.google.common.util.concurrent.ListenableFuture;
import com.google.mlkit.vision.barcode.*;
import com.google.mlkit.vision.barcode.common.Barcode;
import com.google.mlkit.vision.common.InputImage;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import android.webkit.*;
import android.net.Uri;
import androidx.webkit.WebViewAssetLoader;
import androidx.webkit.WebViewCompat;
import androidx.webkit.WebViewFeature;
import org.json.JSONObject;
import java.util.ArrayList;
import java.util.Collections;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class QrCameraService extends LifecycleService {
    public static final String EVENT = "com.grafiplot.trabajosqr.SCANNER_EVENT";
    public static volatile boolean running = false;
    public static volatile boolean front = false;
    public static volatile boolean torch = false;
    private static final String CHANNEL = "grafiplot_camera";
    private static final String ALERT_CHANNEL = "grafiplot_qr_alert";
    private ProcessCameraProvider provider;
    private Camera camera;
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private BarcodeScanner scanner;
    private String lastCode = "";
    private long lastSeen;
    private int generation;
    private WebView publisher;
    private boolean publisherReady;
    private final ArrayList<String> pendingCodes = new ArrayList<>();
    private WindowManager windowManager;
    private LinearLayout overlay;
    private TextView overlayInfo;
    private String overlayCode;
    private String lastDebtText;

    @Override public void onCreate() {
        super.onCreate();
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Escáner de cámara activo", NotificationManager.IMPORTANCE_LOW));
        manager.createNotificationChannel(new NotificationChannel(ALERT_CHANNEL, "QR detectado", NotificationManager.IMPORTANCE_HIGH));
        initPublisher();
        scanner = BarcodeScanning.getClient(new BarcodeScannerOptions.Builder().setBarcodeFormats(Barcode.FORMAT_QR_CODE).build());
    }
    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        super.onStartCommand(intent, flags, startId);
        if (intent == null || "STOP".equals(intent.getAction())) { stopSelf(); return START_NOT_STICKY; }
        if ("REFRESH_DEBT".equals(intent.getAction())) {
            String code=intent.getStringExtra("codigo");
            if (publisherReady && publisher != null && code != null && code.matches("[0-9]{3}"))
                publisher.evaluateJavascript("window.refreshQrDebt && window.refreshQrDebt(" + JSONObject.quote(code) + ")",null);
            return START_NOT_STICKY;
        }
        if ("HIDE_OVERLAY".equals(intent.getAction())) { removeOverlay(); return START_NOT_STICKY; }
        if ("TORCH".equals(intent.getAction())) {
            setTorch(intent.getBooleanExtra("torch", false));
            return START_NOT_STICKY;
        }
        if (checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) { stopSelf(); return START_NOT_STICKY; }
        front = intent.getBooleanExtra("front", false);
        torch = false;
        try {
            Notification notification = ongoing();
            if (Build.VERSION.SDK_INT >= 29) startForeground(101, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_CAMERA);
            else startForeground(101, notification);
            running = true;
            bindCamera();
        } catch (Exception error) { fail(error.getMessage()); }
        return START_NOT_STICKY;
    }
    private PendingIntent openIntent(String code) {
        Intent intent = new Intent(this, MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        if (code != null) intent.putExtra("qr", code);
        return PendingIntent.getActivity(this, code == null ? 1 : 2, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
    private Notification ongoing() {
        PendingIntent stop = PendingIntent.getService(this, 3, new Intent(this, QrCameraService.class).setAction("STOP"), PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        return new Notification.Builder(this, CHANNEL).setSmallIcon(android.R.drawable.ic_menu_camera)
            .setContentTitle("GRAFIPLOT · Cámara activa")
            .setContentText(lastDebtText != null ? lastDebtText : (front ? "Cámara delantera" : "Cámara trasera") + " · Buscando códigos QR")
            .setStyle(new Notification.BigTextStyle().bigText(lastDebtText != null ? lastDebtText : "Acerca un código QR para consultar su deuda."))
            .setVisibility(Notification.VISIBILITY_PRIVATE).setOnlyAlertOnce(true)
            .setOngoing(true).setContentIntent(openIntent(null))
            .addAction(new Notification.Action.Builder(android.R.drawable.ic_menu_close_clear_cancel, "Detener", stop).build()).build();
    }
    private void bindCamera() {
        final int current = ++generation;
        ListenableFuture<ProcessCameraProvider> future = ProcessCameraProvider.getInstance(this);
        future.addListener(() -> {
            if (!running || current != generation) return;
            try {
                provider = future.get();
                provider.unbindAll();
                ImageAnalysis analysis = new ImageAnalysis.Builder()
                    .setTargetResolution(new android.util.Size(1280,720))
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST).build();
                analysis.setAnalyzer(worker, this::analyze);
                CameraSelector selector = front ? CameraSelector.DEFAULT_FRONT_CAMERA : CameraSelector.DEFAULT_BACK_CAMERA;
                if (!provider.hasCamera(selector)) throw new Exception("La cámara seleccionada no está disponible.");
                camera = provider.bindToLifecycle(this, selector, analysis);
                event(null, null);
            } catch (Exception error) { fail(error.getMessage()); }
        }, ContextCompat.getMainExecutor(this));
    }
    @SuppressLint("UnsafeOptInUsageError")
    private void analyze(ImageProxy frame) {
        if (!running || frame.getImage() == null) { frame.close(); return; }
        InputImage image = InputImage.fromMediaImage(frame.getImage(), frame.getImageInfo().getRotationDegrees());
        scanner.process(image).addOnSuccessListener(codes -> {
            for (Barcode barcode : codes) {
                String value = barcode.getRawValue();
                if (value == null || value.length() > 512) continue;
                long now = SystemClock.elapsedRealtime();
                boolean repeated = value.equals(lastCode) && now - lastSeen < 2000;
                lastCode = value; lastSeen = now;
                if (!repeated && running) {
                    publishCode(value);
                    event(value, null);
                    if (!MainActivity.visible) showAndOpen(value);
                    getSystemService(NotificationManager.class).notify(102,
                        new Notification.Builder(this, ALERT_CHANNEL)
                            .setSmallIcon(android.R.drawable.ic_menu_camera)
                            .setContentTitle("QR detectado: " + value)
                            .setContentText("Toca para abrir la ficha en GRAFIPLOT")
                            .setAutoCancel(true).setContentIntent(openIntent(value)).build());
                }
                break;
            }
        }).addOnCompleteListener(task -> frame.close());
    }
    @SuppressLint("SetJavaScriptEnabled")
    private void initPublisher() {
        publisher = new WebView(this);
        publisher.getSettings().setJavaScriptEnabled(true);
        publisher.getSettings().setDomStorageEnabled(true);
        publisher.getSettings().setAllowFileAccess(false);
        publisher.getSettings().setAllowContentAccess(false);
        WebViewAssetLoader loader = new WebViewAssetLoader.Builder().addPathHandler("/assets/",new WebViewAssetLoader.AssetsPathHandler(this)).build();
        publisher.setWebViewClient(new WebViewClient() {
            @Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest r) { return loader.shouldInterceptRequest(r.getUrl()); }
            @Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r) { return true; }
            @Override public void onPageFinished(WebView v,String url) {
                publisherReady=true;
                for (String code:new ArrayList<>(pendingCodes)) publishCode(code);
                pendingCodes.clear();
            }
        });
        if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_LISTENER)) {
            WebViewCompat.addWebMessageListener(publisher,"GrafiplotService",Collections.singleton("https://appassets.androidplatform.net"),
                (view,message,origin,mainFrame,reply) -> {
                    if (!mainFrame) return;
                    try {
                        JSONObject data=new JSONObject(message.getData());
                        if (running) notifyDebt(data);
                        if (overlayInfo!=null && data.optString("codigo").equals(overlayCode)) {
                            String info=data.optString("error");
                            if (info.isEmpty()) {
                                info=data.optBoolean("encontrado") ? data.optString("nombre") + "\n" + data.optString("descripcion") + "\nTotal S/ " + data.optString("total") + " · A cuenta S/ " + data.optString("pago") : "Código libre. Completa los datos en la PC o en la aplicación.";
                                info += "\nQR enviado a la PC.";
                            }
                            overlayInfo.setText(info);
                        }
                    } catch(Exception ignored) {}
                });
        }
        publisher.loadUrl("https://appassets.androidplatform.net/assets/service-bridge.html");
    }
    private void notifyDebt(JSONObject data) {
        String code = data.optString("codigo");
        if (!code.matches("[0-9]{3}")) return;
        String title, detail;
        if (!data.optString("error").isEmpty()) {
            title = "QR " + code + " · Deuda no disponible";
            detail = data.optString("error");
        } else if (!data.optBoolean("encontrado")) {
            title = "QR " + code + " · Sin cuenta asignada";
            detail = "Completa los datos de este código en la PC.";
        } else {
            double total = data.optDouble("total",0), paid = data.optDouble("pago",0);
            if (!Double.isFinite(total)) total=0;
            if (!Double.isFinite(paid)) paid=0;
            double debt = Math.max(0, Math.round((total-paid)*100)/100.0);
            String debtLabel = debt > 0 ? "Debe S/ " + money(debt) : "Sin deuda · S/ 0.00";
            title = "QR " + code + " · " + debtLabel;
            String name = data.optString("nombre","Cliente");
            detail = name + "\nTotal: S/ " + money(total) + " · A cuenta: S/ " + money(paid) +
                "\n" + debtLabel + "\nToca para abrir la ficha.";
        }
        lastDebtText = title + "\n" + detail;
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.notify(102,new Notification.Builder(this,ALERT_CHANNEL)
            .setSmallIcon(android.R.drawable.ic_menu_camera)
            .setContentTitle(title).setContentText(detail)
            .setStyle(new Notification.BigTextStyle().bigText(detail))
            .setContentIntent(openIntent(code)).setAutoCancel(true)
            .setOnlyAlertOnce(true).setVisibility(Notification.VISIBILITY_PRIVATE).build());
        manager.notify(101,ongoing());
    }
    private static String money(double value) {
        return String.format(java.util.Locale.US,"%.2f",value);
    }
    private void publishCode(String code) {
        if (!publisherReady) { if(pendingCodes.size()<10) pendingCodes.add(code); return; }
        publisher.evaluateJavascript("window.enqueueQr && window.enqueueQr(" + JSONObject.quote(code) + ")",null);
    }
    private void removeOverlay() {
        if (overlay!=null && windowManager!=null) {
            try {windowManager.removeView(overlay);} catch(Exception ignored) {}
        }
        overlay=null;overlayInfo=null;overlayCode=null;
    }
    private void showAndOpen(String code) {
        if (!Settings.canDrawOverlays(this) || !getSharedPreferences("grafiplot_camera",MODE_PRIVATE).getBoolean("auto_open",false)) return;
        removeOverlay();
        try {
            windowManager=(WindowManager)getSystemService(WINDOW_SERVICE);
            overlay=new LinearLayout(this);overlay.setOrientation(LinearLayout.VERTICAL);
            int pad=(int)(16*getResources().getDisplayMetrics().density);
            overlay.setPadding(pad,pad,pad,pad);overlay.setBackgroundColor(Color.WHITE);
            TextView title=new TextView(this);title.setText("GRAFIPLOT · QR " + code);title.setTextColor(Color.rgb(180,20,30));title.setTextSize(22);
            overlay.addView(title);
            overlayInfo=new TextView(this);overlayInfo.setText("Abriendo ficha y enviando QR a la PC…");overlayInfo.setTextColor(Color.BLACK);overlayInfo.setTextSize(16);overlay.addView(overlayInfo);
            overlayCode=code;
            Button open=new Button(this);open.setText("Abrir ficha");open.setOnClickListener(v -> launchApp(code));overlay.addView(open);
            Button close=new Button(this);close.setText("Cerrar cuadro");close.setOnClickListener(v -> removeOverlay());overlay.addView(close);
            WindowManager.LayoutParams params=new WindowManager.LayoutParams(
                (int)(getResources().getDisplayMetrics().widthPixels*0.9),WindowManager.LayoutParams.WRAP_CONTENT,
                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE,PixelFormat.TRANSLUCENT);
            params.gravity=Gravity.TOP|Gravity.CENTER_HORIZONTAL;params.y=pad*4;
            windowManager.addView(overlay,params);
            overlay.postDelayed(() -> launchApp(code),200);
        } catch(Exception error) {removeOverlay();}
    }
    private void launchApp(String code) {
        try {
            Intent open=new Intent(this,MainActivity.class).putExtra("qr",code)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK|Intent.FLAG_ACTIVITY_CLEAR_TOP|Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(open);
        } catch(Exception ignored) {}
    }
    private void setTorch(boolean enable) {
        if (camera == null || !camera.getCameraInfo().hasFlashUnit()) {
            torch = false; event(null, "Esta cámara no dispone de flash."); return;
        }
        camera.getCameraControl().enableTorch(enable).addListener(() -> { torch = enable; event(null,null); }, ContextCompat.getMainExecutor(this));
    }
    private void event(String code, String error) {
        Intent event = new Intent(EVENT).setPackage(getPackageName())
            .putExtra("running", running).putExtra("front", front).putExtra("torch", torch);
        if (code != null) event.putExtra("qr", code);
        if (error != null) event.putExtra("error", error);
        sendBroadcast(event);
    }
    private void fail(String message) {
        running = false; event(null, message == null ? "No se pudo iniciar la cámara." : message); stopSelf();
    }
    @Override public void onDestroy() {
        running = false; torch = false; generation++;
        removeOverlay();
        if (publisher!=null) {publisher.destroy();publisher=null;}
        if (provider != null) provider.unbindAll();
        if (scanner != null) scanner.close();
        worker.shutdown();
        event(null,null);
        stopForeground(true);
        super.onDestroy();
    }
}
