# Project Overview
- Unity 2022.3.62f3 dual-platform project: WebGL (Toss) + Android (Google Play)
- Single codebase with platform-specific branches managed via `merge` branch

# Game Core Logic Rules (DO NOT MODIFY)
- Level-based fixed assets
- Game over detection: Overflow-based only (animal must fall completely outside container), NOT deadline crossing
- Scoreboard data: do not use 'dessert name', 'toss score', or 'notes' columns

# Coding Style & Environment
- WebView communication: Always consider async handling when writing Unity-JS bridge code
- Platform conditionals: Use `#if UNITY_WEBGL` / `#if UNITY_ANDROID` for platform-specific code paths
- Android wrapper: `android-wrapper/` contains the Android WebView shell (Gradle project)
- WebGL template: `Assets/WebGLTemplates/AnimalPop/` for Toss deployment

# Build Targets
- **Toss (WebGL):** `build-ait.sh` for .ait package generation
- **Android:** `android-wrapper/build_apk.sh` or `android-wrapper/build_aab.sh`
