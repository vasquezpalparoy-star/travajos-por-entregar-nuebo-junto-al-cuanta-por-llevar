from pathlib import Path
import qrcode
from qrcode.util import QRData, MODE_8BIT_BYTE
import zxingcpp
from PIL import Image
rows=Path('qr-matrices.txt').read_text().splitlines()
assert len(rows)==500*22
for n in range(500):
    code=rows[n*22]
    actual=[[c=='1' for c in row] for row in rows[n*22+1:n*22+22]]
    qr=qrcode.QRCode(version=1,error_correction=qrcode.constants.ERROR_CORRECT_M,box_size=1,border=0,mask_pattern=0)
    qr.add_data(QRData(code,mode=MODE_8BIT_BYTE),optimize=0)
    qr.make(fit=False)
    assert actual==qr.get_matrix(),code
result=zxingcpp.read_barcode(Image.open('comprobante-qr.png'))
assert result and result.text=='027'
print('OK: 500 QR verificados y recibo QR leido')
