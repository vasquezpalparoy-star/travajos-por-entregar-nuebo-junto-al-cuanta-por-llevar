-- Aplicado al proyecto CUENTAS. RLS de propietario sigue activo.
grant update (codigo, ubicacion, descripcion, hojas, pago)
  on public.pedidos_pendientes to authenticated;
