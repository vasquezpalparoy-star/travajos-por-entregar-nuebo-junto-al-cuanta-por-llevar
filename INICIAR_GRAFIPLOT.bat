@echo off
setlocal
set "GRAFIPLOT_ROOT=%~dp0"
title Grafiplot 1.8 - CUENTAS configurado
if not exist "%GRAFIPLOT_ROOT%lector-wifi\INICIAR_WINDOWS.bat" (
 echo Extrae TODO el ZIP. Falta la carpeta lector-wifi.
 pause
 exit /b 1
)
call "%GRAFIPLOT_ROOT%lector-wifi\INICIAR_WINDOWS.bat"
