# Grafiplot: trabajos pendientes y pago a cuenta

Sistema de registro en Supabase con códigos QR, clientes, detalles, ubicación, total y pago a cuenta.

## Lector Android por Wi-Fi

La APK utiliza la **cámara trasera** en segundo plano. El receptor Windows abre este mismo sistema y muestra el trabajo correspondiente al QR, con su deuda pendiente. La PC puede estar conectada **por Ethernet al router** y el Android **por Wi-Fi al mismo router**.

- Inicia la PC con `INICIAR_GRAFIPLOT.bat`. El paquete portable incluye Python; al descargar solo el codigo, el iniciador lo prepara automaticamente si hace falta.
- Instala `lector-wifi/QRClientes-Camara-Trasera.apk` en Android 10 o superior.
- Inicia sesión en la página local con tu proyecto y cuenta Supabase existentes.
- Copia dirección y clave del panel **Conectar APK por Wi-Fi** a la APK.
- Pulsa **Iniciar lector en segundo plano** y presenta el QR a la cámara trasera.

Consulta [instalación, requisitos y pruebas](lector-wifi/LEEME.md). La PC necesita internet para Supabase. Las ventanas de consulta aparecen dentro del navegador. Las lecturas no cambian pagos ni entregan trabajos automáticamente.
