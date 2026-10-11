package com.qrclientes;
import android.content.*;
import android.database.*;
import android.database.sqlite.*;
import java.util.UUID;
class PendingDb extends SQLiteOpenHelper {
 PendingDb(Context c){super(c,"lecturas.db",null,1);}
 public void onCreate(SQLiteDatabase d){d.execSQL("CREATE TABLE pendientes(id TEXT PRIMARY KEY,codigo TEXT NOT NULL,fecha INTEGER NOT NULL)");}
 public void onUpgrade(SQLiteDatabase d,int a,int b){}
 synchronized boolean add(String codigo){
  SQLiteDatabase d=getWritableDatabase();try(Cursor c=d.rawQuery("SELECT COUNT(*) FROM pendientes",null)){c.moveToFirst();if(c.getInt(0)>=1000)return false;}
  ContentValues v=new ContentValues();v.put("id",UUID.randomUUID().toString());v.put("codigo",codigo);v.put("fecha",System.currentTimeMillis());d.insertOrThrow("pendientes",null,v);return true;
 }
 synchronized String[] first(){try(Cursor c=getReadableDatabase().rawQuery("SELECT id,codigo FROM pendientes ORDER BY fecha,rowid LIMIT 1",null)){return c.moveToFirst()?new String[]{c.getString(0),c.getString(1)}:null;}}
 synchronized void remove(String id){getWritableDatabase().delete("pendientes","id=?",new String[]{id});}
}
