package com.cldery.mhop;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.View;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.ProgressBar;
import android.widget.Toast;

/**
 * 心光 MHOP 站点壳：一个被锁定在 cldery.com 生态的全屏 WebView。
 * - 允许 scheme=https，且 host 属于 *.cldery.com 子域（mhop 主站、auth 统一身份证、api 等）；
 * - 站外域一律拦截，不唤起外部浏览器；
 * - 支持 H5 文件/图片选择上传；
 * - 物理返回键优先网页后退；
 * - 主框架加载失败显示原生重试页。
 */
public class MainActivity extends Activity {

    private static final String ALLOWED_TLD = "cldery.com";
    private static final String SITE_URL = "https://mhop.cldery.com/";
    private static final int FILE_CHOOSER_REQUEST = 51001;

    private WebView web;
    private ProgressBar progress;
    private View errorOverlay;
    private ValueCallback<Uri[]> filePathCallback;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        web = findViewById(R.id.web);
        progress = findViewById(R.id.progress);
        errorOverlay = findViewById(R.id.error_overlay);
        Button retryButton = findViewById(R.id.retry_button);
        retryButton.setOnClickListener(v -> loadSite());

        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        // 仅 HTTPS 站点，禁止任何 http 混合内容
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(true);
        settings.setMediaPlaybackRequiresUserGesture(false);

        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                // 返回 true = WebView 不加载该 URL（拦截）。仅放行同源 https。
                return !isSameSite(request.getUrl());
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                errorOverlay.setVisibility(View.GONE);
            }

            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) {
                    errorOverlay.setVisibility(View.VISIBLE);
                }
            }
        });

        web.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onProgressChanged(WebView view, int newProgress) {
                progress.setProgress(newProgress);
                progress.setVisibility(newProgress >= 100 ? View.GONE : View.VISIBLE);
            }

            // H5 <input type="file"> 上传
            @Override
            public boolean onShowFileChooser(WebView view,
                                             ValueCallback<Uri[]> callback,
                                             FileChooserParams fileChooserParams) {
                if (filePathCallback != null) {
                    filePathCallback.onReceiveValue(null);
                }
                filePathCallback = callback;

                Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("image/*");
                intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{
                        "image/jpeg", "image/png", "image/webp", "image/gif"
                });

                try {
                    startActivityForResult(Intent.createChooser(intent, getString(R.string.choose_image)),
                            FILE_CHOOSER_REQUEST);
                } catch (Exception e) {
                    filePathCallback = null;
                    Toast.makeText(MainActivity.this, R.string.no_file_picker, Toast.LENGTH_SHORT).show();
                    return false;
                }
                return true;
            }
        });

        if (savedInstanceState == null) {
            web.loadUrl(SITE_URL);
        } else {
            web.restoreState(savedInstanceState);
        }
    }

    /**
     * 放行规则：scheme 必须 https，且 host 等于 *.cldery.com 的任意子域（大小写不敏感）。
     * 例：mhop.cldery.com ✅ / auth.cldery.com ✅ / api.cldery.com ✅ / cldery.com 本身 ✅ / google.com ❌。
     */
    private static boolean isSameSite(Uri uri) {
        if (uri == null || !"https".equals(uri.getScheme())) return false;
        String host = uri.getHost();
        if (host == null) return false;
        host = host.toLowerCase();
        return host.equals(ALLOWED_TLD) || host.endsWith("." + ALLOWED_TLD);
    }

    private void loadSite() {
        errorOverlay.setVisibility(View.GONE);
        web.loadUrl(SITE_URL);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == FILE_CHOOSER_REQUEST) {
            if (filePathCallback == null) {
                return;
            }
            Uri[] results = null;
            if (resultCode == RESULT_OK && data != null && data.getData() != null) {
                results = new Uri[]{data.getData()};
            }
            filePathCallback.onReceiveValue(results);
            filePathCallback = null;
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        if (keyCode == KeyEvent.KEYCODE_BACK && web.canGoBack()) {
            web.goBack();
            return true;
        }
        return super.onKeyDown(keyCode, event);
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        web.saveState(outState);
    }

    @Override
    protected void onDestroy() {
        if (filePathCallback != null) {
            filePathCallback.onReceiveValue(null);
            filePathCallback = null;
        }
        web.destroy();
        super.onDestroy();
    }
}
