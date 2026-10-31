# GRAFIPLOT Trabajos QR para Android

APK para Android 8 o superior. Requiere Internet y permiso de cámara.
Inicia sesión con tu cuenta Grafiplot. Después de cargar los trabajos, la cámara se abre automáticamente. Acerca un QR para ver su ficha; pulsa Aceptar para continuar escaneando.
La pantalla permanece encendida mientras usas la app. Al minimizarla o bloquear el teléfono se detiene la cámara; al regresar se reactiva.
Esta versión no escanea con la app cerrada ni abre la app desde segundo plano.
El flujo de recuperación se abre en el navegador; tras cambiar la contraseña, vuelve a la APK e inicia sesión.
Los datos se guardan en Supabase con permisos por usuario.
Compilación: Gradle 8.11.1, JDK 17, SDK 35. Ejecutar gradle assembleDebug lintDebug desde android/.
El instalador de esta primera versión usa firma de pruebas; para distribuir actualizaciones comerciales se debe configurar una firma privada permanente.
