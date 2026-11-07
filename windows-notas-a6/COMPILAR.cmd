@echo off
setlocal
cd /d "%~dp0"
set "COMPILADOR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%COMPILADOR%" set "COMPILADOR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%COMPILADOR%" /nologo /target:winexe /platform:anycpu /optimize+ /out:GRAFIPLOT_Notas_A6.exe /win32icon:notas.ico /resource:notas.ico,notas.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll Notas.cs Models.cs Cloud.cs ReceiptQr.cs Style.cs
if errorlevel 1 (echo Error de compilacion.) else (echo Compilacion terminada.)
pause
