# ── Animal Pop – ProGuard Rules ──────────────────────────────────

# WebView JavaScript Interface (삭제하면 런타임 크래시)
-keepclassmembers class com.animalpop.app.MainActivity$AndroidBridge {
    @android.webkit.JavascriptInterface <methods>;
}
-keep class com.animalpop.app.** { *; }

# Google Play Billing Library
-keep class com.android.billingclient.** { *; }
-keep interface com.android.billingclient.** { *; }
-dontwarn com.android.billingclient.**

# Google Mobile Ads (AdMob)
-keep class com.google.android.gms.ads.** { *; }
-keep class com.google.android.gms.common.** { *; }
-dontwarn com.google.android.gms.**

# WebView 관련 (크래시 방지)
-keepclassmembers class * extends android.webkit.WebViewClient {
    public void *(android.webkit.WebView, java.lang.String, android.graphics.Bitmap);
    public boolean *(android.webkit.WebView, java.lang.String);
}
-keepclassmembers class * extends android.webkit.WebChromeClient {
    public void *(android.webkit.WebView, java.lang.String);
}

# 일반 Android 필수
-keepattributes Signature
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
