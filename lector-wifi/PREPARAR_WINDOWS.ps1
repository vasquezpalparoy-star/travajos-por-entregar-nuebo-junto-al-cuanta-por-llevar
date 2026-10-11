$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$runtime = Join-Path $PSScriptRoot 'runtime'
$archive = Join-Path $PSScriptRoot 'python-portable.zip'
$staging = Join-Path $PSScriptRoot 'runtime-preparando'
$expectedHash = '0a3b162281b0a8e4d95d748e40ec2db9927086c7ec4231a5c8d29f91a1c2ba89'
try {
    if (Test-Path (Join-Path $runtime 'python.exe')) { exit 0 }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Host 'Descargando Python portable oficial. Solo se guarda en esta carpeta.'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://www.python.org/ftp/python/3.13.16/python-3.13.16-embed-win32.zip' -OutFile $archive
    $actualHash = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) { throw 'La descarga no coincide con el archivo oficial. Vuelve a intentar.' }
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    Expand-Archive -Path $archive -DestinationPath $staging
    if (-not (Test-Path (Join-Path $staging 'python.exe'))) { throw 'Falta python.exe en la descarga.' }
    & (Join-Path $staging 'python.exe') -c 'import sqlite3, http.server, webbrowser; print("Python portable preparado")'
    if ($LASTEXITCODE -ne 0) { throw 'Python no se pudo ejecutar en este Windows. Requiere Windows 8.1 o superior.' }
    if (Test-Path $runtime) { Remove-Item -Recurse -Force $runtime }
    Move-Item -Path $staging -Destination $runtime
    Remove-Item -Force $archive
    exit 0
} catch {
    Write-Host ('No se pudo preparar el programa: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Usa el ZIP portable completo o comprueba tu conexion a Internet.'
    exit 1
}
