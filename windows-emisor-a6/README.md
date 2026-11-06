# GRAFIPLOT Emisor A6 con QR

Utilidad de Windows para registrar trabajos y emitir comprobantes A6 en negro desde la bandeja del sistema. Acepta texto o capturas del portapapeles; la captura se estira en el cuadro del cliente. El QR mantiene su proporción y contiene el código del trabajo.

## Configuración

Compilar con `COMPILAR.cmd` en Windows con .NET Framework 4.5 o posterior. Abrir `INSTALAR.vbs` para instalar por usuario. Seleccionar impresora y bandeja, y conectar el mismo usuario de la web. No se necesitan claves administrativas. El cliente usa la API pública del proyecto configurado en `Cloud.cs` con sesión de usuario y RLS.

En HP OfficeJet Pro 9020, usar la bandeja 2 para A6 (105 × 148 mm). La prueba física debe hacerse en esa impresora.

## Registro

La función `grafiplot_emitir_a6` reserva un código libre 001–500 e inserta en `pedidos_pendientes`. Conserva el mismo UUID ante reintentos, consulta solamente registros del usuario autenticado y utiliza el índice único de códigos pendientes existente. No se guardan registros si falta autenticación. La aplicación imprime después de confirmar el registro.

El monto es el precio total; el pago a cuenta queda en 0. Teléfono vacío, ubicación E1 y una hoja son valores iniciales editables en la web. Una captura se conserva en `items[0].captura`, con marcador `emisor_a6`, formato PNG y límite de 600000 caracteres. La contraseña nunca se guarda; sesión y recuperación se cifran con DPAPI para el usuario actual de Windows.

## Recuperación y reimpresión

Si falla la red, `emision-pendiente.dat` conserva la emisión. El botón reintenta ese UUID, sin crear otro pedido. Tras enviar a la cola, el último comprobante queda disponible desde el menú del icono. Reimprimir verifica que el pedido siga pendiente, con el mismo QR, nombre y monto. Si fue entregado, borrado o modificado, se solicita usar la web. Revisar la cola antes de repetir: Windows puede aceptar un trabajo aunque la impresora esté desconectada.

## Pruebas

`GRAFIPLOT_Emisor_A6.exe --self-test` comprueba importes, serialización, render de texto/capturas, DPAPI en Windows y genera 500 matrices QR. `--ui-smoke-test` prueba el portapapeles, ambos modos y dibuja las ventanas sin imprimir ni conectar una cuenta. El flujo de GitHub Actions compila y ejecuta ambas pruebas en Windows, compara los 500 QR con qrcode 8.2 y lee el QR del recibo con zxing-cpp.

La función de base de datos se verificó con transacciones revertidas: autenticación, registro, idempotencia, captura, montos, código 500 y rechazo cuando los 500 están ocupados. La referencia SQL está en `database/grafiplot_emitir_a6.sql`. No ejecutar las pruebas de base de datos dejando sus datos confirmados.
