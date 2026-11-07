using System;
using System.Collections.Generic;
using System.Drawing;
// QR Code Model 2, version 1, byte mode, ECC M, mask 0. For codes 001-500 only.
// Reed-Solomon over GF(256), primitive polynomial 0x11D.
static class ReceiptQr {
 static int Multiply(int x,int y){int z=0;for(int i=7;i>=0;i--){z=(z<<1)^(((z>>7)&1)*0x11D);z^=((y>>i)&1)*x;}return z;}
 static void Bits(List<bool> bits,int value,int count){for(int i=count-1;i>=0;i--)bits.Add(((value>>i)&1)!=0);}
 public static bool[,] Encode(string code) {
  int value;if(code==null||code.Length!=3||!int.TryParse(code,out value)||value<1||value>500||code!=value.ToString("D3"))throw new Exception("QR fuera del rango 001-500.");
  var bits=new List<bool>();Bits(bits,4,4);Bits(bits,3,8);foreach(char c in code)Bits(bits,c,8);Bits(bits,0,4);while(bits.Count%8!=0)bits.Add(false);
  var words=new List<int>();for(int i=0;i<bits.Count;i+=8){int b=0;for(int j=0;j<8;j++)b=(b<<1)|(bits[i+j]?1:0);words.Add(b);}int pad=0;while(words.Count<16)words.Add((pad++%2)==0?0xEC:0x11);
  int[] divisor=new int[10];divisor[9]=1;int root=1;
  for(int i=0;i<10;i++){for(int j=0;j<10;j++){divisor[j]=Multiply(divisor[j],root);if(j+1<10)divisor[j]^=divisor[j+1];}root=Multiply(root,2);}
  int[] ecc=new int[10];foreach(int b in words){int factor=b^ecc[0];Array.Copy(ecc,1,ecc,0,9);ecc[9]=0;for(int j=0;j<10;j++)ecc[j]^=Multiply(divisor[j],factor);}words.AddRange(ecc);
  var modules=new bool[21,21];var function=new bool[21,21];
  Action<int,int,bool> set=(x,y,dark)=>{modules[x,y]=dark;function[x,y]=true;};
  for(int i=0;i<21;i++){set(6,i,i%2==0);set(i,6,i%2==0);}
  foreach(var point in new[]{new Point(3,3),new Point(17,3),new Point(3,17)})for(int dy=-4;dy<=4;dy++)for(int dx=-4;dx<=4;dx++){int x=point.X+dx,y=point.Y+dy;if(x>=0&&x<21&&y>=0&&y<21){int d=Math.Max(Math.Abs(dx),Math.Abs(dy));set(x,y,d!=2&&d!=4);}}
  int format=0x5412;
  for(int i=0;i<=5;i++)set(8,i,((format>>i)&1)!=0);set(8,7,((format>>6)&1)!=0);set(8,8,((format>>7)&1)!=0);set(7,8,((format>>8)&1)!=0);
  for(int i=9;i<15;i++)set(14-i,8,((format>>i)&1)!=0);for(int i=0;i<8;i++)set(20-i,8,((format>>i)&1)!=0);for(int i=8;i<15;i++)set(8,6+i,((format>>i)&1)!=0);set(8,13,true);
  int index=0;
  for(int right=20;right>=1;right-=2){if(right==6)right=5;for(int vert=0;vert<21;vert++){int y=((right+1)&2)==0?20-vert:vert;for(int j=0;j<2;j++){int x=right-j;if(function[x,y])continue;bool dark=((words[index>>3]>>(7-(index&7)))&1)!=0;modules[x,y]=dark^((x+y)%2==0);index++;}}}
  if(index!=208)throw new Exception("Longitud QR inválida.");return modules;
 }
 public static void Draw(Graphics g,string code,RectangleF area) {
  var m=Encode(code);float unit=area.Width/29f;g.FillRectangle(Brushes.White,area);
  for(int y=0;y<21;y++)for(int x=0;x<21;x++)if(m[x,y])g.FillRectangle(Brushes.Black,area.Left+(x+4)*unit,area.Top+(y+4)*unit,unit,unit);
 }
}

