# FitForge — Play Store (TWA) Build Guide

This folder contains everything needed to publish FitForge on Google Play Store
as a Trusted Web Activity (TWA) — a native Android wrapper around your hosted app.

## Prerequisites

1. FitForge deployed on Render with HTTPS (e.g. `https://fitforge.onrender.com`)
2. [Google Play Developer account](https://play.google.com/console) ($25 one-time)
3. [Node.js 18+](https://nodejs.org/)
4. [Java JDK 17+](https://adoptium.net/)
5. [Android Studio](https://developer.android.com/studio) (for signing)

## Step 1 — Install Bubblewrap

```bash
npm install -g @bubblewrap/cli
```

## Step 2 — Initialize TWA project

Replace `YOUR_RENDER_URL` with your actual Render URL:

```bash
cd android
bubblewrap init --manifest https://YOUR_RENDER_URL/manifest.json
```

When prompted:
- **Package name:** `com.fitforge.app`
- **App name:** FitForge
- **Start URL:** `/Dashboard/Index`
- **Theme color:** `#0d1117`
- **Background color:** `#0d1117`

## Step 3 — Get SHA-256 fingerprint

After generating a signing key:

```bash
keytool -list -v -keystore android.keystore -alias fitforge
```

Copy the **SHA-256** fingerprint and set it on Render:

```
TWA__Sha256Fingerprint=AA:BB:CC:...
```

Verify at: `https://YOUR_RENDER_URL/.well-known/assetlinks.json`

## Step 4 — Build release AAB

```bash
bubblewrap build
```

Output: `app-release-signed.aab` — upload this to Google Play Console.

## Step 5 — Play Store listing

You'll need:
- App icon 512×512 (`wwwroot/icons/icon-512.png`)
- Feature graphic 1024×500
- 2+ phone screenshots
- Privacy policy URL: `https://YOUR_RENDER_URL/Home/Privacy`
- Short description (80 chars)
- Full description

## Render environment variables

```
ConnectionStrings__DefaultConnection=Server=HOST.a.aivencloud.com;Port=PORT;Database=defaultdb;User=avnadmin;Password=XXX;SslMode=Required;
Email__BaseUrl=https://YOUR_RENDER_URL
TWA__Sha256Fingerprint=YOUR_SHA256
```

## Package name

`com.fitforge.app` — do not change after Play Store upload.
