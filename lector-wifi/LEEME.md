# Grafiplot + lector Android por Wi-Fi

La APK lee QR con la **cámara trasera**, incluso al cambiar de aplicación, y envía el código al receptor de la PC. El navegador consulta el trabajo en tu sistema Grafiplot y abre su ventana con cliente, descripción, ubicación y deuda: **total menos pago a cuenta**, nunca inferior a cero.

## Instalación

1. Descarga y extrae **todo** el proyecto de la rama `integracion-qr-camara-trasera` (o el ZIP entregado). El receptor necesita `index.html` y `qr-wifi.mjs` en la carpeta superior.
2. En el paquete portable entregado, Python ya viene incluido: no necesitas instalarlo. Si descargas solo el codigo de GitHub y no tienes Python, el iniciador descarga y verifica Python portable oficial en la primera ejecucion. Necesita internet para esa descarga. Windows 8.1 o superior, en PC Intel/AMD de 32 o 64 bits.
3. Extrae **todo** el ZIP y abre `INICIAR_GRAFIPLOT.bat` en la carpeta principal (tambien sirve `lector-wifi/INICIAR_WINDOWS.bat`). Se abre Grafiplot en `http://127.0.0.1:8765/`. Deja la consola y esa pestaña abiertas. El proyecto CUENTAS y su clave pública ya están configurados. Inicia sesión con tu cuenta Grafiplot la primera vez. El mismo navegador conserva la sesión; abre siempre la dirección `http://127.0.0.1:8765/` sin modo incógnito. La configuración avanzada permite usar otro proyecto si lo necesitas.
4. En el panel **Conectar APK por Wi-Fi · cámara trasera**, despliega la dirección y la clave de conexión.
5. Instala `lector-wifi/QRClientes-Camara-Trasera.apk` en Android 10 o superior. Puedes actualizar la APK anterior sin desinstalarla; ambas versiones entregadas llevan la misma firma. Permite instalar desde la fuente donde abriste el archivo.
6. Copia dirección y clave en la APK. Pulsa **Probar conexión** y después **Iniciar lector en segundo plano**. Concede cámara y notificaciones.
7. Acerca un QR que ya use tu sistema (por ejemplo `001`) a la cámara trasera. La ventana se abre **dentro de Grafiplot en el navegador de la PC**. Cierra o acepta una consulta para ver la siguiente.

**Conexión correcta:** PC por cable **Ethernet al router**, Android por **Wi-Fi al mismo router**. También funciona si la PC utiliza Wi-Fi. No conectes el celular a una red de invitados con aislamiento. Permite TCP entrante 8765 en el Firewall de Windows **para la red privada**. No abras puertos del router hacia internet. Si la dirección mostrada corresponde a una VPN, reemplázala por la IPv4 Ethernet de `ipconfig`, conservando `:8765`.

## Registros y datos

- Las fichas se crean y actualizan en el formulario original **Registro de Trabajo**, en la tabla Supabase `pedidos_pendientes` y bajo tu usuario. El receptor no crea una segunda base de clientes.
- Se consultan los trabajos con estado `pendiente` del usuario que inició sesión. Un código libre permite pasar al formulario y asignarlo a un nuevo registro.
- La lectura de un QR no cobra, no registra un pago ni marca el trabajo como entregado. Para cambiar el pago a cuenta, edita el registro en tu sistema.
- **Necesitas internet en la PC** para consultar y guardar en Supabase y cargar las librerías de la web. La APK transmite solo el QR por la red local y no necesita acceder a Supabase.
- La APK guarda hasta 1000 lecturas sin conexión. El receptor guarda hasta 1000 lecturas pendientes en `%LOCALAPPDATA%\GrafiplotLectorWiFi\lecturas.sqlite`, incluyendo su clave local. No subas ese archivo a GitHub.
- Los reenvíos de un mismo evento no generan consultas duplicadas. La lectura se confirma después de cerrar la ventana. Si cierras o reinicias el receptor antes, se vuelve a presentar al abrir el sistema.
- Si falla la consulta de Supabase, la lectura permanece pendiente; no se muestra como código libre por un error de red. Si hay registros duplicados para el código, se informa para corregirlos.
- Deja abierta una sola pestaña receptora. La ventana es un modal del sistema; el navegador puede impedir que se ponga por encima de otros programas si está minimizado.
- Configura el mismo usuario y proyecto antes de procesar lecturas pendientes. La clave de conexión de la APK es distinta de la clave pública Supabase. Nunca uses una clave `service_role` ni `sb_secret_` en la página.

## Segundo plano en Android

Inicia el lector desde la APK mientras esté visible. Luego puedes cambiar de aplicación; se mantiene una notificación permanente con botón **Detener**. Puedes probar el bloqueo de pantalla. En ajustes de batería de QR Clientes selecciona **Sin restricciones**; algunos fabricantes tienen permisos adicionales para actividad en segundo plano.

