"""Puente local APK -> navegador Grafiplot. No guarda fichas ni credenciales Supabase."""
import argparse
import hmac
import json
import os
import re
import secrets
import socket
import sys
import sqlite3
import threading
import time
import webbrowser
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
# Embedded Python isolates sys.path; include this trusted application's folder.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from ventana import Ventana

ROOT = Path(__file__).resolve().parents[1]

class Lecturas:
    def __init__(self, carpeta):
        carpeta = Path(carpeta); carpeta.mkdir(parents=True, exist_ok=True)
        self.ruta = carpeta / 'lecturas.sqlite'
        with self.conectar() as db:
            db.executescript('''CREATE TABLE IF NOT EXISTS ajustes(clave TEXT PRIMARY KEY,valor TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS lecturas(evento TEXT PRIMARY KEY,codigo TEXT NOT NULL,fecha REAL NOT NULL,visto INTEGER NOT NULL DEFAULT 0);
                CREATE INDEX IF NOT EXISTS lecturas_pendientes ON lecturas(visto,fecha);''')
            db.execute('INSERT OR IGNORE INTO ajustes VALUES(?,?)', ('token', secrets.token_hex(16)))
            self.token = db.execute("SELECT valor FROM ajustes WHERE clave='token'").fetchone()[0]
    def conectar(self): return sqlite3.connect(self.ruta, timeout=10)
    def guardar(self, evento, codigo):
        with self.conectar() as db:
            prior = db.execute('SELECT codigo FROM lecturas WHERE evento=?', (evento,)).fetchone()
            if prior:
                if prior[0] != codigo: raise ValueError('Evento reutilizado con otro código')
                return False
            count = db.execute('SELECT COUNT(*) FROM lecturas WHERE visto=0').fetchone()[0]
            if count >= 1000: raise OverflowError('Hay 1000 lecturas pendientes; revísalas en la PC')
            db.execute('INSERT INTO lecturas(evento,codigo,fecha) VALUES(?,?,?)', (evento,codigo,time.time()))
            return True
    def pendientes(self):
        with self.conectar() as db:
            rows = db.execute('SELECT evento,codigo,fecha FROM lecturas WHERE visto=0 ORDER BY fecha,rowid LIMIT 20').fetchall()
            count = db.execute('SELECT COUNT(*) FROM lecturas WHERE visto=0').fetchone()[0]
        return [{'evento':e,'codigo':c,'fecha':f} for e,c,f in rows], count
    def ultima(self):
        with self.conectar() as db:
            row=db.execute('SELECT evento,codigo,fecha FROM lecturas WHERE visto=0 ORDER BY fecha DESC,rowid DESC LIMIT 1').fetchone()
        return dict(zip(('evento','codigo','fecha'),row)) if row else None
    def confirmar_anteriores(self, evento):
        with self.conectar() as db:
            row=db.execute('SELECT rowid,fecha FROM lecturas WHERE evento=? AND visto=0',(evento,)).fetchone()
            if not row:raise ValueError('Lectura no pendiente')
            db.execute('UPDATE lecturas SET visto=1 WHERE visto=0 AND (fecha<? OR (fecha=? AND rowid<?))',(row[1],row[1],row[0]))
    def confirmar(self, evento):
        with self.conectar() as db: db.execute('UPDATE lecturas SET visto=1 WHERE evento=?',(evento,))

