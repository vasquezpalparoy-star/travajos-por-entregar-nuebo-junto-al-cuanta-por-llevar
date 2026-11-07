using System;
using System.IO;
using System.Text;
using System.Net;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using System.Security.Cryptography;

public class LoginRequired : Exception { public LoginRequired(string message):base(message) {} }
public class CloudSession {
 public string UserId="",Email="",Access="",Refresh=""; public long Expires;
}
public class Emission {
 public string Id="",UserId="",Name="",Capture="",Code="",Date=""; public decimal Total;
 public string NoteId="",Note=""; public System.Collections.Generic.List<PrintLine> Lines=new System.Collections.Generic.List<PrintLine>();
}
static class PrivateFile {
 static readonly byte[] entropy=Encoding.UTF8.GetBytes("Grafiplot.Emisor.A6.v2");
 public static T Load<T>(string path) {
  if(!File.Exists(path))return default(T);
  byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser);
  using(var s=new MemoryStream(bytes))return (T)new XmlSerializer(typeof(T)).Deserialize(s);
 }
 public static void Save<T>(string path,T value) {
  byte[] data;using(var s=new MemoryStream()){new XmlSerializer(typeof(T)).Serialize(s,value);data=ProtectedData.Protect(s.ToArray(),entropy,DataProtectionScope.CurrentUser);}
  string tmp=path+".tmp";using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(data,0,data.Length);f.Flush(true);}
  if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);
 }
}
class CloudClient {
 internal const string Url="https://gyvztqpvgtinxoqhyfbw.supabase.co";
 internal const string Key="sb_publishable_m5tDCWoaiu-xP-lezwd3Lg_rlUfq118";
 readonly string path;public CloudSession Session;
 readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=2500000};
 public CloudClient(string folder) {
  path=Path.Combine(folder,"sesion-protegida.dat");
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  try{Session=PrivateFile.Load<CloudSession>(path);}catch{Session=null;}
 }
 internal static string StringValue(Dictionary<string,object> d,string key){object value;return d.TryGetValue(key,out value)&&value!=null?Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture):"";}
 object Request(string method,string endpoint,object body,string access) {
  var req=(HttpWebRequest)WebRequest.Create(Url+endpoint);req.Method=method;req.ContentType="application/json; charset=utf-8";req.Accept="application/json";
  req.Headers["apikey"]=Key;if(!string.IsNullOrEmpty(access))req.Headers["Authorization"]="Bearer "+access;
  req.Timeout=20000;req.ReadWriteTimeout=20000;
  if(body!=null){var bytes=Encoding.UTF8.GetBytes(json.Serialize(body));req.ContentLength=bytes.Length;using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
  try{using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){string text=reader.ReadToEnd();return string.IsNullOrEmpty(text)?null:json.DeserializeObject(text);}}
  catch(WebException ex){
   var response=ex.Response as HttpWebResponse;
   if(response==null)throw new Exception("No se pudo conectar al sistema. Revisa Internet y vuelve a intentar; se conservará la misma emisión.");
   using(response)using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){
    string message="No se pudo completar la operación.";
    try{var d=json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());foreach(var k in new[]{"message","msg","error_description","error"}){var m=StringValue(d,k);if(m.Length>0){message=m;break;}}}catch{}
    if(response.StatusCode==HttpStatusCode.Unauthorized)throw new LoginRequired("La sesión no es válida. Conecta tu cuenta de nuevo.");
    throw new Exception(message);
   }
  }
 }
 void AcceptSession(Dictionary<string,object> data) {
  var user=(Dictionary<string,object>)data["user"];
  var session=new CloudSession{UserId=StringValue(user,"id"),Email=StringValue(user,"email"),Access=StringValue(data,"access_token"),Refresh=StringValue(data,"refresh_token"),Expires=Now()+Convert.ToInt64(data["expires_in"])-90};
  if(session.UserId==""||session.Access==""||session.Refresh=="")throw new Exception("El sistema devolvió una sesión incompleta.");
  PrivateFile.Save(path,session);Session=session;
 }
 static long Now(){return (long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;}
 public void Login(string email,string password) {AcceptSession((Dictionary<string,object>)Request("POST","/auth/v1/token?grant_type=password",new{email=email,password=password},null));}
 void EnsureSession(){
  if(Session==null)throw new LoginRequired("Conecta tu cuenta del sistema.");
  if(Session.Expires>Now())return;
  try{AcceptSession((Dictionary<string,object>)Request("POST","/auth/v1/token?grant_type=refresh_token",new{refresh_token=Session.Refresh},null));}
  catch(LoginRequired){throw;}catch(Exception ex){throw new LoginRequired("No se pudo renovar la sesión: "+ex.Message);}
 }
 public void Register(Emission emission) {
  EnsureSession();if(emission.UserId!=Session.UserId)throw new Exception("Esta emisión pertenece a otra cuenta. Conecta el correo con el que la iniciaste.");
  var response=(Dictionary<string,object>)Request("POST","/rest/v1/rpc/grafiplot_notas_emitir_a6_v2",new{p_id=emission.Id,p_nombre=emission.Name,p_total=emission.Total,p_items=Payload(emission),p_nota=emission.Note},Session.Access);
  Validate(response,emission);ValidateDetails(response,emission);
  if(emission.Total!=Convert.ToDecimal(response["total"],System.Globalization.CultureInfo.InvariantCulture)||emission.Name!=StringValue(response,"nombre"))throw new Exception("Este trabajo fue modificado en la web. Revisa su comprobante en el sistema antes de imprimir.");
  emission.Code=StringValue(response,"codigo");emission.Date=StringValue(response,"guardado_en");
 }
 public void VerifyActive(Emission emission) {
  EnsureSession();if(emission.UserId!=Session.UserId)throw new Exception("El comprobante pertenece a otra cuenta.");
  var rows=(object[])Request("GET","/rest/v1/pedidos_pendientes?select=id,user_id,codigo,nombre,total,estado,guardado_en,items,descripcion&id=eq."+Uri.EscapeDataString(emission.Id)+"&user_id=eq."+Uri.EscapeDataString(Session.UserId),null,Session.Access);
  if(rows.Length!=1)throw new Exception("Este trabajo ya no está disponible en el sistema. No se imprimirá un QR sin registro.");
  var row=(Dictionary<string,object>)rows[0];Validate(row,emission);ValidateDetails(row,emission);
  if(emission.Code!=StringValue(row,"codigo"))throw new Exception("El QR del trabajo ha cambiado. Imprime el comprobante actualizado desde la web.");
  if(emission.Total!=Convert.ToDecimal(row["total"],System.Globalization.CultureInfo.InvariantCulture)||emission.Name!=StringValue(row,"nombre"))throw new Exception("El trabajo fue modificado en la web. Imprime su comprobante actualizado desde el sistema.");
 }
 public static object[] Payload(Emission e) { var list=new List<object>();foreach(var l in e.Lines)list.Add(l.Payload());return list.ToArray(); }
 static void ValidateDetails(Dictionary<string,object> row,Emission e) {
  object raw;if(!row.TryGetValue("items",out raw))throw new Exception("Falta el detalle del registro.");
  var items=raw as object[];if(items==null||items.Length!=e.Lines.Count)throw new Exception("El detalle cambió en la web.");
  for(int i=0;i<items.Length;i++){var item=items[i] as Dictionary<string,object>;var l=e.Lines[i];if(item==null||StringValue(item,"name")!=l.Name||StringValue(item,"mode")!=l.Mode||Convert.ToDecimal(item["price"],System.Globalization.CultureInfo.InvariantCulture)!=l.Price)throw new Exception("El detalle cambió en la web.");var qs=item["quantities"] as object[];if(qs==null||qs.Length!=l.Quantities.Length)throw new Exception("Las cantidades cambiaron en la web.");for(int j=0;j<qs.Length;j++)if(Convert.ToString(qs[j],System.Globalization.CultureInfo.InvariantCulture)!=l.Quantities[j])throw new Exception("Las cantidades cambiaron en la web.");}
  string description=Summary(e),legacy=LegacyDescription(e);
  if(StringValue(row,"descripcion")!=description&&StringValue(row,"descripcion")!=legacy)throw new Exception("La nota o las cantidades cambiaron en la web. Revisa el comprobante en el sistema.");

 }
 internal static string LegacyDescription(Emission e){var names=new List<string>();foreach(var l in e.Lines)names.Add(l.Name);string s=String.Join(", ",names.ToArray());return e.Note!=""?e.Note+" | "+s:s;}
 internal static string Summary(Emission e){var keys=new List<string>();var names=new Dictionary<string,string>();var modes=new Dictionary<string,string>();var quantities=new Dictionary<string,decimal>();var amounts=new Dictionary<string,decimal>();foreach(var l in e.Lines){string key=l.Mode+"|"+l.Name;if(!names.ContainsKey(key)){keys.Add(key);names[key]=l.Name;modes[key]=l.Mode;quantities[key]=0;amounts[key]=0;}quantities[key]+=l.Quantity;amounts[key]+=l.Amount;}var parts=new List<string>();foreach(string key in keys)parts.Add(names[key]+(modes[key]=="otros"?": S/ "+Calc.F(amounts[key]):": "+quantities[key].ToString("0",System.Globalization.CultureInfo.InvariantCulture)));return String.Join(" | ",parts.ToArray())+(e.Note!=""?" | Nota: "+e.Note:"");}
 static void Validate(Dictionary<string,object> row,Emission e) {
  if(StringValue(row,"id")!=e.Id||StringValue(row,"user_id")!=e.UserId)throw new Exception("La respuesta no corresponde a esta emisión.");
  if(StringValue(row,"estado")!="pendiente")throw new Exception("El trabajo ya no está pendiente. No se reutilizará su QR.");
  int n;var code=StringValue(row,"codigo");if(code.Length!=3||!int.TryParse(code,out n)||n<1||n>500)throw new Exception("El sistema no devolvió un QR válido.");
 }
}

