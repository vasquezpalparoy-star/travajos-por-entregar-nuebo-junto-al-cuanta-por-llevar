@echo off
cd /d "%~dp0"
py -3 puente.py
if errorlevel 1 (
 echo Instala Python 3 para Windows con el lanzador py, o cierra otro receptor en el puerto 8765.
 pause
)
