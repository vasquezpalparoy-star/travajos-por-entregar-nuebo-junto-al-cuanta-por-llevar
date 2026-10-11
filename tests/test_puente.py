import sys, tempfile, threading, unittest, json, urllib.request, urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'lector-wifi'))
from puente import Lecturas, Puente
from ventana import Ventana
class BridgeTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.data=Lecturas(self.tmp.name);self.server=Puente(self.data,host='127.0.0.1',port=0)
  threading.Thread(target=self.server.serve_forever,daemon=True).start();self.url='http://'+self.server.local_host()
 def tearDown(self):self.server.shutdown();self.server.server_close();self.tmp.cleanup()
 def request(self,path,body=None,browser=False,key=None,origin=None,host=None):
  headers={}
  if browser:headers['Cookie']='grafiplot_bridge='+self.server.browser_token
  else:headers['Authorization']='Bearer '+(key or self.data.token)
  if body is not None:headers['Content-Type']='application/json';headers['Origin']=origin or self.url
  if host:headers['Host']=host
  req=urllib.request.Request(self.url+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
  with urllib.request.urlopen(req,timeout=5) as r:return r.status,r.read(),dict(r.headers)
 def test_end_to_end_and_retry(self):
  self.request('/scan',{'codigo':'001','evento':'test-1'});self.request('/scan',{'codigo':'001','evento':'test-1'})
  result=json.loads(self.request('/events',browser=True)[1]);self.assertEqual(result['pendientes'],1);self.assertEqual(result['events'][0]['codigo'],'001')
  self.request('/ack',{'evento':'test-1'},browser=True);self.assertEqual(json.loads(self.request('/events',browser=True)[1])['pendientes'],0)
  self.request('/scan',{'codigo':'001','evento':'test-1'});self.assertEqual(self.data.pendientes()[1],0)
 def test_persists_before_ack(self):
  self.request('/scan',{'codigo':'002','evento':'test-2'});copy=Lecturas(self.tmp.name);self.assertEqual(copy.pendientes()[1],1);self.assertEqual(copy.token,self.data.token)
 def test_bad_key(self):
  with self.assertRaises(urllib.error.HTTPError) as e:self.request('/scan',{'codigo':'001','evento':'test-3'},key='bad')
  self.assertEqual(e.exception.code,401)
 def test_rebinding_and_cross_origin_blocked(self):
  for path in ('/','/connection','/events'):
   with self.assertRaises(urllib.error.HTTPError) as e:self.request(path,browser=True,host='evil.example')
   self.assertEqual(e.exception.code,403)
  with self.assertRaises(urllib.error.HTTPError) as e:self.request('/ack',{'evento':'test'},browser=True,origin='https://evil.example')
  self.assertEqual(e.exception.code,403)
 def test_cookie_and_page(self):
  code,raw,headers=self.request('/');self.assertIn(b'qr-wifi.mjs',raw);self.assertIn('HttpOnly',headers['Set-Cookie']);self.assertIn('SameSite=Strict',headers['Set-Cookie'])
  self.assertEqual(json.loads(self.request('/connection',browser=True)[1])['token'],self.data.token)
 def test_invalid_event_and_conflict(self):
  for obj in ([],{'evento':'e','codigo':5},{'evento':'e','codigo':'x'*257}):
   with self.assertRaises(urllib.error.HTTPError) as e:self.request('/scan',obj)
   self.assertEqual(e.exception.code,400)
  self.request('/scan',{'evento':'e','codigo':'001'})
  with self.assertRaises(urllib.error.HTTPError) as e:self.request('/scan',{'evento':'e','codigo':'002'})
  self.assertEqual(e.exception.code,400)
 def test_desktop_result_requires_browser_and_pending_event(self):
  popup=Ventana();self.server.popup=popup
  try:
   self.request('/scan',{'evento':'desktop','codigo':'001'})
   with self.assertRaises(urllib.error.HTTPError) as e:self.request('/popup',{'evento':'desktop','text':'Cliente'})
   self.assertEqual(e.exception.code,403)
   self.request('/popup',{'evento':'desktop','text':'Cliente: Ana\nDebe S/ 15.00'},browser=True)
   self.assertEqual(json.loads(popup.path.read_text())['text'],'Cliente: Ana\nDebe S/ 15.00')
   self.assertFalse(json.loads(self.request('/popup-state',browser=True)[1])['closed'])
   popup.closed_path.write_text('otro-evento')
   self.assertFalse(json.loads(self.request('/popup-state',browser=True)[1])['closed'])
   popup.closed_path.write_text('desktop')
   self.assertTrue(json.loads(self.request('/popup-state',browser=True)[1])['closed'])
   self.request('/ack',{'evento':'desktop'},browser=True)
   self.assertFalse(json.loads(popup.path.read_text())['visible'])
   with self.assertRaises(urllib.error.HTTPError) as e:self.request('/popup',{'evento':'desktop','text':'Viejo'},browser=True)
   self.assertEqual(e.exception.code,400)
  finally:popup.close();self.server.popup=None
 def test_live_stream_is_browser_only_and_wakes(self):
  with self.assertRaises(urllib.error.HTTPError) as e:self.request('/live')
  self.assertEqual(e.exception.code,403)
  req=urllib.request.Request(self.url+'/live',headers={'Cookie':'grafiplot_bridge='+self.server.browser_token})
  with urllib.request.urlopen(req,timeout=4) as response:
   self.assertEqual(response.headers['Content-Type'],'text/event-stream')
   self.assertEqual(response.readline(),b'data: wake\n')
   self.assertEqual(response.readline(),b'\n')
   self.request('/scan',{'evento':'live','codigo':'002'})
   self.assertEqual(response.readline(),b'data: wake\n')
if __name__=='__main__':unittest.main()
