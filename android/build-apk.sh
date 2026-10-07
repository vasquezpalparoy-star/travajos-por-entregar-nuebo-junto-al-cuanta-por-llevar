#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${ANDROID_HOME:?Define ANDROID_HOME con la ruta del SDK Android}"
BT="$ANDROID_HOME/build-tools/35.0.0"
JAR="$ANDROID_HOME/platforms/android-35/android.jar"
mkdir -p build/classes build/dex build/compiled
rm -f build/compiled/*.flat
"$BT/aapt2" compile --dir app/src/main/res -o build/compiled
"$BT/aapt2" link -o build/resources.apk --manifest app/src/main/AndroidManifest.xml -I "$JAR" --java build/generated -A app/src/main/assets build/compiled/*.flat
java com.sun.tools.javac.Main -source 8 -target 8 -classpath "$JAR" -d build/classes $(find app/src/main/java build/generated -name '*.java')
"$BT/d8" --lib "$JAR" --min-api 26 --output build/dex $(find build/classes -name '*.class')
cp build/resources.apk build/unsigned.apk
(cd build/dex && zip -q ../unsigned.apk classes.dex)
"$BT/zipalign" -f -p 4 build/unsigned.apk build/aligned.apk
: "${GRAFIPLOT_KEYSTORE:?Define GRAFIPLOT_KEYSTORE}"
: "${GRAFIPLOT_KEY_PASS:?Define GRAFIPLOT_KEY_PASS}"
"$BT/apksigner" sign --ks "$GRAFIPLOT_KEYSTORE" --ks-key-alias grafiplot --ks-pass env:GRAFIPLOT_KEY_PASS --key-pass env:GRAFIPLOT_KEY_PASS --out build/Grafiplot.apk build/aligned.apk
"$BT/apksigner" verify --verbose build/Grafiplot.apk
