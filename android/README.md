# Grafiplot Android

Aplicación instalable para Android 8.0 o posterior. Conserva las funciones del index.html del repositorio: registro y edición de trabajos, pagos y saldo, búsqueda por cliente/celular/código, escáner QR, contacto por WhatsApp, tickets PDF e impresión, Excel, 100 etiquetas QR y eliminación con confirmación.

En la versión 1.1 la cámara QR se inicia automáticamente después de recuperar la sesión y cargar los registros, y al volver a la aplicación. Se detiene al salir de la app, cambiar de sección, cerrar sesión o leer un QR. Android solicita el permiso de cámara la primera vez.

La conexión al proyecto CUENTAS ya está incluida. El usuario solo introduce su correo y contraseña de Grafiplot; la sesión se conserva en el almacenamiento privado de la aplicación. Necesita Internet para consultar y modificar datos de Supabase. Bibliotecas, estilos e iconos vienen incluidos en la APK.

## Compilación

Instalar Java 17, Android SDK Platform 35 y Build Tools 35.0.0. Definir ANDROID_HOME, GRAFIPLOT_KEYSTORE y GRAFIPLOT_KEY_PASS; ejecutar `python3 download-vendor.py` y `./build-apk.sh`. El alias del certificado es `grafiplot`. La APK se genera en `build/Grafiplot.apk`.

La clave de firma y su contraseña se entregan como copia privada de recuperación y no deben subirse al repositorio. Usar el mismo certificado para futuras actualizaciones. Incrementar versionCode en AndroidManifest.xml antes de distribuir otra versión.

## Configuración

`app/src/main/assets/web/config.js` contiene exclusivamente la URL y la clave pública publishable; no contiene contraseñas, service_role ni claves secretas. Los pedidos se filtran por el usuario autenticado y la base de datos aplica políticas RLS de propietario.

Para permitir las ediciones que ya hace el formulario, se añadieron permisos UPDATE únicamente sobre `codigo, ubicacion, descripcion, hojas, pago` al rol authenticated de pedidos_pendientes. No se modificaron registros ni políticas RLS.

## Dependencias incluidas

Tailwind CSS 3.4.17 (CSS compilado), html5-qrcode 2.3.8, jsPDF 2.5.1, QRCode 1.5.1, JsBarcode 3.11.5, SheetJS 0.18.5 y supabase-js 2.57.4. Los archivos vendor conservan los avisos de licencia que proporcionan los distribuidores.

## Validación

Pruebas con datos simulados verifican crear/editar/eliminar trabajos, navegación, ticket PDF, envío al sistema de impresión, Excel y PDF de 100 QR. La compilación y la firma APK v2/v3 están verificadas. Permisos autenticados y políticas RLS comprobados en CUENTAS. El endpoint de autenticación acepta la clave pública integrada. La cámara, impresión y selector de guardado requieren una prueba final en un dispositivo Android; este entorno no dispone de dispositivo/emulador.
