#!/usr/bin/env python3
from pathlib import Path
import urllib.request
import hashlib
VENDORS = {
 'scanner.js':'https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js',
 'jspdf.js':'https://cdnjs.cloudflare.com/ajax/libs/jspdf/2.5.1/jspdf.umd.min.js',
 'qrcode.js':'https://cdnjs.cloudflare.com/ajax/libs/qrcode/1.5.1/qrcode.min.js',
 'barcode.js':'https://cdn.jsdelivr.net/npm/jsbarcode@3.11.5/dist/JsBarcode.all.min.js',
 'xlsx.js':'https://cdnjs.cloudflare.com/ajax/libs/xlsx/0.18.5/xlsx.full.min.js',
 'supabase.js':'https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2.57.4/dist/umd/supabase.js'
}
root=Path(__file__).parent/'app/src/main/assets/web/vendor'
checksums=__import__('json').loads((Path(__file__).parent/'vendor-checksums.json').read_text())
for name,url in VENDORS.items():
 data=urllib.request.urlopen(url,timeout=60).read()
 if hashlib.sha256(data).hexdigest()!=checksums[name]: raise ValueError('Unexpected checksum: '+name)
 (root/name).write_bytes(data)
 print(name,'OK')
