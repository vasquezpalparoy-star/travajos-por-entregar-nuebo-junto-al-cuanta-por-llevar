using System;
using System.IO;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class Options { public string Printer="",TrayName="";public int Tray=-1,Copies=1;public long Next=1; }
static class Program {
 public static bool Smoke,StartupSmoke,StartHidden;
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [STAThread] static void Main(string[] args) {
  if(Array.IndexOf(args,"--self-test")>=0){SelfTest.Run();return;}
  Smoke=Array.IndexOf(args,"--ui-smoke-test")>=0;StartupSmoke=Array.IndexOf(args,"--startup-smoke-test")>=0;StartHidden=StartupSmoke||Array.IndexOf(args,"--startup")>=0;
  bool first;using(var mutex=new Mutex(true,"Local\\GrafiplotNotasA6_v1",out first)){
   if(!first){MessageBox.Show("GRAFIPLOT Notas A6 ya está abierto. Pulsa su icono junto al reloj.");return;}
   try{SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Emisor());}
   catch(Exception ex){if(Smoke||StartupSmoke){File.WriteAllText("error-prueba.txt",ex.ToString());Environment.ExitCode=1;}else MessageBox.Show(ex.Message,"GRAFIPLOT Notas A6");}
  }
 }
}
class Emisor:Form {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),(Program.Smoke||Program.StartupSmoke)?"GrafiplotNotasA6-Pruebas":"GrafiplotNotasA6");
 readonly string settings=Path.Combine(Folder,"configuracion.xml"),bookFile=Path.Combine(Folder,"notas.dat"),pendingFile=Path.Combine(Folder,"emision-pendiente.dat"),lastFile=Path.Combine(Folder,"ultimo-comprobante.dat");
 Options opt;Book book;CloudClient cloud;Emission pending,last;Icon receiptIcon;NotifyIcon tray;
 TextBox name=new TextBox(),note=new TextBox();DataGridView grid=new DataGridView();TabControl tabs=new BlueTabs();Label totalLabel=new TotalBadge(),status=new Label(),info=new Label();Button emit,account,add,more;bool loading,dirty,busy,exit,firstVisibility=true;System.Windows.Forms.Timer autosave=new System.Windows.Forms.Timer();
 public Emisor(){
  Directory.CreateDirectory(Folder);if(!File.Exists(settings)&&!Program.Smoke&&!Program.StartupSmoke){string old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GrafiplotEmisorA6","configuracion.xml");if(File.Exists(old))File.Copy(old,settings);}
  opt=LoadSettings();cloud=new CloudClient(Folder);
  try{pending=PrivateFile.Load<Emission>(pendingFile);last=PrivateFile.Load<Emission>(lastFile);}catch(Exception ex){throw new Exception("No se pudo leer la emisión pendiente. Se conservaron sus archivos; revisa el sistema antes de emitir de nuevo. "+ex.Message);}
  string warning="";
  try{book=PrivateFile.Load<Book>(bookFile)??Calc.NewBook();Calc.Validate(book);}catch{try{book=PrivateFile.Load<Book>(bookFile+".bak");Calc.Validate(book);warning="Se recuperó la copia anterior de las notas.";}catch{throw new Exception("No se pudieron recuperar las notas. Los archivos se conservaron en "+Folder+". No se sobrescribieron.");}if(File.Exists(bookFile))File.Copy(bookFile,Path.Combine(Folder,"recuperar-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".dat"),true);}
  Calc.CompactNames(book);
  using(var s=typeof(Emisor).Assembly.GetManifestResourceStream("notas.ico"))receiptIcon=new Icon(s);
  Icon=receiptIcon;Text="GRAFIPLOT Notas A6";Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(520,520);MinimumSize=new Size(520,520);ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Theme.Paper;MaximizeBox=false;FormBorderStyle=FormBorderStyle.None;DoubleBuffered=true;
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=11,Padding=new Padding(8)};
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  foreach(int h in new[]{38,30,32,0,34,28,32,28,28,40,22})layout.RowStyles.Add(h==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,h));Controls.Add(layout);
  var head=new TitleBar(this,receiptIcon);layout.Controls.Add(head,0,0);
  var tabrow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};tabrow.RowCount=1;tabrow.RowStyles.Add(new RowStyle(SizeType.Percent,100));tabrow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));tabrow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));tabs.Dock=DockStyle.Fill;tabs.SelectedIndexChanged+=(s,e)=>{if(loading)return;CaptureNote();book.Active=tabs.SelectedIndex;ShowNote();Mark();};tabs.MouseDoubleClick+=(s,e)=>Rename();tabrow.Controls.Add(tabs,0,0);more=MakeButton("+",0,0,58,32);more.Dock=DockStyle.Fill;more.Margin=new Padding(2);more.Click+=(s,e)=>NewNote();tabrow.Controls.Add(more,1,0);layout.Controls.Add(tabrow,0,1);
  var clientrow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};clientrow.RowCount=1;clientrow.RowStyles.Add(new RowStyle(SizeType.Percent,100));clientrow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));clientrow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));clientrow.Controls.Add(new Label{Text="Cliente:",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font(Font,FontStyle.Bold)},0,0);name.Dock=DockStyle.Fill;name.MaxLength=80;name.TextChanged+=(s,e)=>Mark();clientrow.Controls.Add(new RoundField(name,false){Dock=DockStyle.Fill,Margin=new Padding(1)},1,0);layout.Controls.Add(clientrow,0,2);
  grid.Dock=DockStyle.Fill;grid.BackgroundColor=Theme.Paper;grid.BorderStyle=BorderStyle.None;grid.GridColor=Theme.Line;grid.Font=new Font("Segoe UI",10.5f);grid.DefaultCellStyle.ForeColor=Theme.Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(222,235,255);grid.DefaultCellStyle.SelectionForeColor=Theme.Ink;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Ink;grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;grid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;grid.DefaultCellStyle.BackColor=Color.FromArgb(250,251,255);grid.RowHeadersDefaultCellStyle.BackColor=Color.FromArgb(232,239,250);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersWidth=26;grid.RowTemplate.Height=26;grid.ColumnHeadersHeight=28;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(192,216,250);grid.ColumnHeadersDefaultCellStyle.Font=new Font(Font,FontStyle.Bold);grid.SelectionMode=DataGridViewSelectionMode.CellSelect;grid.MultiSelect=false;
  grid.Columns.Add("Name","Formato");grid.Columns.Add("Qty","Cantidad (+)");grid.Columns.Add("Price","Precio S/");grid.Columns.Add("Amount","Importe S/");grid.Columns[0].FillWeight=20;grid.Columns[1].FillWeight=36;grid.Columns[2].FillWeight=22;grid.Columns[3].FillWeight=22;grid.Columns[0].DefaultCellStyle.Font=new Font("Segoe UI",10.5f,FontStyle.Bold);grid.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;grid.Columns[0].DefaultCellStyle.Padding=new Padding(8,0,0,0);grid.CellPainting+=PaintCell;
  foreach(DataGridViewColumn c in grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
  grid.CellEndEdit+=(s,e)=>{if(!loading){RememberPrices();Calculate();Mark();}};grid.CellValueChanged+=(s,e)=>{if(!loading){Calculate();Mark();}};
  grid.EditingControlShowing+=(s,e)=>{var t=e.Control as TextBox;if(t!=null){t.TextChanged-=EditingChanged;t.TextChanged+=EditingChanged;t.MaxLength=grid.CurrentCell.ColumnIndex==0?60:grid.CurrentCell.ColumnIndex==1?400:12;}};
  grid.RowPostPaint+=(s,e)=>TextRenderer.DrawText(e.Graphics,(e.RowIndex+1).ToString(),Font,new Rectangle(e.RowBounds.Left,e.RowBounds.Top,27,e.RowBounds.Height),Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  layout.Controls.Add(grid,0,3);
  var totals=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};totals.RowCount=1;totals.RowStyles.Add(new RowStyle(SizeType.Percent,100));totals.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));add=MakeButton("+ Fila",0,0,80,28);add.Margin=new Padding(1,0,1,0);add.Click+=(s,e)=>AddRow();totals.Controls.Add(add,0,0);totalLabel.Dock=DockStyle.Fill;totalLabel.Font=new Font(Font.FontFamily,14,FontStyle.Bold);totalLabel.Margin=new Padding(75,0,0,0);totalLabel.TextAlign=ContentAlignment.MiddleRight;totals.Controls.Add(totalLabel,1,0);layout.Controls.Add(totals,0,4);
  layout.Controls.Add(new Label{Text="Cantidad × precio · Enter para sumar\nOtros: importe directo en soles",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Ink,Font=new Font("Segoe UI",8.5f)},0,5);
  var notes=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};notes.RowCount=1;notes.RowStyles.Add(new RowStyle(SizeType.Percent,100));notes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));notes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));notes.Controls.Add(new Label{Text="Nota:",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);note.Dock=DockStyle.Fill;note.Multiline=true;note.ScrollBars=ScrollBars.None;note.MaxLength=300;note.TextChanged+=(s,e)=>Mark();notes.Controls.Add(new RoundField(note,true){Dock=DockStyle.Fill,Margin=new Padding(1)},1,0);layout.Controls.Add(notes,0,6);
  var settingsrow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};settingsrow.RowCount=1;settingsrow.RowStyles.Add(new RowStyle(SizeType.Percent,100));settingsrow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));settingsrow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));info.Dock=DockStyle.Fill;info.TextAlign=ContentAlignment.MiddleLeft;info.ForeColor=Theme.Ink;info.Font=new Font(Font.FontFamily,9);settingsrow.Controls.Add(info,0,0);var cfg=MakeButton("⚙ Ajustes",0,0,104,32);cfg.Dock=DockStyle.Fill;cfg.Margin=new Padding(2,0,2,0);cfg.Click+=(s,e)=>Configure();settingsrow.Controls.Add(cfg,1,0);layout.Controls.Add(settingsrow,0,7);
  var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};account=MakeButton("Conectar cuenta",0,0,164,26);account.Click+=async(s,e)=>await Connect();account.Margin=new Padding(2,0,2,0);tools.Controls.Add(account);var web=MakeButton("Ver trabajos",0,0,106,26);web.Click+=(s,e)=>OpenWeb();web.Margin=new Padding(2,0,2,0);tools.Controls.Add(web);var options=MakeButton("Notas / respaldo",0,0,152,26);options.Click+=(s,e)=>NotesMenu(options);options.Margin=new Padding(2,0,2,0);tools.Controls.Add(options);layout.Controls.Add(tools,0,8);
  emit=MakeButton("GUARDAR E IMPRIMIR",0,0,560,38);emit.Dock=DockStyle.Fill;emit.Font=new Font(Font.FontFamily,12,FontStyle.Bold);emit.BackColor=Color.FromArgb(0,74,190);emit.ForeColor=Color.White;emit.Click+=async(s,e)=>await Emit(false);layout.Controls.Add(emit,0,9);
  status.Dock=DockStyle.Fill;status.Font=new Font(Font.FontFamily,8);status.ForeColor=Color.FromArgb(49,81,61);status.Text="Configura tus precios unitarios antes de emitir.";layout.Controls.Add(status,0,10);
  var menu=new ContextMenuStrip();menu.Items.Add("Abrir / ocultar",null,(s,e)=>Toggle());menu.Items.Add("Reimprimir último comprobante",null,async(s,e)=>{ShowPanel();await Emit(true);});menu.Items.Add("Ver trabajos por recoger",null,(s,e)=>OpenWeb());menu.Items.Add("Respaldar notas",null,(s,e)=>{ShowPanel();Export();});menu.Items.Add("Salir",null,(s,e)=>{if(!busy&&SaveBook()){exit=true;Close();}});
  SizeChanged+=(s,e)=>{using(var shape=Theme.Round(new RectangleF(0,0,Width,Height),11)){var previous=Region;Region=new Region(shape);if(previous!=null)previous.Dispose();}};using(var shape=Theme.Round(new RectangleF(0,0,Width,Height),11))Region=new Region(shape);
  tray=new NotifyIcon{Icon=receiptIcon,Text="GRAFIPLOT Notas A6 — clic para abrir / ocultar",Visible=true,ContextMenuStrip=menu};tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)Toggle();};
  autosave.Interval=700;autosave.Tick+=(s,e)=>{autosave.Stop();if(dirty)SaveBook();};
  FormClosing+=(s,e)=>{bool shutdown=e.CloseReason==CloseReason.WindowsShutDown||e.CloseReason==CloseReason.TaskManagerClosing||e.CloseReason==CloseReason.ApplicationExitCall;if(busy&&!shutdown){e.Cancel=true;return;}bool saved=SaveBook();if(!shutdown&&(!exit||!saved)){e.Cancel=true;if(saved)Hide();}};
  Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized){if(SaveBook())Hide();WindowState=FormWindowState.Normal;}};
  if(pending!=null)RestorePending();BuildTabs();SetMode();UpdateInfo();UpdateAccount();
  Shown+=(s,e)=>{Place();if(Program.Smoke)SmokeUi();else if(!Program.StartupSmoke){if(warning!="")MessageBox.Show(this,warning);if(!Program.StartHidden&&string.IsNullOrEmpty(opt.Printer))Configure();}};
  if(Program.StartupSmoke)SmokeStartup();
 }
 void PaintCell(object sender,DataGridViewCellPaintingEventArgs e){
  if(e.RowIndex<0||e.ColumnIndex<1)return;var row=grid.Rows[e.RowIndex];if(row.Tag==null)return;bool other=(bool)row.Tag;bool disabled=other&&(e.ColumnIndex==1||e.ColumnIndex==2);bool price=!other&&e.ColumnIndex==2;bool amount=e.ColumnIndex==3;
  if(!disabled&&!price&&!amount)return;e.PaintBackground(e.ClipBounds,false);var r=e.CellBounds;r.Inflate(-4,-4);string text=Convert.ToString(e.FormattedValue);
  if(disabled){Theme.Fill(e.Graphics,r,Color.FromArgb(230,232,239),Color.FromArgb(220,224,233),7);text="—";}
  else if(price){Theme.Fill(e.Graphics,r,Color.White,Color.FromArgb(250,252,255),6);Theme.Border(e.Graphics,r,Color.FromArgb(153,171,207),6);}
  else {Theme.Fill(e.Graphics,r,Color.FromArgb(216,248,199),Color.FromArgb(197,239,170),8);if(other)Theme.Border(e.Graphics,r,Theme.Blue,7);}
  var tr=new Rectangle(r.X+3,r.Y,r.Width-(price?18:6),r.Height);using(var f=new Font("Segoe UI",10.5f,amount?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(e.Graphics,text,f,tr,disabled?Color.Gray:amount?Color.FromArgb(15,50,8):Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  if(price){using(var pen=new Pen(Theme.Ink,1.5f)){int x=r.Right-12,y=r.Top+8;e.Graphics.DrawLine(pen,x,y+7,x+6,y+1);e.Graphics.DrawLine(pen,x+2,y+9,x+8,y+3);e.Graphics.DrawLine(pen,x,y+7,x,y+10);e.Graphics.DrawLine(pen,x,y+10,x+3,y+9);}}
  e.Paint(e.ClipBounds,DataGridViewPaintParts.Border|DataGridViewPaintParts.ErrorIcon);e.Handled=true;
 }
 void EditingChanged(object s,EventArgs e){if(!loading)Mark();}
 RowData ReadRow(DataGridViewRow r){var d=new RowData{Name=Cell(r,0),Quantities=Cell(r,1),Price=Cell(r,2),Amount=Cell(r,3),Other=(bool)r.Tag};return d;}
 string Cell(DataGridViewRow r,int col){if(grid.IsCurrentCellInEditMode&&grid.CurrentCell!=null&&grid.CurrentCell.RowIndex==r.Index&&grid.CurrentCell.ColumnIndex==col&&grid.EditingControl is TextBox)return ((TextBox)grid.EditingControl).Text;return Convert.ToString(r.Cells[col].Value);}
 void CaptureNote(){if(loading||book.Active<0||book.Active>=book.Notes.Count)return;var n=book.Notes[book.Active];n.Client=name.Text;n.Text=note.Text;n.Rows.Clear();foreach(DataGridViewRow r in grid.Rows)n.Rows.Add(ReadRow(r));}
 void BuildTabs(){loading=true;tabs.TabPages.Clear();foreach(var n in book.Notes)tabs.TabPages.Add(n.Title);tabs.SelectedIndex=book.Active;loading=false;ShowNote();}
 void ShowNote(){loading=true;grid.Rows.Clear();var n=book.Notes[book.Active];name.Text=n.Client;note.Text=n.Text;foreach(var r in n.Rows)InsertRow(r);loading=false;Calculate();}
 void InsertRow(RowData r){bool wasLoading=loading;loading=true;int i=grid.Rows.Add(r.Name,r.Other?"":r.Quantities,r.Other?"":r.Price,r.Other?r.Amount:"");var row=grid.Rows[i];row.Tag=r.Other;row.Cells[1].ReadOnly=r.Other;row.Cells[2].ReadOnly=r.Other;row.Cells[3].ReadOnly=!r.Other;row.Cells[3].Style.BackColor=Color.FromArgb(220,245,210);if(r.Other){row.Cells[1].Style.BackColor=Color.Gainsboro;row.Cells[2].Style.BackColor=Color.Gainsboro;}loading=wasLoading;}
 void Calculate(){if(loading)return;loading=true;decimal total=0;bool errors=false;foreach(DataGridViewRow row in grid.Rows){try{var l=Calc.Line(ReadRow(row));row.ErrorText="";if(l!=null)total+=l.Amount;if(!(bool)row.Tag)row.Cells[3].Value=l==null?"":Calc.F(l.Amount);}catch(Exception ex){errors=true;row.ErrorText=ex.Message;if(!(bool)row.Tag)row.Cells[3].Value="Revisar";}}if(total>9999999.99m)errors=true;totalLabel.Text=errors?"Revisa las filas marcadas":"Total: S/ "+Calc.F(total);totalLabel.ForeColor=errors?Color.DarkRed:Color.DarkGreen;loading=false;}
 void RememberPrices(){if(loading||pending!=null)return;foreach(DataGridViewRow row in grid.Rows){if((bool)row.Tag)continue;string f=Cell(row,0),p=Cell(row,2);try{Calc.Money(p);}catch{continue;}bool found=false;foreach(var rate in book.Rates)if(rate.Name==f){rate.Price=p;found=true;break;}if(!found)book.Rates.Add(new Rate{Name=f,Price=p});}}
 void Mark(){if(loading||pending!=null)return;dirty=true;autosave.Stop();autosave.Start();}
 bool SaveBook(){if(loading)return true;try{CaptureNote();RememberPrices();PrivateFile.Save(bookFile,book);dirty=false;if(!busy&&pending==null)status.Text="Guardado automático · "+DateTime.Now.ToString("HH:mm:ss");return true;}catch(Exception ex){status.Text="No se pudo guardar: "+ex.Message;return false;}}
 void RestorePending(){int i=book.Notes.FindIndex(x=>x.Id==pending.NoteId);if(i<0){book.Notes.Add(new NoteData{Id=pending.NoteId,Title="Emisión pendiente"});i=book.Notes.Count-1;}book.Active=i;var n=book.Notes[i];n.Client=pending.Name;n.Text=pending.Note;n.Rows.Clear();foreach(var l in pending.Lines)n.Rows.Add(new RowData{Name=l.Name,Other=l.Mode=="otros",Quantities=l.Expression,Price=Calc.F(l.Price),Amount=l.Mode=="otros"?Calc.F(l.Amount):""});}
 void SetMode(){bool locked=busy||pending!=null;grid.ReadOnly=locked;tabs.Enabled=!locked;name.ReadOnly=locked;note.ReadOnly=locked;add.Enabled=!locked;more.Enabled=!locked;account.Enabled=!busy;emit.Enabled=!busy;emit.Text=pending==null?"GUARDAR E IMPRIMIR":String.IsNullOrEmpty(pending.Code)?"REINTENTAR REGISTRO":"IMPRIMIR QR "+pending.Code;}
 protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Enter&&grid.ContainsFocus){if(busy||pending!=null)return true;int i=grid.CurrentCell==null?0:grid.CurrentCell.RowIndex;grid.EndEdit();RememberPrices();Calculate();if(i<grid.Rows.Count-1){int c=(bool)grid.Rows[i+1].Tag?3:1;grid.CurrentCell=grid.Rows[i+1].Cells[c];grid.BeginEdit(true);}Mark();return true;}if(key==(Keys.Control|Keys.S)){SaveBook();return true;}return base.ProcessCmdKey(ref msg,key);}
 void NewNote(){if(busy||pending!=null)return;if(book.Notes.Count>=200){MessageBox.Show(this,"Máximo 200 notas. Respalda y elimina las que ya no uses.");return;}CaptureNote();RememberPrices();book.Notes.Add(Calc.NewNote(book,"Nota "+(book.Notes.Count+1)));book.Active=book.Notes.Count-1;BuildTabs();Mark();}
 void AddRow(){if(busy||pending!=null)return;if(grid.Rows.Count>=50){MessageBox.Show(this,"Máximo 50 filas por nota.");return;}var menu=new ContextMenuStrip();foreach(string format in Calc.Formats){string f=format;menu.Items.Add(f,null,(s,e)=>{string p="0.00";foreach(var rate in book.Rates)if(rate.Name==f)p=rate.Price;InsertRow(new RowData{Name=f,Price=p,Other=f=="Otros"});var row=grid.Rows[grid.Rows.Count-1];grid.CurrentCell=row.Cells[f=="Otros"?3:1];grid.BeginEdit(true);Mark();});}menu.Show(add,new Point(0,add.Height));}
 void Rename(){if(busy||pending!=null)return;using(var f=new Form{Text="Renombrar nota",ClientSize=new Size(300,90),StartPosition=FormStartPosition.CenterParent}){var t=new TextBox{Text=book.Notes[book.Active].Title,Dock=DockStyle.Top,MaxLength=60};var b=new Button{Text="Guardar",Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};f.Controls.Add(t);f.Controls.Add(b);f.AcceptButton=b;if(f.ShowDialog(this)==DialogResult.OK&&t.Text.Trim()!=""){book.Notes[book.Active].Title=t.Text.Trim();tabs.SelectedTab.Text=t.Text.Trim();Mark();}}}
 void NotesMenu(Control control){if(busy||pending!=null){MessageBox.Show(this,"Completa primero la emisión pendiente.");return;}var m=new ContextMenuStrip();m.Items.Add("Renombrar nota",null,(s,e)=>Rename());m.Items.Add("Eliminar fila seleccionada",null,(s,e)=>{if(grid.CurrentCell!=null&&MessageBox.Show(this,"¿Eliminar esta fila?","Fila",MessageBoxButtons.YesNo)==DialogResult.Yes){grid.Rows.RemoveAt(grid.CurrentCell.RowIndex);Calculate();Mark();}});m.Items.Add("Eliminar nota",null,(s,e)=>{if(MessageBox.Show(this,"¿Eliminar la nota y sus filas?","Nota",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;book.Notes.RemoveAt(book.Active);if(book.Notes.Count==0)book.Notes.Add(Calc.NewNote(book,"Nota 1"));book.Active=Math.Min(book.Active,book.Notes.Count-1);BuildTabs();Mark();});m.Items.Add("Guardar respaldo",null,(s,e)=>Export());m.Items.Add("Restaurar respaldo",null,(s,e)=>Import());m.Show(control,new Point(0,control.Height));}
 void Export(){if(!SaveBook())return;using(var d=new SaveFileDialog{Filter="Respaldo de notas (*.xml)|*.xml",FileName="GRAFIPLOT_Notas_"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".xml"})if(d.ShowDialog(this)==DialogResult.OK)try{Calc.WriteBackup(d.FileName,book);MessageBox.Show(this,"Respaldo guardado: incluye notas y precios.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}
 void Import(){if(busy||pending!=null)return;using(var d=new OpenFileDialog{Filter="Respaldo de notas (*.xml)|*.xml"})if(d.ShowDialog(this)==DialogResult.OK)try{var b=Calc.ReadBackup(d.FileName);if(MessageBox.Show(this,"¿Reemplazar las notas? Se conservará una copia de las actuales.","Restaurar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;if(!SaveBook())return;File.Copy(bookFile,Path.Combine(Folder,"antes-de-restaurar-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".dat"),true);book=b;BuildTabs();dirty=true;SaveBook();}catch(Exception ex){MessageBox.Show(this,"No se restauró: "+ex.Message);}}
 void OpenWeb(){System.Diagnostics.Process.Start("https://vasquezpalparoy-star.github.io/travajos-por-entregar-nuebo-junto-al-cuanta-por-llevar/");}
 protected override void SetVisibleCore(bool value){if(firstVisibility&&value){firstVisibility=false;if(Program.StartHidden){value=false;if(!IsHandleCreated)CreateHandle();}}base.SetVisibleCore(value);}
 void Place(){var r=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(Math.Max(r.Left,r.Right-Width-10),Math.Max(r.Top,r.Bottom-Height-10));}
 void ShowPanel(){Place();Show();WindowState=FormWindowState.Normal;Activate();}
 void Toggle(){if(busy)return;if(Visible){if(SaveBook())Hide();}else ShowPanel();}
 void UpdateInfo(){info.Text=(String.IsNullOrEmpty(opt.Printer)?"Elige impresora y bandeja":opt.Printer)+" · A6";}
 void UpdateAccount(){account.Text=cloud.Session==null?"Conectar cuenta":"Cuenta: "+cloud.Session.Email;}
 static Label AddLabel(Control parent,string text,int x,int y,int w,int h){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h)};parent.Controls.Add(l);return l;}
 static Button MakeButton(string text,int x,int y,int w,int h){return new RoundButton{Text=text,Location=new Point(x,y),Size=new Size(w,h),Cursor=Cursors.Hand};}
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
 Task<bool> Connect() {
  if(busy)return Task.FromResult(false);
  using(var f=new Form{Text="Conectar con tu sistema",ClientSize=new Size(385,306),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=Font,Icon=receiptIcon}) {
   AddLabel(f,"Usa el mismo correo de Trabajos por entregar.",15,12,355,35);
   AddLabel(f,"Correo",15,50,355,22);var email=new TextBox{Location=new Point(15,73),Width=355,Text=cloud.Session==null?"":cloud.Session.Email};f.Controls.Add(email);
   AddLabel(f,"Contraseña",15,110,355,22);var pass=new TextBox{Location=new Point(15,133),Width=355,UseSystemPasswordChar=true};f.Controls.Add(pass);
   var message=AddLabel(f,"La contraseña no se guarda.",15,170,355,38);message.Font=new Font("Segoe UI",8);
   var forgot=MakeButton("Recuperar contraseña",15,211,355,32);f.Controls.Add(forgot);
   forgot.Click+=(a,b)=>{try{System.Diagnostics.Process.Start("https://vasquezpalparoy-star.github.io/travajos-por-entregar-nuebo-junto-al-cuanta-por-llevar/?recuperar=1");}catch(Exception ex){message.Text="Abre la página web del sistema para recuperar tu contraseña. "+ex.Message;}};
   var button=MakeButton("Conectar",15,259,172,33);var cancel=MakeButton("Cancelar",198,259,172,33);f.Controls.Add(button);f.Controls.Add(cancel);cancel.DialogResult=DialogResult.Cancel;f.CancelButton=cancel;f.AcceptButton=button;
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
 async Task Emit(bool reprint){
  if(busy)return;
  try{
   if(reprint&&pending!=null)throw new Exception("Completa primero la emisión pendiente.");if(reprint&&last==null)throw new Exception("Todavía no hay un comprobante para reimprimir.");
   grid.EndEdit();Calculate();if(!SaveBook())throw new Exception("No se pudieron guardar las notas en esta PC.");
   if(cloud.Session==null&&!await Connect())return;
   using(var doc=PreparePrinter()){
    if(!reprint&&pending==null){CaptureNote();var n=book.Notes[book.Active];if(n.Client.Trim()=="")throw new Exception("Escribe el cliente o número.");decimal total;var lines=Calc.Lines(n,out total);if(total<=0||lines.Count==0)throw new Exception("Escribe cantidades y configura los precios, o un importe en Otros.");
     var job=new Emission{Id=Guid.NewGuid().ToString(),UserId=cloud.Session.UserId,Name=n.Client.Trim(),Total=total,Note=n.Text,NoteId=n.Id,Lines=lines};PrivateFile.Save(pendingFile,job);pending=job;
    }
    var current=reprint?last:pending;busy=true;SetMode();
    try{
     if(String.IsNullOrEmpty(current.Code)){status.Text="Guardando el trabajo y su QR…";await Task.Run(()=>cloud.Register(current));PrivateFile.Save(pendingFile,current);}else{status.Text="Verificando el registro…";await Task.Run(()=>cloud.VerifyActive(current));}
     UpdateAccount();status.Text="QR "+current.Code+" guardado · enviando a impresora…";
     int page=0;doc.DocumentName="GRAFIPLOT Notas A6 QR "+current.Code;doc.PrintPage+=(s,e)=>{Receipt.Draw(e.Graphics,e.PageBounds.Width,e.PageBounds.Height,e.PageSettings.HardMarginX,e.PageSettings.HardMarginY,current,page);page++;e.HasMorePages=page<Receipt.Pages(current);};doc.Print();
     if(!reprint){PrivateFile.Save(lastFile,current);last=current;int i=book.Notes.FindIndex(n=>n.Id==current.NoteId);if(i>=0){string title=book.Notes[i].Title;var clean=Calc.NewNote(book,title);clean.Id=current.NoteId;book.Notes[i]=clean;}book.Active=Math.Max(0,i);BuildTabs();if(!SaveBook())throw new Exception("La impresión se envió, pero no se pudo limpiar el borrador. Revisa la cola antes de repetir.");File.Delete(pendingFile);pending=null;}
     status.Text="QR "+current.Code+" guardado · enviado a la cola de impresión";
    }catch(LoginRequired ex){MessageBox.Show(this,ex.Message+" Conecta la misma cuenta y reintenta.","Conectar cuenta");}
    catch(Exception ex){MessageBox.Show(this,ex.Message+"\nEl reintento conserva el mismo trabajo. Revisa la cola de impresión antes de repetir.","Guardar e imprimir",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    finally{busy=false;SetMode();}
   }
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"GRAFIPLOT Notas A6",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }
 void SmokeUi(){var timer=new System.Windows.Forms.Timer{Interval=500};timer.Tick+=(s,e)=>{timer.Stop();try{
  name.Text="MARÍA LÓPEZ";note.Text="Entregar a las 5:00 p. m.";string[] qty={"25 + 15 + 10","2 + 1","4","1","2"};string[] price={"0.10","3.00","0.50","6.00","1.50"};
  loading=true;for(int i=0;i<5;i++){grid.Rows[i].Cells[1].Value=qty[i];grid.Rows[i].Cells[2].Value=price[i];}grid.Rows[5].Cells[3].Value="10.00";loading=false;Calculate();if(totalLabel.Text!="Total: S/ 35.00")throw new Exception("El total de pantalla no coincide.");if(grid.Right>grid.Parent.ClientSize.Width-4||grid.GetRowDisplayRectangle(5,false).Bottom>grid.ClientSize.Height||more.Bottom>more.Parent.ClientSize.Height||account.Bottom>account.Parent.ClientSize.Height)throw new Exception("Botón recortado.");
  NewNote();if(book.Notes[book.Active].Rows[0].Price!="0.10")throw new Exception("El precio no pasó a la nueva nota.");tabs.SelectedIndex=0;if(name.Text!="MARÍA LÓPEZ"||totalLabel.Text!="Total: S/ 35.00")throw new Exception("Cambiar de pestaña alteró la nota.");using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save("interfaz-prueba.png");}
  if(!SaveBook())throw new Exception("No se guardó el borrador.");
  var copy=PrivateFile.Load<Book>(bookFile);decimal total;var lines=Calc.Lines(copy.Notes[copy.Active],out total);if(total!=35||lines.Count!=6||copy.Rates[0].Price!="0.10")throw new Exception("No se conservaron precios y filas.");
 }catch(Exception ex){File.WriteAllText("error-ui.txt",ex.ToString());Environment.ExitCode=1;}finally{timer.Dispose();exit=true;Close();}};timer.Start();}
 void SmokeStartup(){var timer=new System.Windows.Forms.Timer{Interval=500};timer.Tick+=(s,e)=>{timer.Stop();try{if(Visible||!tray.Visible)throw new Exception("El inicio no quedó oculto en la bandeja.");decimal total;Calc.Lines(book.Notes[book.Active],out total);if(total!=35||name.Text!="MARÍA LÓPEZ")throw new Exception("No se recuperó el borrador tras reiniciar.");ShowPanel();if(!Visible)throw new Exception("No se abrió desde la bandeja.");}catch(Exception ex){File.WriteAllText("error-inicio.txt",ex.ToString());Environment.ExitCode=1;}finally{timer.Dispose();exit=true;Close();}};timer.Start();}
 protected override void Dispose(bool disposing){if(disposing){autosave.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}if(receiptIcon!=null)receiptIcon.Dispose();}base.Dispose(disposing);}
}
static class Receipt {
 const int PerPage=8;
 public static int Pages(Emission e){return Math.Max(1,(e.Lines.Count+PerPage-1)/PerPage);}
 public static void Draw(Graphics g,float w,float h,float hx,float hy,Emission e,int page){
  g.TranslateTransform(-hx,-hy);g.ScaleTransform(w/413f,h/583f);g.Clear(Color.White);float left=28,width=357;
  using(var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
  using(var right=new StringFormat{Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center})
  using(var ellipsis=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})
  using(var title=new Font("Arial",20,FontStyle.Bold))using(var bold=new Font("Arial",11,FontStyle.Bold))using(var normal=new Font("Arial",9))using(var small=new Font("Arial",8))using(var large=new Font("Arial",19,FontStyle.Bold))using(var pen=new Pen(Color.Black)){
   g.DrawString("GRAFIPLOT",title,Brushes.Black,new RectangleF(left,22,width,33),center);g.DrawString("COMPROBANTE DE TRABAJO",normal,Brushes.Black,new RectangleF(left,57,width,20),center);
   DateTime when;if(!DateTime.TryParse(e.Date,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out when))when=DateTime.Now;
   g.DrawString("QR "+e.Code+" · "+when.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),bold,Brushes.Black,new RectangleF(left,82,width,22),center);
   g.DrawString(e.Name,bold,Brushes.Black,new RectangleF(left,110,width,35));g.DrawLine(pen,left,149,left+width,149);
   float y=156;int start=page*PerPage,end=Math.Min(e.Lines.Count,start+PerPage);
   for(int i=start;i<end;i++){var l=e.Lines[i];g.DrawString(l.Name,normal,Brushes.Black,new RectangleF(left,y,245,16),ellipsis);g.DrawString("S/ "+Calc.F(l.Amount),normal,Brushes.Black,new RectangleF(left+245,y,112,16),right);string detail=l.Mode=="otros"?"Importe directo":l.Quantity.ToString("0",CultureInfo.InvariantCulture)+" × S/ "+Calc.F(l.Price);g.DrawString(detail,small,Brushes.Black,new RectangleF(left,y+16,240,14),ellipsis);y+=30;}
   g.DrawLine(pen,left,399,left+width,399);g.DrawString("TOTAL: S/ "+Calc.F(e.Total),large,Brushes.Black,new RectangleF(left,403,width,30),right);
   g.DrawString("A cuenta: S/ 0.00 · Saldo: S/ "+Calc.F(e.Total),small,Brushes.Black,new RectangleF(left,438,width,18),right);
   string note=e.Note??"";if(note.Length>0)g.DrawString(note,small,Brushes.Black,new RectangleF(left,461,230,72));
   ReceiptQr.Draw(g,e.Code,new RectangleF(269,459,112,112));g.DrawString("QR del trabajo",small,Brushes.Black,new RectangleF(269,566,112,12),center);
   g.DrawString("Página "+(page+1)+" de "+Pages(e),small,Brushes.Black,new RectangleF(left,548,225,15));
  }
 }
}
static class SelfTest {
 public static void Run(){
  var b=Calc.NewBook();var n=b.Notes[0];n.Client="MARÍA LÓPEZ";n.Text="Entregar a las 5:00 p. m.";
  string[] qty={"25+15+10","2+1","4","1","2"};string[] price={"0.10","3.00","0.50","6.00","1.50"};for(int i=0;i<5;i++){n.Rows[i].Quantities=qty[i];n.Rows[i].Price=price[i];}n.Rows[5].Amount="10,00";decimal total;var lines=Calc.Lines(n,out total);if(total!=35||lines.Count!=6||lines[0].Quantity!=50||lines[5].Mode!="otros")throw new Exception("Error en suma o precios.");
  foreach(string bad in new[]{"1++2","1+","-1","1.5","abc"}){bool rejected=false;try{Calc.Line(new RowData{Name="A4",Quantities=bad,Price="1.00"});}catch{rejected=true;}if(!rejected)throw new Exception("Cantidad inválida aceptada.");}
  var blank=Calc.Line(new RowData{Name="A4",Price="0"});if(blank!=null)throw new Exception("Fila vacía incluida.");
  var e=new Emission{Id=Guid.NewGuid().ToString(),UserId=Guid.NewGuid().ToString(),NoteId=n.Id,Name=n.Client,Note=n.Text,Total=total,Lines=lines,Code="027",Date=DateTime.Now.ToString("o")};
  if(CloudClient.Summary(e)!="A4: 50 | A1: 3 | A3: 4 | A0: 1 | A2: 2 | Otros: S/ 10.00 | Nota: Entregar a las 5:00 p. m.")throw new Exception("Resumen por formato incorrecto.");
  string path=Path.Combine(Path.GetTempPath(),"grafiplot-notas-test-"+Guid.NewGuid()+".xml");try{Calc.WriteBackup(path,b);Calc.WriteBackup(path,b);if(Calc.ReadBackup(path).Notes[0].Client!=n.Client)throw new Exception("Respaldo incompleto.");}finally{File.Delete(path);File.Delete(path+".bak");}
  if(Environment.OSVersion.Platform==PlatformID.Win32NT){string p=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dat");try{PrivateFile.Save(p,e);var recovered=PrivateFile.Load<Emission>(p);if(recovered.Lines.Count!=6||recovered.NoteId!=n.Id||recovered.Total!=35)throw new Exception("Recuperación incompleta.");}finally{File.Delete(p);}}
  var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();string payload=serializer.Serialize(CloudClient.Payload(e));if(!payload.Contains("A4")||!payload.Contains("otros"))throw new Exception("Detalle JSON incompleto.");
  using(var image=new Bitmap(826,1166))using(var g=Graphics.FromImage(image)){Receipt.Draw(g,826,1166,0,0,e,0);image.Save("comprobante-prueba.png");}
  for(int i=0;i<12;i++)e.Lines.Add(lines[0]);if(Receipt.Pages(e)!=3)throw new Exception("Paginación incorrecta.");for(int page=0;page<Receipt.Pages(e);page++)using(var image=new Bitmap(826,1166))using(var g=Graphics.FromImage(image)){Receipt.Draw(g,826,1166,0,0,e,page);image.Save("comprobante-largo-"+page+".png");}
  using(var writer=new StreamWriter("qr-matrices.txt"))for(int code=1;code<=500;code++){var matrix=ReceiptQr.Encode(code.ToString("D3"));writer.WriteLine(code.ToString("D3"));for(int y=0;y<21;y++){for(int x=0;x<21;x++)writer.Write(matrix[x,y]?'1':'0');writer.WriteLine();}}
  File.WriteAllText("pruebas-ok.txt","OK: formatos, precios, sumas, Otros, límites, respaldo, recuperación, paginación y 500 QR.");
 }
}
