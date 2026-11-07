using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

public class RowData { public string Name="",Quantities="",Price="0.00",Amount=""; public bool Other; }
public class NoteData { public string Id=Guid.NewGuid().ToString(),Title="Nota",Client="",Text=""; public List<RowData> Rows=new List<RowData>(); }
public class Rate { public string Name="",Price="0.00"; }
public class Book { public int Active; public List<NoteData> Notes=new List<NoteData>(); public List<Rate> Rates=new List<Rate>(); }
public class PrintLine {
 public string Name="",Expression="",Mode="copias"; public decimal Price,Quantity,Amount; public string[] Quantities;
 public object Payload() { return new {name=Name,price=Price,quantities=Quantities,mode=Mode}; }
}
static class Calc {
 public static readonly string[] Formats={"A4","A1","A3","A0","A2","Otros"};
 public static string CompactName(string name){foreach(string format in Formats)if(name=="Copias "+format)return format;return name;}
 public static void CompactNames(Book b){foreach(var rate in b.Rates)rate.Name=CompactName(rate.Name);foreach(var note in b.Notes)foreach(var row in note.Rows)row.Name=CompactName(row.Name);}
 public static decimal Money(string s) {
  s=(s??"").Trim(); if(!Regex.IsMatch(s,@"^\d{1,7}([.,]\d{1,2})?$"))throw new Exception("Usa soles con hasta 2 decimales, sin separador de miles.");
  return decimal.Parse(s.Replace(',','.'),CultureInfo.InvariantCulture);
 }
 public static string F(decimal n) { return n.ToString("0.00",CultureInfo.InvariantCulture); }
 public static PrintLine Line(RowData r) {
  if(r.Name==null || r.Name.Trim().Length==0 || r.Name.Trim().Length>60)throw new Exception("Escribe una descripción de hasta 60 caracteres.");
  var line=new PrintLine{Name=r.Name.Trim()};
  if(r.Other) { if(String.IsNullOrWhiteSpace(r.Amount))return null; line.Mode="otros"; line.Price=Money(r.Amount);line.Quantity=1;line.Quantities=new[]{"1"};line.Amount=line.Price;if(line.Amount==0)return null;return line; }
  if(String.IsNullOrWhiteSpace(r.Quantities))return null;
  if(r.Quantities.Length>400)throw new Exception("La suma es demasiado larga.");
  string[] parts=r.Quantities.Split('+'); if(parts.Length>100)throw new Exception("Máximo 100 cantidades por fila.");
  decimal qty=0; var normalized=new List<string>();
  foreach(string part in parts) { string p=part.Trim(); if(!Regex.IsMatch(p,@"^\d{1,7}$"))throw new Exception("Cantidad: usa enteros separados por +, por ejemplo 25 + 15."); decimal n=decimal.Parse(p,CultureInfo.InvariantCulture);qty+=n;normalized.Add(n.ToString("0",CultureInfo.InvariantCulture)); }
  if(qty>9999999)throw new Exception("La cantidad supera el máximo."); if(qty==0)return null;
  decimal price=Money(r.Price);if(price<=0)throw new Exception("Configura el precio unitario de "+r.Name+".");
  decimal amount=qty*price;if(amount>9999999.99m)throw new Exception("El importe es demasiado alto.");
  line.Expression=r.Quantities;line.Price=price;line.Quantity=qty;line.Quantities=normalized.ToArray();line.Amount=amount;return line;
 }
 public static List<PrintLine> Lines(NoteData note,out decimal total) { total=0; var list=new List<PrintLine>(); foreach(var row in note.Rows){var l=Line(row);if(l!=null){list.Add(l);total+=l.Amount;}}if(total>9999999.99m)throw new Exception("El total máximo es S/ 9 999 999.99.");return list; }
 public static NoteData NewNote(Book b,string title) { var n=new NoteData{Title=title};foreach(string f in Formats){string price="0.00";foreach(var rate in b.Rates)if(rate.Name==f)price=rate.Price;n.Rows.Add(new RowData{Name=f,Price=price,Other=f=="Otros"});}return n; }
 public static Book NewBook() { var b=new Book();foreach(string f in Formats)if(f!="Otros")b.Rates.Add(new Rate{Name=f});b.Notes.Add(NewNote(b,"Nota 1"));return b; }
 public static void Validate(Book b) { if(b==null||b.Notes==null||b.Notes.Count==0||b.Notes.Count>200||b.Rates==null)throw new Exception("Respaldo inválido.");var ids=new HashSet<string>();foreach(var n in b.Notes){Guid id;if(n==null||!Guid.TryParse(n.Id,out id)||!ids.Add(n.Id)||n.Rows==null||n.Rows.Count>50||n.Client==null||n.Client.Length>80||n.Text==null||n.Text.Length>300||String.IsNullOrEmpty(n.Title)||n.Title.Length>60)throw new Exception("Nota inválida.");foreach(var r in n.Rows)if(r==null||String.IsNullOrEmpty(r.Name)||r.Name.Length>60)throw new Exception("Fila inválida.");}b.Active=Math.Max(0,Math.Min(b.Active,b.Notes.Count-1)); }
 public static Book ReadBackup(string path) { var s=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16000000};Book b;using(var r=XmlReader.Create(path,s))b=(Book)new XmlSerializer(typeof(Book)).Deserialize(r);Validate(b);return b; }
 public static void WriteBackup(string path,Book b) { string tmp=path+".tmp";using(var f=File.Create(tmp)){new XmlSerializer(typeof(Book)).Serialize(f,b);f.Flush(true);}if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path); }
}
