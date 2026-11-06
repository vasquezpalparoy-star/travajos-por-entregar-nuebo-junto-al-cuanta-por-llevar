Option Explicit
Dim sh, fs, source, dest, link, desktop, menu
Set sh = CreateObject("WScript.Shell")
Set fs = CreateObject("Scripting.FileSystemObject")
source = fs.GetParentFolderName(WScript.ScriptFullName)
dest = sh.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\GrafiplotEmisorA6"
On Error Resume Next
If Not fs.FolderExists(dest) Then fs.CreateFolder dest
fs.CopyFile source & "\GRAFIPLOT_Emisor_A6.exe", dest & "\GRAFIPLOT_Emisor_A6.exe", True
fs.CopyFile source & "\recibo.ico", dest & "\recibo.ico", True
fs.CopyFile source & "\GRAFIPLOT_Emisor_A6.exe.config", dest & "\GRAFIPLOT_Emisor_A6.exe.config", True
If Err.Number <> 0 Then
 MsgBox "No se pudo instalar. Si el programa esta abierto, usa clic derecho en su icono y Salir. Luego intenta nuevamente." & vbCrLf & Err.Description, 16, "GRAFIPLOT"
 WScript.Quit 1
End If
On Error GoTo 0
desktop = sh.SpecialFolders("Desktop")
Set link = sh.CreateShortcut(desktop & "\GRAFIPLOT Emisor A6.lnk")
link.TargetPath = dest & "\GRAFIPLOT_Emisor_A6.exe"
link.WorkingDirectory = dest
link.IconLocation = dest & "\recibo.ico"
link.Save
menu = sh.SpecialFolders("Programs")
Set link = sh.CreateShortcut(menu & "\GRAFIPLOT Emisor A6.lnk")
link.TargetPath = dest & "\GRAFIPLOT_Emisor_A6.exe"
link.WorkingDirectory = dest
link.IconLocation = dest & "\recibo.ico"
link.Save
MsgBox "Instalacion terminada. Se creo el acceso directo en el escritorio." & vbCrLf & "Elige tu HP 9020 y la bandeja A6 en Configuracion.", 64, "GRAFIPLOT"
sh.Run Chr(34) & dest & "\GRAFIPLOT_Emisor_A6.exe" & Chr(34), 1, False
