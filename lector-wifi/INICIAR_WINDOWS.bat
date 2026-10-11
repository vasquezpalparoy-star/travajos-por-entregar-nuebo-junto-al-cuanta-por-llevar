@echo off
setlocal
cd /d "%~dp0"
title Grafiplot - Receptor QR
if not exist "puente.py" goto incompleto
if not exist "..\index.html" goto incompleto
if not exist "..\qr-wifi.mjs" goto incompleto
if exist "runtime\python.exe" goto portable
py -3 -c "import sqlite3, http.server, sys; assert sys.version_info >= (3, 10)" >nul 2>&1
if not errorlevel 1 goto lanzador
python -c "import sqlite3, http.server, sys; assert sys.version_info >= (3, 10)" >nul 2>&1
if not errorlevel 1 goto python
echo Python no esta instalado. Preparando la version portable oficial...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PREPARAR_WINDOWS.ps1"
if errorlevel 1 goto fallo
if not exist "runtime\python.exe" goto incompleto
:portable
"%~dp0runtime\python.exe" -u "%~dp0puente.py"
goto terminar
:lanzador
py -3 -u "%~dp0puente.py"
goto terminar
:python
python -u "%~dp0puente.py"
goto terminar
:incompleto
echo Faltan archivos. Extrae TODO el ZIP en una carpeta antes de abrir el programa.
goto fallo
:terminar
if errorlevel 1 goto fallo
echo El receptor se ha detenido.
pause
exit /b 0
:fallo
echo.
echo No se pudo iniciar. El mensaje anterior muestra la causa.
echo Conserva una foto de esta ventana si necesitas ayuda.
pause
exit /b 1