No se garantiza funcionamiento con pantalla bloqueada en todos los modelos. Si tu celular interrumpe la cámara, utiliza **Pantalla negra**: deja la app visible, con pantalla encendida y brillo mínimo. Un teléfono completamente apagado no puede escanear. Después de reiniciar o forzar detención, abre la APK e inicia el lector. Otra app que use la cámara puede interrumpirla; el servicio intenta recuperarla. Para uso todo el día, prueba consumo y temperatura en tu celular y manténlo cargado y ventilado.

## Prueba con tus registros

Guarda un trabajo con total S/ 20 y pago a cuenta S/ 5, asignado a un código libre. Escanea el QR de ese código: debe mostrar **Debe S/ 15.00** y su descripción. Esta prueba escribe un registro real; usa datos de prueba que puedas identificar.

## Compilación y verificación

Abre `lector-wifi/android` en Android Studio con JDK 17, SDK 35 y Gradle compatible con AGP 8.7.3. Compila `app > assembleDebug`. La APK generada con otra firma no actualizará la entregada: tendrás que desinstalar la anterior. Conserva la firma de producción si vas a distribuir futuras actualizaciones.

Pruebas sin credenciales ni modificación de la nube:

```sh
python -m unittest discover -s tests -v
node --test tests/qr-wifi.test.mjs
```

Se verifica HTTP, persistencia, autenticación, restricción de la interfaz a la PC local, reenvíos, normalización de QR, deuda y consultas limitadas al usuario y a trabajos pendientes. La integración de Supabase usa respuestas simuladas en las pruebas. **No se ha probado con tu cuenta Supabase ni en un teléfono físico**. Valida la instalación y una jornada de uso antes de depender de ella.

ZXing Core 3.5.3: https://github.com/zxing/zxing (Apache-2.0).
Android servicios de cámara: https://developer.android.com/develop/background-work/services/fgs/service-types.

## Si no inicia en Windows

Abre `INICIAR_GRAFIPLOT.bat` desde la carpeta extraida, no desde dentro del ZIP. El iniciador busca primero el Python portable incluido, luego `py` y `python`. Si falta Python, prepara una copia portable sin cambiar la instalacion del sistema. Si hay un error, la consola permanece abierta con su causa. Si la consola indica que el receptor esta activo pero no se abre el navegador, entra manualmente a http://127.0.0.1:8765/.

## Conexión preconfigurada

Esta versión incluye la URL y la clave pública del proyecto CUENTAS. No contiene contraseñas ni sesiones de ningún usuario. Escribe tu correo y contraseña de Grafiplot una vez en la PC; la sesión se conserva en ese navegador mientras siga válida. En Android solo se configura la dirección local de la PC y la clave de conexión que muestra el panel Wi-Fi.

## Versión 1.4

La APK incluye vista previa de cámara trasera en una ventana flotante con permiso Mostrar sobre otras apps. Iniciar desde la app visible, luego cambiar de aplicación. Detener desde la ventana o notificación.

Windows abre una ventana nativa `TopMost` con cliente, deuda y detalle. El navegador debe quedar abierto y autenticado; un canal de eventos despierta la consulta al recibir QR y el cierre de la ventana confirma la lectura. Los datos se consultan en el sistema existente, sin cambiar permisos de Supabase. El detalle de la ventana se pasa por archivos temporales y se elimina al detener el receptor.

Validación: pruebas de receptor, autorización local, eventos, confirmación y cálculo de deuda; APK compilada y firmada. No se ha ejecutado en hardware Android ni en Windows real.

## APK 1.5

Sustituye la ventana flotante por una pantalla de cámara dentro de la APK. Abrir cámara y escanear inicia el servicio de cámara con la app visible. Seguir en segundo plano envía la tarea al fondo y separa únicamente la vista previa: continúa el análisis YUV y el envío de lecturas. Al volver, la vista previa se conecta al mismo servicio. No requiere SYSTEM_ALERT_WINDOW. La PC utiliza el receptor 1.4.1 existente.

## Versión 1.6

Envío inmediato al detectar QR, análisis cada 180 ms como máximo y lectura de novedades de ventana Windows cada 100 ms. La ficha de Windows sigue el estilo de Grafiplot y recibe campos estructurados validados. Se mantiene la consulta actual de datos al leer cada QR; no se utiliza una deuda almacenada en caché. Las cifras indican intervalos programados, no latencia total garantizada.

## APK 1.7

Reemplaza TextureView y su matriz manual por SurfaceView con proporciones conservadas. Android gestiona la rotación del buffer; la app calcula únicamente el ajuste completo de la imagen según sensor y pantalla, con márgenes negros. Se verifican 64 combinaciones de orientación y tamaño de vista y dos geometrías de referencia. No se ha probado la imagen en hardware Android real. El receptor Windows sigue en versión 1.6.


Versión 1.8: actualizar PC y APK. Ficha completa, registro nativo cuando no hay trabajo pendiente, último QR reemplaza la ficha abierta, reenvío manual y envío continuo de la cola. Consulta PASOS_ANDROID.txt.

Corrección PC 1.8.1: ficha QR flotante y minimizable en Chrome; la ventana nativa deja de mantenerse permanentemente encima de otras aplicaciones. APK 1.8 sin cambios.
