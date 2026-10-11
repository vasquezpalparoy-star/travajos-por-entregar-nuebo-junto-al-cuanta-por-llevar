@echo off
title Grafiplot 1.4 - CUENTAS configurado
cd /d "%~dp0"
if not exist "lector-wifi\INICIAR_WINDOWS.bat" (
 echo Extrae TODO el ZIP. Falta la carpeta lector-wifi.
 pause
 exit /b 1
)
call "lector-wifi\INICIAR_WINDOWS.bat"
