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

public class MainActivity extends Activity {
    private WebView web;
    private PermissionRequest pendingCamera;
    private boolean foreground;
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
        web.setWebViewClient(new WebViewClient() {
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
        web.loadUrl("https://" + HOST + "/assets/index.html");
    }
    @Override public void onRequestPermissionsResult(int code, String[] permissions, int[] results) {
        super.onRequestPermissionsResult(code, permissions, results);
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
        if (web != null) {
            web.onResume();
            web.evaluateJavascript("window.grafiplotSetForeground && window.grafiplotSetForeground(true)", null);
        }
    }
    @Override protected void onPause() {
        foreground = false;
        if (web != null) {
            web.evaluateJavascript("window.grafiplotSetForeground && window.grafiplotSetForeground(false)", null);
            web.onPause();
        }
        super.onPause();
    }
    @Override public void onBackPressed() {
        new AlertDialog.Builder(this).setMessage("¿Salir del escáner?")
            .setPositiveButton("Salir", (d,w) -> finish()).setNegativeButton("Continuar", null).show();
    }
    @Override protected void onDestroy() {
        if (web != null) { web.destroy(); web = null; }
        super.onDestroy();
    }
}
