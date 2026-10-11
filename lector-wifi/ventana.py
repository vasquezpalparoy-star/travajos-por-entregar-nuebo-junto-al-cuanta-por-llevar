"""Temporary local IPC for an ordinary topmost Windows form."""
import json
import os
import secrets
import subprocess
import tempfile
from pathlib import Path

class Ventana:
    def __init__(self):
        self.directory = tempfile.TemporaryDirectory(prefix='GrafiplotQR-')
        self.path = Path(self.directory.name) / 'ventana.json'
        self.closed_path = Path(str(self.path) + '.closed')
        self.event = None
        self.process = None
        self.write({'visible': False})
        if os.name == 'nt':
            self.process = subprocess.Popen([
                'powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                '-STA', '-File', str(Path(__file__).with_name('VENTANA_QR.ps1')),
                '-DataPath', str(self.path)
            ], creationflags=subprocess.CREATE_NO_WINDOW)
    def write(self, data):
        data['revision'] = secrets.token_hex(8)
        staging = self.path.with_suffix('.tmp')
        staging.write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
        os.replace(staging,self.path)
    def show(self, event, text):
        self.event = event
        self.closed_path.unlink(missing_ok=True)
        self.write({'visible':True,'evento':event,'text':text})
    def closed(self, event):
        try:return self.closed_path.read_text(encoding='utf-8') == event
        except OSError:return False
    def hide(self, event):
        if event == self.event:
            self.write({'visible':False})
            self.event = None
    def close(self):
        self.path.unlink(missing_ok=True)
        if self.process:
            try:self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:self.process.terminate()
        self.directory.cleanup()
