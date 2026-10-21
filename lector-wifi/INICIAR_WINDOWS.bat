@echo off
setlocal
set "GRAFIPLOT_LECTOR=%~dp0"
cd /d "%GRAFIPLOT_LECTOR%"
if errorlevel 1 goto ruta
title Grafiplot 1.8.1 - Receptor QR
echo Grafiplot 1.8.1 - iniciando receptor
if not exist "puente.py" goto incompleto
if not exist "..\index.html" goto incompleto
if not exist "..\qr-wifi.mjs" goto incompleto
if exist "runtime\python.exe" goto portable
py -3 -c "import sqlite3, http.server, sys; assert sys.version_info >= (3, 10)" >nul 2>&1
if not errorlevel 1 goto lanzador
python -c "import sqlite3, http.server, sys; assert sys.version_info >= (3, 10)" >nul 2>&1
if not errorlevel 1 goto python
echo Python no esta instalado. Preparando la version portable oficial...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%GRAFIPLOT_LECTOR%PREPARAR_WINDOWS.ps1"
if errorlevel 1 goto fallo
if not exist "runtime\python.exe" goto incompleto
:portable
"%GRAFIPLOT_LECTOR%runtime\python.exe" -u "%GRAFIPLOT_LECTOR%puente.py"
goto terminar
:lanzador
py -3 -u "%GRAFIPLOT_LECTOR%puente.py"
goto terminar
:python
python -u "%GRAFIPLOT_LECTOR%puente.py"
goto terminar
:incompleto
echo Faltan archivos. Extrae TODO el ZIP en una carpeta antes de abrir el programa.
goto fallo
:ruta
echo No se pudo abrir la carpeta del programa. Extrae TODO el ZIP en una carpeta local.
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
