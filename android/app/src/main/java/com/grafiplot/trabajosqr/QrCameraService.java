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

    @Override public void onCreate() {
        super.onCreate();
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Escáner de cámara activo", NotificationManager.IMPORTANCE_LOW));
        manager.createNotificationChannel(new NotificationChannel(ALERT_CHANNEL, "QR detectado", NotificationManager.IMPORTANCE_HIGH));
        scanner = BarcodeScanning.getClient(new BarcodeScannerOptions.Builder().setBarcodeFormats(Barcode.FORMAT_QR_CODE).build());
    }
    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        super.onStartCommand(intent, flags, startId);
        if (intent == null || "STOP".equals(intent.getAction())) { stopSelf(); return START_NOT_STICKY; }
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
            .setContentText((front ? "Cámara delantera" : "Cámara trasera") + " · Buscando códigos QR")
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
                    event(value, null);
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
        if (provider != null) provider.unbindAll();
        if (scanner != null) scanner.close();
        worker.shutdown();
        event(null,null);
        stopForeground(true);
        super.onDestroy();
    }
}