class Puente(ThreadingHTTPServer):
    daemon_threads = True
    def __init__(self, datos, host='0.0.0.0', port=8765, root=ROOT, popup=None):
        self.popup=popup; self.popup_lock=threading.Lock(); self.changes=threading.Condition(); self.generation=0;
        self.datos=datos; self.browser_token=secrets.token_hex(32); self.root=Path(root)
        super().__init__((host,port), Handler)
    def local_host(self): return f'127.0.0.1:{self.server_port}'
    def ip_wifi(self):
        try:
            with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sock:
                sock.connect(('192.0.2.1',1)); return sock.getsockname()[0]
        except OSError: return 'IP-DE-TU-PC'

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def local(self):
        return self.client_address[0] in ('127.0.0.1','::1') and self.headers.get('Host','') in (self.server.local_host(),f'localhost:{self.server.server_port}')
    def browser(self, mutation=False):
        if not self.local(): return False
        if self.headers.get('Sec-Fetch-Site','same-origin') not in ('same-origin','none'): return False
        if mutation and self.headers.get('Origin') != 'http://' + self.headers.get('Host',''): return False
        cookie=SimpleCookie()
        try: cookie.load(self.headers.get('Cookie',''))
        except Exception: return False
        token=cookie.get('grafiplot_bridge')
        return token is not None and hmac.compare_digest(token.value,self.server.browser_token)
    def device(self):
        return hmac.compare_digest(self.headers.get('Authorization',''), 'Bearer '+self.server.datos.token)
    def response(self, status, obj): self.raw(status,json.dumps(obj,ensure_ascii=False).encode(),'application/json; charset=utf-8')
    def raw(self,status,body,kind,cookie=False):
        self.send_response(status);self.send_header('Content-Type',kind);self.send_header('Content-Length',str(len(body)))
        self.send_header('Cache-Control','no-store');self.send_header('X-Content-Type-Options','nosniff')
        self.send_header('X-Frame-Options','DENY');self.send_header('Referrer-Policy','no-referrer')
        if cookie: self.send_header('Set-Cookie',f'grafiplot_bridge={self.server.browser_token}; HttpOnly; SameSite=Strict; Path=/')
        self.end_headers();self.wfile.write(body)
    def do_GET(self):
        if self.path == '/health':
            if not self.device(): return self.response(401,{'error':'Clave incorrecta'})
            return self.response(200,{'ok':True,'sistema':'Grafiplot'})
        if self.path in ('/','/index.html','/qr-wifi.mjs'):
            if not self.local(): return self.response(403,{'error':'Abre el sistema desde esta PC con 127.0.0.1'})
            filename='qr-wifi.mjs' if self.path=='/qr-wifi.mjs' else 'index.html'
            try: content=(self.server.root / filename).read_bytes()
            except OSError: return self.response(404,{'error':'Falta un archivo del sistema. Extrae todo el ZIP.'})
            return self.raw(200,content,'text/javascript; charset=utf-8' if filename.endswith('.mjs') else 'text/html; charset=utf-8',cookie=True)
        if self.path == '/live':
            if not self.browser(): return self.response(403,{'error':'Acceso local no autorizado'})
            self.send_response(200);self.send_header('Content-Type','text/event-stream');self.send_header('Cache-Control','no-store');self.end_headers()
            try:
                generation=-1
                while True:
                    with self.server.changes:
                        if generation==self.server.generation:self.server.changes.wait(timeout=1)
                        generation=self.server.generation
                    self.wfile.write(b'data: wake\n\n');self.wfile.flush()
            except (OSError,ConnectionError):return
        if self.path in ('/connection','/events','/popup-state'):
            if not self.browser(): return self.response(403,{'error':'Acceso local no autorizado'})
            if self.path=='/connection': return self.response(200,{'url':f'http://{self.server.ip_wifi()}:{self.server.server_port}','token':self.server.datos.token})
            if self.path=='/popup-state':
                popup=self.server.popup
                return self.response(200,{'closed':bool(popup and popup.event and popup.closed(popup.event)),'evento':popup.event if popup else None,'action':popup.action() if popup and hasattr(popup,'action') else None,'editing':bool(popup and hasattr(popup,'editing') and popup.editing())})
            events,count=self.server.datos.pendientes();return self.response(200,{'events':events,'pendientes':count,'latest':self.server.datos.ultima()})
        self.response(404,{'error':'Ruta no encontrada'})
    def do_POST(self):
        self.connection.settimeout(5)
        if self.path == '/scan':
            if not self.device(): return self.response(401,{'error':'Clave incorrecta'})
        elif self.path in ('/ack','/ack-before','/popup','/popup-action-ack'):
            if not self.browser(mutation=True): return self.response(403,{'error':'Acceso local no autorizado'})
        else: return self.response(404,{'error':'Ruta no encontrada'})
        try:
            size=int(self.headers.get('Content-Length','0'))
            if not 0<size<=(32768 if self.path=='/popup' else 4096): return self.response(413,{'error':'Petición demasiado grande'})
            obj=json.loads(self.rfile.read(size))
            if not isinstance(obj,dict): raise ValueError('Lectura inválida')
            evento=obj.get('evento')
            if not isinstance(evento,str) or not 1<=len(evento)<=100: raise ValueError('Evento inválido')
            if self.path=='/popup-action-ack':
                action_id=obj.get('id');message=obj.get('message','')
                if not isinstance(action_id,str) or len(action_id)>100 or not isinstance(message,str) or len(message)>500:raise ValueError('Acción inválida')
                with self.server.popup_lock:
                    popup=self.server.popup
                    if not popup or popup.event!=evento:raise ValueError('Lectura no pendiente')
                    popup.action_ack(action_id,message)
            elif self.path=='/popup':
                text=obj.get('text')
                if not isinstance(text,str) or not 1<=len(text)<=3000:raise ValueError('Detalle inválido')
                details=obj.get('details')
                if details is not None:
                    if not isinstance(details,dict):raise ValueError('Ficha inválida')
                    limits={'codigo':256,'nombre':200,'deuda':32,'descripcion':1200,'ubicacion':100,'telefono':64,'completo':8000}
                    if any(k not in limits or not isinstance(v,str) or len(v)>limits[k] for k,v in details.items()):raise ValueError('Ficha inválida')
                    if details.get('deuda') and not re.fullmatch(r'\d+\.\d{2}',details['deuda']):raise ValueError('Saldo inválido')
                events,_=self.server.datos.pendientes()
                if not any(e['evento']==evento for e in events) and (self.server.datos.ultima() or {}).get('evento')!=evento:raise ValueError('Lectura no pendiente')
                with self.server.popup_lock:
                    if self.server.popup:self.server.popup.show(evento,text,details)
            elif self.path=='/ack-before':
                self.server.datos.confirmar_anteriores(evento)
            elif self.path=='/ack':
                self.server.datos.confirmar(evento)
                with self.server.popup_lock:
                    if self.server.popup:self.server.popup.hide(evento)
            else:
                codigo=obj.get('codigo')
                if not isinstance(codigo,str) or not 1<=len(codigo.strip())<=256: raise ValueError('Código inválido')
                self.server.datos.guardar(evento,codigo.strip())
                with self.server.changes:
                    self.server.generation+=1;self.server.changes.notify_all()
            self.response(200,{'ok':True})
        except (ValueError,TypeError,KeyError) as e:self.response(400,{'error':str(e)})
        except OverflowError as e:self.response(503,{'error':str(e)})
        except (sqlite3.Error,OSError):self.response(503,{'error':'No se pudo guardar; reintentar'})

def main():
    parser=argparse.ArgumentParser(description='Grafiplot: receptor QR por Wi-Fi');parser.add_argument('--port',type=int,default=8765);parser.add_argument('--no-browser',action='store_true');args=parser.parse_args()
    folder=Path(os.environ.get('LOCALAPPDATA',str(Path.home()))) / 'GrafiplotLectorWiFi'
    try: server=Puente(Lecturas(folder),port=args.port)
    except OSError as e: raise SystemExit(f'No se pudo abrir el puerto {args.port}. Cierra el receptor anterior. {e}')
    try: server.popup=Ventana()
    except OSError as e: print('No se pudo abrir ventana Windows:',e)
    url=f'http://{server.local_host()}/'
    print('Grafiplot 1.8 - CUENTAS configurado. Mantén esta ventana abierta.');print('Carpeta del sistema:',ROOT);print('Abre en esta PC:',url)
    print('Inicia sesión con el MISMO proyecto y cuenta Supabase de tu sistema.');print('Detener: Ctrl+C')
    if not args.no_browser: webbrowser.open(url)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally:
        server.server_close()
        if server.popup:server.popup.close()
if __name__=='__main__':main()
