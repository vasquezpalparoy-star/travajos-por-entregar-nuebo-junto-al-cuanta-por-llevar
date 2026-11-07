Option Explicit
Dim sh, fs, source, dest, app, link, startup, files, item
Set sh = CreateObject("WScript.Shell")
Set fs = CreateObject("Scripting.FileSystemObject")
source = fs.GetParentFolderName(WScript.ScriptFullName)
dest = sh.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\GrafiplotNotasA6"
app = dest & "\GRAFIPLOT_Notas_A6.exe"
If Not fs.FileExists(source & "\GRAFIPLOT_Notas_A6.exe") Then
 MsgBox "Extrae todo el ZIP antes de instalar.", 48, "GRAFIPLOT Notas A6"
 WScript.Quit 1
End If
If Not fs.FolderExists(dest) Then fs.CreateFolder dest
On Error Resume Next
files = Array("GRAFIPLOT_Notas_A6.exe", "GRAFIPLOT_Notas_A6.exe.config", "notas.ico", "LEEME.txt")
For Each item In files
 fs.CopyFile source & "\" & item, dest & "\" & item, True
 If Err.Number <> 0 Then
  MsgBox "No se pudo instalar. Si Notas A6 esta abierto, usa clic derecho en su icono > Salir y vuelve a instalar." & vbCrLf & Err.Description, 48, "GRAFIPLOT"
  WScript.Quit 1
 End If
Next
Set link = sh.CreateShortcut(sh.SpecialFolders("Desktop") & "\GRAFIPLOT Notas A6.lnk")
link.TargetPath = app
link.WorkingDirectory = dest
link.IconLocation = dest & "\notas.ico"
link.Save
startup = sh.SpecialFolders("Startup") & "\GRAFIPLOT Notas A6.lnk"
Set link = sh.CreateShortcut(startup)
link.TargetPath = app
link.Arguments = "--startup"
link.WorkingDirectory = dest
link.IconLocation = dest & "\notas.ico"
link.Save
If Err.Number <> 0 Then
 MsgBox "No se pudo crear el acceso directo: " & Err.Description, 48, "GRAFIPLOT"
 WScript.Quit 1
End If
On Error GoTo 0
sh.Run Chr(34) & app & Chr(34), 1, False
MsgBox "Instalado. Configura tus precios unitarios y conecta el mismo correo y contrasena de tu sistema de trabajos." & vbCrLf & "Se abrira automaticamente junto al reloj al iniciar Windows.", 64, "GRAFIPLOT Notas A6"
