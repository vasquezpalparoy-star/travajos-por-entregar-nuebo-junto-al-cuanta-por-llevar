using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Theme {
 public static readonly Color Ink=Color.FromArgb(9,24,61),Blue=Color.FromArgb(0,66,201),Line=Color.FromArgb(185,199,225),Paper=Color.FromArgb(246,248,254);
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<1){p.AddRectangle(r);return p;}p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static void Fill(Graphics g,Rectangle r,Color a,Color b,int radius){if(r.Width<1||r.Height<1)return;g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Round(r,radius))using(var brush=new LinearGradientBrush(r,a,b,90f))g.FillPath(brush,p);}
 public static void Border(Graphics g,Rectangle r,Color color,int radius){using(var p=Round(r,radius))using(var pen=new Pen(color))g.DrawPath(pen,p);}
 public static void Printer(Graphics g,Rectangle r,Color color){float k=r.Width/30f;var save=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(k,k);using(var p=new Pen(color,1.8f)){g.DrawRectangle(p,7,1,16,8);g.DrawRectangle(p,2,10,26,14);g.DrawRectangle(p,7,18,16,11);g.DrawLine(p,22,14,24,14);}g.Restore(save);}
}
class RoundButton:Button {
 bool hover;
 public RoundButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;ForeColor=Theme.Blue;BackColor=Theme.Paper;Font=new Font("Segoe UI",10,FontStyle.Regular);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;if(Parent is TitleBar){g.Clear(Color.FromArgb(4,27,76));TextRenderer.DrawText(g,Text,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}g.Clear(Parent==null?Theme.Paper:Parent.BackColor);bool primary=Text.StartsWith("GUARDAR E IMPRIMIR")||Text.StartsWith("IMPRIMIR QR")||Text.StartsWith("REINTENTAR");var r=new Rectangle(1,1,Width-3,Height-3);Color top=primary?Color.FromArgb(0,100,244):Color.White,bottom=primary?Color.FromArgb(0,42,173):Color.FromArgb(229,239,255);if(hover&&Enabled){top=primary?Color.FromArgb(39,127,255):Color.FromArgb(227,239,255);}if(!Enabled){top=bottom=Color.FromArgb(225,229,238);}Theme.Fill(g,r,top,bottom,9);Theme.Border(g,r,primary?Theme.Blue:Color.FromArgb(88,121,215),9);Color text=Enabled?(primary?Color.White:ForeColor):Color.Gray;var tr=new Rectangle(4,2,Width-8,Height-4);
  if(primary&&Width>300){int x=Math.Max(22,Width/2-190);Theme.Printer(g,new Rectangle(x,Height/2-15,30,30),text);using(var p=new Pen(Color.FromArgb(170,220,255)))g.DrawLine(p,x+48,9,x+48,Height-9);tr=new Rectangle(x+58,2,Width-x-72,Height-4);}
  TextRenderer.DrawText(g,Text,Font,tr,text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-11,Height-11),text,Color.Transparent);
 }
}
class RoundField:Panel {
 public RoundField(TextBox input,bool multiline){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);BackColor=Theme.Paper;Padding=multiline?new Padding(9,5,9,5):new Padding(10,5,10,4);input.BorderStyle=BorderStyle.None;input.BackColor=Color.White;input.Font=new Font("Segoe UI",12);input.Dock=DockStyle.Fill;Controls.Add(input);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=new Rectangle(1,1,Width-3,Height-3);Theme.Fill(e.Graphics,r,Color.White,Color.FromArgb(252,253,255),8);Theme.Border(e.Graphics,r,Color.FromArgb(147,164,202),8);}
}
class TotalBadge:Label {
 public TotalBadge(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Paper:Parent.BackColor);var r=new Rectangle(1,2,Width-3,Height-5);Theme.Fill(e.Graphics,r,Color.FromArgb(216,248,197),Color.FromArgb(194,237,164),10);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(8,1,Width-16,Height-2),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);}
}
class BlueTabs:TabControl {
 public BlueTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);DrawMode=TabDrawMode.OwnerDrawFixed;SizeMode=TabSizeMode.Fixed;ItemSize=new Size(128,30);Font=new Font("Segoe UI",12,FontStyle.Bold);Padding=new Point(10,4);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Paper);for(int i=0;i<TabCount;i++)OnDrawItem(new DrawItemEventArgs(e.Graphics,Font,GetTabRect(i),i,i==SelectedIndex?DrawItemState.Selected:DrawItemState.Default));}
 protected override void OnDrawItem(DrawItemEventArgs e){bool selected=e.Index==SelectedIndex;var r=GetTabRect(e.Index);r.Inflate(-1,0);Theme.Fill(e.Graphics,r,selected?Color.FromArgb(182,213,255):Color.FromArgb(243,245,250),selected?Color.FromArgb(151,194,252):Color.FromArgb(225,231,243),9);Theme.Border(e.Graphics,new Rectangle(r.X,r.Y,r.Width-1,r.Height-1),Color.FromArgb(174,193,225),9);TextRenderer.DrawText(e.Graphics,TabPages[e.Index].Text,Font,r,Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
}
class TitleBar:Panel {
 Icon icon;Form form;
 [DllImport("user32.dll")] static extern bool ReleaseCapture();[DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,int w,int l);
 public TitleBar(Form owner,Icon appIcon){form=owner;icon=appIcon;DoubleBuffered=true;BackColor=Color.FromArgb(5,28,78);Dock=DockStyle.Fill;Margin=Padding.Empty;
  var close=new RoundButton{Text="×",Dock=DockStyle.Right,Width=42,Font=new Font("Segoe UI",23),ForeColor=Color.White};var hide=new RoundButton{Text="−",Dock=DockStyle.Right,Width=42,Font=new Font("Segoe UI",23),ForeColor=Color.White};close.BackColor=hide.BackColor=BackColor;close.Click+=(s,e)=>form.Close();hide.Click+=(s,e)=>form.Close();Controls.Add(hide);Controls.Add(close);
  // Header buttons draw directly on the navy strip.
  close.Paint+=(s,e)=>{e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,"×",close.Font,close.ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);};hide.Paint+=(s,e)=>{e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,"−",hide.Font,hide.ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);};
  MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(form.Handle,0xA1,2,0);}};
 }
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(6,34,91),Color.FromArgb(2,23,66),0f))e.Graphics.FillRectangle(b,ClientRectangle);e.Graphics.DrawIcon(icon,new Rectangle(14,9,30,30));using(var f=new Font("Segoe UI",17,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"GRAFIPLOT Notas A6",f,new Rectangle(53,0,Width-145,Height),Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}
}
