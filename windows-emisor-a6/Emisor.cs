using System;
using System.IO;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Globalization;
using System.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class Options {
 public string Printer=""; public int Tray=-1; public string TrayName="";
 public int Copies=1; public long Next=1;
}
static class Program {
 public static bool Smoke=false;
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [STAThread] static void Main(string[] args) {
  if(args.Length>0 && args[0]=="--self-test") { SelfTest.Run(); return; }
  Smoke=args.Length>0 && args[0]=="--ui-smoke-test";
  bool first; using(var mutex=new Mutex(true,"Local\\GrafiplotEmisorA6_1",out first)) {
   if(!first) { MessageBox.Show("El emisor ya está abierto. Busca el icono de recibo junto al reloj.","GRAFIPLOT"); return; }
   try { SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Emisor()); }
   catch(Exception ex) { if(Smoke){Console.Error.WriteLine(ex);Environment.ExitCode=1;}else MessageBox.Show(ex.Message,"No se pudo iniciar GRAFIPLOT",MessageBoxButtons.OK,MessageBoxIcon.Error); }
  }
 }
}
static class Money {
 public static decimal Parse(string text) {
  text=text.Trim().Replace("S/", "").Trim();
  if(!System.Text.RegularExpressions.Regex.IsMatch(text,@"^\d{1,7}([.,]\d{1,2})?$")) throw new Exception("Escribe un monto válido, por ejemplo 25.00 o 25,50. No uses separadores de miles.");
  decimal value=decimal.Parse(text.Replace(',','.'),CultureInfo.InvariantCulture);
  if(value<=0 || value>9999999.99m) throw new Exception("El monto debe ser mayor que cero y hasta S/ 9 999 999.99.");
  return value;
 }
 public static string Text(decimal value) { return "S/ "+value.ToString("0.00",CultureInfo.InvariantCulture); }
}
class Emisor : Form {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GrafiplotEmisorA6");
 readonly string settings=Path.Combine(Folder,"configuracion.xml");
 Options opt; NotifyIcon tray; TextBox name,amount; Label info,status; Button emit; bool exit=false,busy=false;
 Icon receiptIcon; PictureBox capturePreview; Bitmap capture;
 CloudClient cloud; Emission pending; string pendingFile,lastFile; Button account;
 Emission last;
 public Emisor() {
  Directory.CreateDirectory(Folder); opt=LoadSettings();
  cloud=new CloudClient(Folder); pendingFile=Path.Combine(Folder,"emision-pendiente.dat");lastFile=Path.Combine(Folder,"ultimo-comprobante.dat");
  try{pending=PrivateFile.Load<Emission>(pendingFile);last=PrivateFile.Load<Emission>(lastFile);}catch(Exception ex){throw new Exception("No se pudo recuperar el comprobante guardado. No emitas otra vez sin revisar el sistema. "+ex.Message);}
  using(var s=typeof(Emisor).Assembly.GetManifestResourceStream("recibo.ico")) receiptIcon=new Icon(s);
  Icon=receiptIcon; Text="GRAFIPLOT • Emisor A6"; ClientSize=new Size(360,479); AutoScaleMode=AutoScaleMode.Font;
  Font=new Font("Segoe UI",10); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; MinimizeBox=false;
  ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; BackColor=Color.White;
  var head=new Label {Text="GRAFIPLOT • Emisor A6",Location=new Point(0,0),Size=new Size(360,42),BackColor=Color.FromArgb(192,0,27),ForeColor=Color.White,Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter}; Controls.Add(head);
  AddLabel(this,"Número o nombre",18,53,320,23);
  name=new TextBox {Location=new Point(18,80),Size=new Size(324,32),Font=new Font("Segoe UI",14),MaxLength=80}; Controls.Add(name);
  capturePreview=new PictureBox {Location=new Point(18,80),Size=new Size(324,70),SizeMode=PictureBoxSizeMode.StretchImage,BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,Visible=false,TabStop=true}; Controls.Add(capturePreview);
  var paste=MakeButton("Pegar captura",18,158,158,29); paste.Click+=(s,e)=>PasteCapture(); Controls.Add(paste);
  var write=MakeButton("Escribir texto",184,158,158,29); write.Click+=(s,e)=>{if(!busy && pending==null){ClearCapture();name.Focus();}}; Controls.Add(write);
  AddLabel(this,"Monto en soles (S/)",18,197,320,23);
  amount=new TextBox {Location=new Point(18,224),Size=new Size(324,32),Font=new Font("Segoe UI",16),MaxLength=12}; Controls.Add(amount);
  info=AddLabel(this,"",18,267,324,42); info.Font=new Font("Segoe UI",8);
  var cfg=MakeButton("Configuración",18,313,158,30); cfg.Click+=(s,e)=>Configure(); Controls.Add(cfg);
  var clear=MakeButton("Limpiar",184,313,158,30); clear.Click+=(s,e)=>{if(!busy){if(pending!=null){MessageBox.Show(this,"Hay una emisión pendiente. Reinténtala antes de iniciar otra para evitar duplicados.","Emisión pendiente");return;}ClearCapture();name.Clear();amount.Clear();name.Focus();}}; Controls.Add(clear);
  account=MakeButton("Conectar cuenta del sistema",18,349,324,30);account.Click+=async(s,e)=>await Connect();Controls.Add(account);
  emit=MakeButton("REGISTRAR E IMPRIMIR",18,393,324,43); emit.BackColor=Color.FromArgb(192,0,27); emit.ForeColor=Color.White; emit.Font=new Font("Segoe UI",12,FontStyle.Bold); emit.Click+=async(s,e)=>await Emit(false); Controls.Add(emit);
  status=AddLabel(this,"Listo • impresión solo en negro",18,442,324,29); status.Font=new Font("Segoe UI",8);
  AcceptButton=emit;
  var menu=new ContextMenuStrip(); menu.Items.Add("Mostrar / ocultar",null,(s,e)=>Toggle()); menu.Items.Add("Configuración",null,(s,e)=>{ShowPanel();Configure();}); menu.Items.Add("Reimprimir último comprobante",null,async(s,e)=>{ShowPanel();await Emit(true);}); menu.Items.Add("Abrir Trabajos por entregar",null,(s,e)=>System.Diagnostics.Process.Start("https://vasquezpalparoy-star.github.io/travajos-por-entregar-nuebo-junto-al-cuanta-por-llevar/"));menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Salir",null,(s,e)=>{exit=true;Close();});
  tray=new NotifyIcon {Icon=receiptIcon,Text="GRAFIPLOT Emisor A6 • clic para abrir / ocultar",Visible=true,ContextMenuStrip=menu};
  tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)Toggle();};
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}if(!exit){e.Cancel=true;Hide();}};
  Shown+=(s,e)=>{Place();name.Focus(); if(!Program.Smoke && string.IsNullOrEmpty(opt.Printer)) Configure();if(Program.Smoke)SmokeUi();};
  UpdateInfo();UpdateAccount();
  if(pending!=null){name.Text=pending.Name;amount.Text=pending.Total.ToString("0.00",CultureInfo.InvariantCulture);if(!string.IsNullOrEmpty(pending.Capture)){using(var ms=new MemoryStream(Convert.FromBase64String(pending.Capture.Substring("data:image/png;base64,".Length))))using(var im=Image.FromStream(ms))capture=new Bitmap(im);capturePreview.Image=capture;capturePreview.Visible=true;name.Visible=false;}SetPendingMode();}
 }
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
  if(keyData==(Keys.Control|Keys.V) && !amount.ContainsFocus) {
   try {if(Clipboard.ContainsImage()){PasteCapture();return true;}}
   catch(Exception ex){MessageBox.Show(this,"No se pudo leer el portapapeles: "+ex.Message,"Captura");return true;}
  }
  return base.ProcessCmdKey(ref msg,keyData);
 }
 void PasteCapture() {
  if(busy||pending!=null)return;
  try {
   if(!Clipboard.ContainsImage())throw new Exception("Primero captura una región con Windows + Mayús + S. Después pulsa Pegar captura o Ctrl+V aquí.");
   using(var image=Clipboard.GetImage()) {
    if(image==null)throw new Exception("No hay una imagen disponible en el portapapeles.");
    if((long)image.Width*image.Height>20000000)throw new Exception("La captura es muy grande. Captura solo la zona del nombre o número.");
    var next=GrayCapture(image); ClearCapture(); capture=next; capturePreview.Image=capture;
    name.Visible=false;capturePreview.Visible=true;capturePreview.Focus();
    status.Text="Captura ajustada al cuadro • impresión en negro";
   }
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"Pegar captura",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }
 void ClearCapture() {capturePreview.Image=null;if(capture!=null){capture.Dispose();capture=null;}capturePreview.Visible=false;name.Visible=true;}
 internal static Bitmap GrayCapture(Image image) {
  var output=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
  using(var g=Graphics.FromImage(output))using(var a=new System.Drawing.Imaging.ImageAttributes()) {
   g.Clear(Color.White);
   var matrix=new System.Drawing.Imaging.ColorMatrix(new float[][] {
    new float[]{.299f,.299f,.299f,0,0},new float[]{.587f,.587f,.587f,0,0},new float[]{.114f,.114f,.114f,0,0},new float[]{0,0,0,1,0},new float[]{0,0,0,0,1}});
   a.SetColorMatrix(matrix);g.DrawImage(image,new Rectangle(0,0,output.Width,output.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,a);
  }
  return output;
 }
 static Label AddLabel(Control parent,string text,int x,int y,int w,int h) { var l=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h)};parent.Controls.Add(l);return l; }
 static Button MakeButton(string text,int x,int y,int w,int h) {return new Button{Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};}
 Options LoadSettings() {
  if(!File.Exists(settings))return new Options();
  try {using(var s=File.OpenRead(settings)){var o=(Options)new XmlSerializer(typeof(Options)).Deserialize(s); if(o.Next<1||o.Copies<1||o.Copies>10)throw new Exception("Valores inválidos");return o;}}
  catch(Exception ex){MessageBox.Show("No se pudo leer la configuración. Se abrirá con valores nuevos. Archivo: "+settings+"\n"+ex.Message,"Configuración");return new Options();}
 }
 void Save(Options value) {
  string tmp=settings+".tmp";
  using(var s=File.Create(tmp))new XmlSerializer(typeof(Options)).Serialize(s,value);
  if(File.Exists(settings)) File.Replace(tmp,settings,settings+".bak"); else File.Move(tmp,settings);
 }
 void Place(){var r=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(r.Right-Width-12,r.Bottom-Height-12);}
 void ShowPanel(){Place();Show();WindowState=FormWindowState.Normal;Activate();name.Focus();}
 void Toggle(){if(busy)return;if(Visible)Hide();else ShowPanel();}
 void UpdateInfo(){info.Text=(string.IsNullOrEmpty(opt.Printer)?"Elige la impresora en Configuración":opt.Printer+" • "+opt.TrayName)+"\nA6 • Negro • "+opt.Copies+" copia(s)";}
 void Configure() {
  if(busy)return;
  using(var f=new Form {Text="Configuración de impresión",ClientSize=new Size(430,340),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=Font,Icon=receiptIcon}) {
   AddLabel(f,"Impresora para este programa",15,15,400,24);
   var printers=new ComboBox {Location=new Point(15,44),Width=400,DropDownStyle=ComboBoxStyle.DropDownList}; f.Controls.Add(printers);
   foreach(string p in PrinterSettings.InstalledPrinters)printers.Items.Add(p);
   AddLabel(f,"Bandeja de papel",15,83,400,24);
   var sources=new ComboBox {Location=new Point(15,111),Width=400,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="SourceName"}; f.Controls.Add(sources);
   var note=AddLabel(f,"",15,150,400,48);note.Font=new Font("Segoe UI",9);
   AddLabel(f,"Copias",15,209,80,24);var copies=new NumericUpDown{Location=new Point(100,206),Minimum=1,Maximum=10,Value=opt.Copies,Width=70};f.Controls.Add(copies);
   Action refresh=()=>{sources.Items.Clear();try{var ps=new PrinterSettings{PrinterName=(string)printers.SelectedItem};foreach(PaperSource p in ps.PaperSources)sources.Items.Add(p);foreach(PaperSource p in sources.Items)if(ps.PrinterName==opt.Printer && p.RawKind==opt.Tray)sources.SelectedItem=p;if(sources.SelectedIndex<0 && sources.Items.Count>0)sources.SelectedIndex=0;bool a6=false;foreach(PaperSize p in ps.PaperSizes)if(IsA6(p))a6=true;note.Text=a6?"Papel A6 (105 × 148 mm), vertical, una cara y negro.\nColoca A6 y confirma el tamaño en la impresora.":"Este controlador no anuncia A6. Instala el controlador HP\ncompleto y confirma que admita A6 antes de imprimir.";}catch(Exception ex){note.Text=ex.Message;}};
   printers.SelectedIndexChanged+=(s,e)=>refresh();
   printers.SelectedItem=opt.Printer;
   if(printers.SelectedIndex<0) {foreach(string p in printers.Items)if(p.IndexOf("9020",StringComparison.OrdinalIgnoreCase)>=0){printers.SelectedItem=p;break;}}
   if(printers.SelectedIndex<0 && printers.Items.Count>0)printers.SelectedIndex=0;
   var save=MakeButton("Guardar",220,285,95,34);f.Controls.Add(save);
   var cancel=MakeButton("Cancelar",322,285,93,34);cancel.DialogResult=DialogResult.Cancel;f.Controls.Add(cancel); f.CancelButton=cancel;
   save.Click+=(s,e)=>{try{if(printers.SelectedItem==null||sources.SelectedItem==null)throw new Exception("Selecciona una impresora y una bandeja disponibles.");var p=(PaperSource)sources.SelectedItem;var next=new Options{Printer=(string)printers.SelectedItem,Tray=p.RawKind,TrayName=p.SourceName,Copies=(int)copies.Value,Next=opt.Next};Save(next);opt=next;UpdateInfo();f.DialogResult=DialogResult.OK;f.Close();}catch(Exception ex){MessageBox.Show(f,ex.Message,"No se pudo guardar");}};
   f.ShowDialog(this);
  }
 }
 static bool IsA6(PaperSize p){return p.Kind==PaperKind.A6 || (Math.Abs(p.Width-413)<=3 && Math.Abs(p.Height-583)<=3);}
 void SmokeUi() {
  var timer=new System.Windows.Forms.Timer{Interval=500};
  timer.Tick+=(s,e)=>{timer.Stop();try{
   amount.Text="25.00";
   using(var image=new Bitmap(145,48)){using(var g=Graphics.FromImage(image))g.Clear(Color.Blue);Clipboard.SetImage(image);}
   PasteCapture();if(capture==null||!capturePreview.Visible||name.Visible)throw new Exception("Clipboard image mode failed");
   var color=capture.GetPixel(10,10);if(color.R!=color.G||color.G!=color.B)throw new Exception("Clipboard grayscale failed");
   using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save("interfaz-captura-prueba.png");}
   ClearCapture();name.Text="JUAN PÉREZ";if(!name.Visible||capturePreview.Visible||amount.Text!="25.00")throw new Exception("Text mode failed");
   using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save("interfaz-texto-prueba.png");}
   Clipboard.Clear();
  }catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}finally{timer.Dispose();exit=true;Close();}};
  timer.Start();
 }
 void UpdateAccount(){account.Text=cloud.Session==null?"Conectar cuenta del sistema":"Cuenta: "+cloud.Session.Email;}
 void SetPendingMode(){name.ReadOnly=pending!=null;amount.ReadOnly=pending!=null;emit.Text=pending==null?"REGISTRAR E IMPRIMIR":(string.IsNullOrEmpty(pending.Code)?"REINTENTAR REGISTRO":"IMPRIMIR QR "+pending.Code);if(pending!=null)status.Text="Emisión recuperable • "+(pending.Code==""?"falta confirmar registro":"QR "+pending.Code+" guardado");}
 Task<bool> Connect() {
  if(busy)return Task.FromResult(false);
  using(var f=new Form{Text="Conectar con tu sistema",ClientSize=new Size(385,266),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=Font,Icon=receiptIcon}) {
   AddLabel(f,"Usa el mismo correo de Trabajos por entregar.",15,12,355,35);
   AddLabel(f,"Correo",15,50,355,22);var email=new TextBox{Location=new Point(15,73),Width=355,Text=cloud.Session==null?"":cloud.Session.Email};f.Controls.Add(email);
   AddLabel(f,"Contraseña",15,110,355,22);var pass=new TextBox{Location=new Point(15,133),Width=355,UseSystemPasswordChar=true};f.Controls.Add(pass);
   var message=AddLabel(f,"La contraseña no se guarda.",15,170,355,38);message.Font=new Font("Segoe UI",8);
   var button=MakeButton("Conectar",15,219,172,33);var cancel=MakeButton("Cancelar",198,219,172,33);f.Controls.Add(button);f.Controls.Add(cancel);cancel.DialogResult=DialogResult.Cancel;f.CancelButton=cancel;f.AcceptButton=button;
   bool connecting=false;f.FormClosing+=(a,b)=>{if(connecting)b.Cancel=true;};
   button.Click+=async(a,b)=>{
    if(email.Text.Trim()==""||pass.Text==""){message.Text="Escribe tu correo y contraseña del sistema.";return;}
    connecting=true;button.Enabled=false;cancel.Enabled=false;message.Text="Conectando...";
    string mail=email.Text.Trim(),password=pass.Text;
    try{await Task.Run(()=>cloud.Login(mail,password));UpdateAccount();
     if(pending!=null&&pending.UserId!=cloud.Session.UserId)throw new Exception("Hay una emisión de otra cuenta. Conecta con su correo para terminarla.");
     connecting=false;f.DialogResult=DialogResult.OK;f.Close();
    }catch(Exception ex){message.Text=ex.Message;}
    finally{password=null;pass.Clear();connecting=false;button.Enabled=true;cancel.Enabled=true;}
   };
   return Task.FromResult(f.ShowDialog(this)==DialogResult.OK);
  }
 }
 PrintDocument PreparePrinter() {
  if(string.IsNullOrEmpty(opt.Printer))throw new Exception("Elige la impresora y bandeja en Configuración.");
  var doc=new PrintDocument();
  try {
   doc.PrinterSettings.PrinterName=opt.Printer;if(!doc.PrinterSettings.IsValid)throw new Exception("La impresora guardada no está disponible.");
   PaperSource source=null;foreach(PaperSource p in doc.PrinterSettings.PaperSources)if(p.RawKind==opt.Tray&&p.SourceName==opt.TrayName){source=p;break;}
   if(source==null)throw new Exception("La bandeja guardada no está disponible. Selecciónala de nuevo en Configuración.");
   PaperSize paper=null;foreach(PaperSize p in doc.PrinterSettings.PaperSizes)if(IsA6(p)){paper=p;break;}
   if(paper==null)throw new Exception("El controlador no anuncia A6. Instala el controlador HP completo o habilita A6 en Windows.");
   doc.PrinterSettings.Copies=(short)opt.Copies;if(doc.PrinterSettings.CanDuplex)doc.PrinterSettings.Duplex=Duplex.Simplex;
   doc.DefaultPageSettings.PaperSize=paper;doc.DefaultPageSettings.PaperSource=source;doc.DefaultPageSettings.Color=false;doc.DefaultPageSettings.Landscape=false;doc.DefaultPageSettings.Margins=new Margins(28,28,28,28);doc.PrintController=new StandardPrintController();return doc;
  }catch{doc.Dispose();throw;}
 }
 async Task Emit(bool reprint) {
  if(busy)return;
  try {
   if(reprint&&pending!=null)throw new Exception("Termina primero la emisión pendiente.");
   if(reprint&&last==null)throw new Exception("No hay un comprobante anterior para reimprimir en este equipo.");
   if(cloud.Session==null && !await Connect())return;
   using(var doc=PreparePrinter()) {
    if(!reprint&&pending==null){
     string who=name.Text.Trim();if(who.Length==0&&capture==null)throw new Exception("Escribe un nombre o pega una captura.");decimal value=Money.Parse(amount.Text);
     string image="";if(capture!=null){using(var ms=new MemoryStream()){capture.Save(ms,System.Drawing.Imaging.ImageFormat.Png);image="data:image/png;base64,"+Convert.ToBase64String(ms.ToArray());}if(image.Length>600000)throw new Exception("La captura es demasiado grande. Captura solo el nombre o número.");who="Cliente (captura)";}
     var draft=new Emission{Id=Guid.NewGuid().ToString(),UserId=cloud.Session.UserId,Name=who,Total=value,Capture=image};
     PrivateFile.Save(pendingFile,draft);pending=draft;SetPendingMode();
    }
    var job=reprint?last:pending;
    busy=true;emit.Enabled=false;account.Enabled=false;
    try {
     if(string.IsNullOrEmpty(job.Code)) {
      status.Text="Registrando trabajo y asignando QR...";
      await Task.Run(()=>cloud.Register(job));PrivateFile.Save(pendingFile,job);
     } else {status.Text="Verificando registro y QR...";await Task.Run(()=>cloud.VerifyActive(job));}
     UpdateAccount();status.Text="QR "+job.Code+" guardado • enviando a impresora...";
     using(var ms=new MemoryStream(string.IsNullOrEmpty(job.Capture)?new byte[0]:Convert.FromBase64String(job.Capture.Substring("data:image/png;base64,".Length)))) {
      Image picture=null;
      try {
       if(ms.Length>0)picture=Image.FromStream(ms);
       DateTime when; if(!DateTime.TryParse(job.Date,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out when))when=DateTime.Now;when=when.ToLocalTime();
       doc.DocumentName="GRAFIPLOT A6 QR "+job.Code;
       doc.PrintPage+=(a,b)=>{DrawReceipt(b.Graphics,b.PageBounds.Width,b.PageBounds.Height,job.Name,job.Total,0,when,b.PageSettings.HardMarginX,b.PageSettings.HardMarginY,picture,job.Code);b.HasMorePages=false;};
       doc.Print();
       // Retain the same saved record for explicit reprints; never register it again.
       if(!reprint){PrivateFile.Save(lastFile,job);last=job;File.Delete(pendingFile);pending=null;ClearCapture();name.Clear();amount.Clear();SetPendingMode();}
       status.Text="QR "+job.Code+" registrado • enviado a la cola";name.Focus();
      }finally{if(picture!=null)picture.Dispose();}
     }
    }catch(LoginRequired ex){MessageBox.Show(this,ex.Message+" Usa el botón de cuenta y reintenta.","Conectar cuenta");}
    catch(Exception ex){MessageBox.Show(this,ex.Message+"\nSi ya existe en el sistema, el reintento conservará su QR. Revisa la cola antes de volver a imprimir.","Registro e impresión",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    finally{busy=false;emit.Enabled=true;account.Enabled=true;if(pending!=null)SetPendingMode();}
   }
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"Emisor A6",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }
 internal static void DrawReceipt(Graphics g,float w,float h,string who,decimal value,long number,DateTime when,float hx,float hy,Image screenshot=null,string qrCode=null) {
  g.TranslateTransform(-hx,-hy);float left=30,width=w-60;
  using(var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
  using(var title=new Font("Arial",21,FontStyle.Bold))
  using(var medium=new Font("Arial",12,FontStyle.Bold))
  using(var normal=new Font("Arial",10))
  using(var pen=new Pen(Color.Black,1)) {
   g.Clear(Color.White);
   g.DrawString("GRAFIPLOT",title,Brushes.Black,new RectangleF(left,38,width,45),center);
   g.DrawString("COMPROBANTE",medium,Brushes.Black,new RectangleF(left,87,width,28),center);
   g.DrawLine(pen,left,126,w-left,126);
   g.DrawString(qrCode==null?"N.º "+number.ToString("D6"):"CÓDIGO: "+qrCode,medium,Brushes.Black,new RectangleF(left,139,width,27),center);
   g.DrawString(when.ToString("dd/MM/yyyy HH:mm"),normal,Brushes.Black,new RectangleF(left,172,width,23),center);
   g.DrawString("Nombre / Número",normal,Brushes.Black,new RectangleF(left,qrCode==null?213:202,width,24),center);
   if(screenshot!=null) {
    // Fill the entire name rectangle; intentionally stretch both axes as requested.
    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
    g.DrawImage(screenshot,new RectangleF(left,qrCode==null?244:230,width,qrCode==null?79:60),new RectangleF(0,0,screenshot.Width,screenshot.Height),GraphicsUnit.Pixel);
   } else using(var font=Fit(g,who,width,qrCode==null?79:60,16,center))g.DrawString(who,font,Brushes.Black,new RectangleF(left,qrCode==null?244:230,width,qrCode==null?79:60),center);
   g.DrawLine(pen,left,qrCode==null?339:300,w-left,qrCode==null?339:300);
   g.DrawString("MONTO",medium,Brushes.Black,new RectangleF(left,qrCode==null?357:306,width,28),center);
   using(var font=Fit(g,Money.Text(value),width,qrCode==null?65:48,qrCode==null?34:28,center))g.DrawString(Money.Text(value),font,Brushes.Black,new RectangleF(left,qrCode==null?391:335,width,qrCode==null?65:48),center);
   if(qrCode!=null){ReceiptQr.Draw(g,qrCode,new RectangleF((w-116)/2,390,116,116));g.DrawString("CÓDIGO: "+qrCode,medium,Brushes.Black,new RectangleF(left,509,width,23),center);}
   g.DrawString("Gracias por su preferencia",normal,Brushes.Black,new RectangleF(left,qrCode==null?h-85:h-44,width,25),center);
  }
 }
 static Font Fit(Graphics g,string text,float width,float height,float start,StringFormat format){for(float n=start;n>=7;n--){var f=new Font("Arial",n,FontStyle.Bold);var size=g.MeasureString(text,f,(int)width,format);if(size.Height<=height && size.Width<=width+1)return f;f.Dispose();}return new Font("Arial",7,FontStyle.Bold);}
 protected override void Dispose(bool disposing){if(disposing){if(capture!=null){capture.Dispose();capture=null;}if(tray!=null){tray.Visible=false;tray.Dispose();}if(receiptIcon!=null)receiptIcon.Dispose();}base.Dispose(disposing);}
}
static class SelfTest {
 public static void Run() {
  if(Money.Parse("25,50")!=25.50m||Money.Parse("S/ 25.00")!=25m)throw new Exception("Parse failed");
  foreach(var bad in new[]{"", "0", "-5", "1,234.56", "1.234", "abc", "10000000"}){bool rejected=false;try{Money.Parse(bad);}catch{rejected=true;}if(!rejected)throw new Exception("Accepted invalid amount: "+bad);}
  using(var b=new Bitmap(413,583))using(var g=Graphics.FromImage(b)){g.PageUnit=GraphicsUnit.Pixel;Emisor.DrawReceipt(g,413,583,"Juan Pérez",25m,1,new DateTime(2026,11,6,13,42,0),0,0);b.Save("comprobante-prueba.png");}
  using(var b=new Bitmap(413,583))using(var g=Graphics.FromImage(b)){g.PageUnit=GraphicsUnit.Pixel;Emisor.DrawReceipt(g,413,583,new string('W',80),9999999.99m,999999,new DateTime(2026,11,6),0,0);b.Save("comprobante-limites.png");}
  using(var input=new Bitmap(145,48)) {
   using(var g=Graphics.FromImage(input)){g.Clear(Color.FromArgb(40,40,40));using(var f=new Font("Arial",9,FontStyle.Bold))g.DrawString("92006097***",f,Brushes.Cyan,8,10);}
   using(var gray=Emisor.GrayCapture(input)) {
    for(int y=0;y<gray.Height;y++)for(int x=0;x<gray.Width;x++){var c=gray.GetPixel(x,y);if(c.R!=c.G||c.G!=c.B)throw new Exception("Capture must be grayscale");}
    using(var b=new Bitmap(413,583))using(var g=Graphics.FromImage(b)){g.PageUnit=GraphicsUnit.Pixel;Emisor.DrawReceipt(g,413,583,"",25m,2,new DateTime(2026,11,6),0,0,gray);b.Save("comprobante-captura.png");
     if(b.GetPixel(32,246).R>100||b.GetPixel(380,320).R>100)throw new Exception("Capture must fill both corners of name rectangle");}
   }
  }
  var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  var test=serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(serializer.Serialize(new{nombre="PEÑA",total=25.50m}));
  if((string)test["nombre"]!="PEÑA"||Convert.ToDecimal(test["total"])!=25.50m)throw new Exception("JSON roundtrip failed");
  using(var writer=new StreamWriter("qr-matrices.txt"))for(int n=1;n<=500;n++){var matrix=ReceiptQr.Encode(n.ToString("D3"));writer.WriteLine(n.ToString("D3"));for(int y=0;y<21;y++){for(int x=0;x<21;x++)writer.Write(matrix[x,y]?'1':'0');writer.WriteLine();}}
  using(var b=new Bitmap(826,1166))using(var g=Graphics.FromImage(b)){g.PageUnit=GraphicsUnit.Pixel;g.ScaleTransform(2,2);Emisor.DrawReceipt(g,413,583,"JUAN PÉREZ",25.00m,0,new DateTime(2026,11,6,18,0,0),0,0,null,"027");b.Save("comprobante-qr.png");}
  if(Environment.OSVersion.Platform==PlatformID.Win32NT){
   string path=Path.Combine(Path.GetTempPath(),"grafiplot-test-"+Guid.NewGuid()+".dat");
   try{var value=new Emission{Id=Guid.NewGuid().ToString(),Name="PRUEBA",Total=25.50m};PrivateFile.Save(path,value);var copy=PrivateFile.Load<Emission>(path);if(copy.Id!=value.Id||copy.Total!=value.Total)throw new Exception("Protected recovery file failed");PrivateFile.Save(path,value);}
   finally{File.Delete(path);File.Delete(path+".bak");}
  }
  Console.WriteLine("OK: importes, texto, captura monocromática, JSON, 500 QR y recibo QR");
 }
}
