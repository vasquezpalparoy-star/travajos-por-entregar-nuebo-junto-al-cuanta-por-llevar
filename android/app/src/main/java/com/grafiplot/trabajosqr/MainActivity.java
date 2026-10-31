package com.grafiplot.trabajosqr;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.view.*;
import android.webkit.*;
import android.widget.Toast;
import androidx.webkit.WebViewAssetLoader;
import androidx.webkit.WebViewCompat;
import androidx.webkit.WebViewFeature;
import android.util.Base64;
import org.json.JSONObject;
import java.util.Collections;
import java.io.OutputStream;

public class MainActivity extends Activity {
    private WebView web;
    private PermissionRequest pendingCamera;
    public static volatile boolean visible = false;
    private boolean foreground;
    private boolean startRequested;
    private boolean backgroundRequested;
    private boolean selectedFront;
    private String pendingQr;
    private final BroadcastReceiver scannerReceiver = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) {
            backgroundRequested = intent.getBooleanExtra("running", false);
            syncScannerState(intent.getStringExtra("error"));
            String code = intent.getStringExtra("qr");
            if (foreground && code != null) deliverQr(code);
        }
    };
    private byte[] pendingPdf;
    private boolean openPdfAfterSave;
    private static final String HOST = "appassets.androidplatform.net";

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        web = new WebView(this);
        web.setBackgroundColor(Color.WHITE);
        setContentView(web);
        web.setOnApplyWindowInsetsListener((view, insets) -> {
            view.setPadding(insets.getSystemWindowInsetLeft(), insets.getSystemWindowInsetTop(), insets.getSystemWindowInsetRight(), insets.getSystemWindowInsetBottom());
            return insets;
        });
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setUserAgentString(settings.getUserAgentString() + " GrafiplotAndroid/1.0");
        final WebViewAssetLoader assets = new WebViewAssetLoader.Builder()
            .addPathHandler("/assets/", new WebViewAssetLoader.AssetsPathHandler(this)).build();
        if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_LISTENER)) {
            WebViewCompat.addWebMessageListener(web, "GrafiplotFiles",
                Collections.singleton("https://" + HOST),
                (view, message, sourceOrigin, isMainFrame, reply) -> {
                    if (!isMainFrame || !foreground || !HOST.equals(sourceOrigin.getHost())) return;
                    try {
                        if (pendingPdf != null) throw new Exception("Termina de guardar el PDF anterior.");
                        String raw = message.getData();
                        if (raw == null || raw.length() > 20000000) throw new Exception("El archivo es demasiado grande.");
                        JSONObject data = new JSONObject(raw);
                        if (!"application/pdf".equals(data.getString("mime"))) throw new Exception("Formato no admitido.");
                        byte[] bytes = Base64.decode(data.getString("base64"), Base64.DEFAULT);
                        if (bytes.length < 5 || bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F') throw new Exception("PDF inválido.");
                        pendingPdf = bytes;
                        openPdfAfterSave = "print".equals(data.optString("action"));
                        String name = data.optString("name", "Recibo_Grafiplot.pdf").replaceAll("[^a-zA-Z0-9_.-]", "_");
                        Intent save = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                        save.addCategory(Intent.CATEGORY_OPENABLE);
                        save.setType("application/pdf");
                        save.putExtra(Intent.EXTRA_TITLE, name);
                        startActivityForResult(save, 30);
                    } catch (Exception error) {
                        pendingPdf = null;
                        Toast.makeText(this, "No se pudo preparar el PDF: " + error.getMessage(), Toast.LENGTH_LONG).show();
                    }
                });
        }
        if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_LISTENER)) {
            WebViewCompat.addWebMessageListener(web, "GrafiplotScanner", Collections.singleton("https://" + HOST),
                (view, message, origin, mainFrame, reply) -> {
                    if (!mainFrame || !foreground || !HOST.equals(origin.getHost())) return;
                    try {
                        JSONObject data = new JSONObject(message.getData());
                        String action = data.getString("action");
                        if ("overlay".equals(action)) {
                            boolean enable = data.optBoolean("enable", true);
                            getSharedPreferences("grafiplot_camera",MODE_PRIVATE).edit().putBoolean("auto_open",enable).apply();
                            if (enable && !android.provider.Settings.canDrawOverlays(this)) {
                                startActivity(new Intent(android.provider.Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:" + getPackageName())));
                            } else syncOverlayState();
                        } else if ("stop".equals(action)) {
                            startRequested = false; backgroundRequested = false;
                            stopService(new Intent(this, QrCameraService.class));
                            syncScannerState(null);
                        } else if ("start".equals(action)) {
                            selectedFront = data.optBoolean("front", false);
                            startRequested = true; backgroundRequested = true;
                            if (checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED ||
                                (android.os.Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)) {
                                if (android.os.Build.VERSION.SDK_INT >= 33) requestPermissions(new String[]{Manifest.permission.CAMERA, Manifest.permission.POST_NOTIFICATIONS}, 21);
                                else requestPermissions(new String[]{Manifest.permission.CAMERA}, 21);
                            } else startBackgroundCamera();
                        } else if ("torch".equals(action) && QrCameraService.running) {
                            startService(new Intent(this, QrCameraService.class).setAction("TORCH").putExtra("torch", data.optBoolean("torch", false)));
                        }
                    } catch (Exception error) { backgroundRequested = false; syncScannerState(error.getMessage()); }
                });
        }
        web.setWebViewClient(new WebViewClient() {
            @Override public void onPageFinished(WebView v, String url) {
                syncScannerState(null);
                syncOverlayState();
                if (pendingQr != null) { deliverQr(pendingQr); pendingQr = null; }
            }
            @Override public WebResourceResponse shouldInterceptRequest(WebView v, WebResourceRequest request) {
                return assets.shouldInterceptRequest(request.getUrl());
            }
            @Override public boolean shouldOverrideUrlLoading(WebView v, WebResourceRequest request) {
                Uri uri = request.getUrl();
                if ("https".equals(uri.getScheme()) && HOST.equals(uri.getHost())) return false;
                if ("https".equals(uri.getScheme()) || "mailto".equals(uri.getScheme()) || "tel".equals(uri.getScheme())) {
                    try { startActivity(new Intent(Intent.ACTION_VIEW, uri)); }
                    catch (Exception e) { Toast.makeText(MainActivity.this, "No se encontró una aplicación para abrir el enlace.", Toast.LENGTH_LONG).show(); }
                }
                return true;
            }
        });
        web.setWebChromeClient(new WebChromeClient() {
            @Override public void onPermissionRequest(PermissionRequest request) {
                runOnUiThread(() -> {
                    boolean camera = false;
                    for (String resource : request.getResources())
                        if (PermissionRequest.RESOURCE_VIDEO_CAPTURE.equals(resource)) camera = true;
                    if (!foreground || !camera || !HOST.equals(request.getOrigin().getHost()) || !"https".equals(request.getOrigin().getScheme())) {
                        request.deny(); return;
                    }
                    if (checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
                        request.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});
                    } else {
                        if (pendingCamera != null) pendingCamera.deny();
                        pendingCamera = request;
                        requestPermissions(new String[]{Manifest.permission.CAMERA}, 20);
                    }
                });
            }
            @Override public void onPermissionRequestCanceled(PermissionRequest request) {
                if (pendingCamera == request) pendingCamera = null;
            }
            @Override public boolean onJsAlert(WebView view, String url, String message, JsResult result) {
                new AlertDialog.Builder(MainActivity.this).setMessage(message).setPositiveButton("Aceptar", (d, w) -> result.confirm()).setOnCancelListener(d -> result.cancel()).show();
                return true;
            }
        });
        pendingQr = getIntent().getStringExtra("qr");
        web.loadUrl("https://" + HOST + "/assets/index.html");
    }
    private void startBackgroundCamera() {
        if (!foreground || !startRequested) return;
        if (checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) return;
        if (android.os.Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return;
        startRequested = false;
        try {
            startForegroundService(new Intent(this, QrCameraService.class).putExtra("front", selectedFront));
            backgroundRequested = true;
        } catch (Exception error) { backgroundRequested = false; syncScannerState(error.getMessage()); }
    }
    private void syncScannerState(String message) {
        if (web == null) return;
        boolean active = QrCameraService.running || backgroundRequested;
        web.evaluateJavascript("window.grafiplotBackgroundState && window.grafiplotBackgroundState(" + active + "," +
            QrCameraService.torch + "," + JSONObject.quote(message == null ? "" : message) + ")", null);
    }
    private void syncOverlayState() {
        if (web == null) return;
        boolean allowed = android.provider.Settings.canDrawOverlays(this) && getSharedPreferences("grafiplot_camera",MODE_PRIVATE).getBoolean("auto_open",false);
        web.evaluateJavascript("window.grafiplotOverlayState && window.grafiplotOverlayState(" + allowed + ")",null);
    }
    private void deliverQr(String code) {
        if (web == null) { pendingQr = code; return; }
        web.evaluateJavascript("window.grafiplotNativeScan && window.grafiplotNativeScan(" + JSONObject.quote(code) + ",false)", null);
    }
    @Override protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent); setIntent(intent);
        String code = intent.getStringExtra("qr");
        if (code != null) deliverQr(code);
    }
    @Override protected void onStart() {
        super.onStart();
        androidx.core.content.ContextCompat.registerReceiver(this, scannerReceiver, new IntentFilter(QrCameraService.EVENT), androidx.core.content.ContextCompat.RECEIVER_NOT_EXPORTED);
    }
    @Override protected void onStop() {
        unregisterReceiver(scannerReceiver);
        super.onStop();
    }

    @Override protected void onActivityResult(int code, int result, Intent data) {
        super.onActivityResult(code, result, data);
        if (code != 30) return;
        final byte[] bytes = pendingPdf;
        final boolean openAfterSave = openPdfAfterSave;
        pendingPdf = null;
        if (result != RESULT_OK || data == null || data.getData() == null || bytes == null) return;
        final Uri uri = data.getData();
        new Thread(() -> {
            try {
                try (OutputStream out = getContentResolver().openOutputStream(uri, "wt")) {
                    if (out == null) throw new Exception("No se pudo abrir el destino.");
                    out.write(bytes);
                }
                runOnUiThread(() -> {
                    Toast.makeText(this, "PDF guardado correctamente.", Toast.LENGTH_LONG).show();
                    if (openAfterSave) {
                        try {
                            Intent open = new Intent(Intent.ACTION_VIEW);
                            open.setDataAndType(uri, "application/pdf");
                            open.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                            startActivity(Intent.createChooser(open, "Abrir recibo para imprimir"));
                        } catch (Exception error) {
                            Toast.makeText(this, "Abre el PDF guardado desde Archivos para imprimir.", Toast.LENGTH_LONG).show();
                        }
                    }
                });
            } catch (Exception error) {
                runOnUiThread(() -> Toast.makeText(this, "Error al guardar: " + error.getMessage(), Toast.LENGTH_LONG).show());
            }
        }).start();
    }

    @Override public void onRequestPermissionsResult(int code, String[] permissions, int[] results) {
        super.onRequestPermissionsResult(code, permissions, results);
        if (code == 21) {
            boolean granted = results.length > 0;
            for (int result : results) if (result != PackageManager.PERMISSION_GRANTED) granted = false;
            if (granted) web.postDelayed(this::startBackgroundCamera, 250);
            else {
                startRequested = false; backgroundRequested = false;
                syncScannerState("Se necesitan permisos de cámara y notificaciones para el modo en segundo plano.");
            }
        }
        if (code == 20 && pendingCamera != null) {
            if (!isFinishing() && results.length > 0 && results[0] == PackageManager.PERMISSION_GRANTED)
                pendingCamera.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});
            else {
                pendingCamera.deny();
                Toast.makeText(this, "Activa el permiso Cámara en Ajustes para escanear.", Toast.LENGTH_LONG).show();
            }
            pendingCamera = null;
        }
    }
    @Override protected void onResume() {
        super.onResume();
        foreground = true;
        visible = true;
        if (QrCameraService.running) startService(new Intent(this,QrCameraService.class).setAction("HIDE_OVERLAY"));
        syncOverlayState();
        if (!startRequested) backgroundRequested = QrCameraService.running;
        startBackgroundCamera();
        syncScannerState(null);
        if (web != null) {
            web.onResume();
            web.evaluateJavascript("window.grafiplotSetForeground && window.grafiplotSetForeground(true)", null);
        }
    }
    @Override protected void onPause() {
        foreground = false;
        visible = false;
        if (web != null) {
            web.evaluateJavascript("window.grafiplotSetForeground && window.grafiplotSetForeground(false)", null);
            web.onPause();
        }
        super.onPause();
    }
    @Override public void onBackPressed() {
        new AlertDialog.Builder(this).setMessage("¿Salir? Si activaste segundo plano, la cámara seguirá activa hasta pulsar Detener en la notificación.")
            .setPositiveButton("Salir", (d,w) -> finish()).setNegativeButton("Continuar", null).show();
    }
    @Override protected void onDestroy() {
        if (web != null) { web.destroy(); web = null; }
        super.onDestroy();
    }
}
