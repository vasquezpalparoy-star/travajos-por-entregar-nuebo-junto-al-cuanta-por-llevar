create or replace function public.grafiplot_emitir_a6(
 p_id uuid, p_nombre text, p_total numeric, p_captura text default ''
) returns jsonb
language plpgsql security invoker
set search_path = ''
as $function$
declare
 v_uid uuid := auth.uid();
 v_row public.pedidos_pendientes%rowtype;
 v_code text;
 v_i integer;
begin
 if v_uid is null then raise exception 'Inicia sesión para registrar.' using errcode='28000'; end if;
 if p_id is null then raise exception 'Falta el identificador de emisión.'; end if;
 if p_total is null or p_total <= 0 or p_total > 9999999.99 or p_total <> round(p_total,2) then
  raise exception 'Monto inválido: usa soles con hasta dos decimales.';
 end if;
 if length(coalesce(p_nombre,'')) > 80 then raise exception 'El nombre admite hasta 80 caracteres.'; end if;
 if length(coalesce(p_captura,'')) > 600000 or
    (coalesce(p_captura,'') <> '' and p_captura !~ '^data:image/png;base64,[A-Za-z0-9+/=]+$') then
  raise exception 'La captura no es válida o supera el tamaño permitido.';
 end if;
 if btrim(coalesce(p_nombre,'')) = '' and coalesce(p_captura,'') = '' then raise exception 'Escribe un nombre o pega una captura.'; end if;
 perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(v_uid::text,71321));
 select * into v_row from public.pedidos_pendientes where id=p_id and user_id=v_uid;
 if found then
  if v_row.estado <> 'pendiente' then raise exception 'Este trabajo ya no está pendiente; no se puede reutilizar su QR.'; end if;
 else
  for v_i in 1..500 loop
   v_code := lpad(v_i::text,3,'0');
   if not exists(select 1 from public.pedidos_pendientes where user_id=v_uid and estado='pendiente' and codigo=v_code) then
    insert into public.pedidos_pendientes(id,user_id,nombre,telefono,items,total,pago,estado,codigo,ubicacion,descripcion,hojas)
    values(p_id,v_uid,coalesce(nullif(btrim(p_nombre),''),'Cliente (captura)'),'',
      jsonb_build_array(jsonb_build_object('name','Comprobante A6','price',p_total,'quantities',jsonb_build_array('1'),'isCustom',true,'emisor_a6',true,'captura',coalesce(p_captura,''))),
      p_total,0,'pendiente',v_code,'E1','Emitido desde GRAFIPLOT Emisor A6',1)
    on conflict do nothing returning * into v_row;
    if found then exit; end if;
   end if;
  end loop;
  if v_row.id is null then raise exception 'No hay códigos QR libres del 001 al 500. Entrega o libera un trabajo antes de emitir otro.'; end if;
 end if;
 return jsonb_build_object('id',v_row.id,'user_id',v_row.user_id,'codigo',v_row.codigo,'nombre',v_row.nombre,'total',v_row.total,'estado',v_row.estado,'guardado_en',v_row.guardado_en);
end
$function$;
revoke all on function public.grafiplot_emitir_a6(uuid,text,numeric,text) from public, anon;
grant execute on function public.grafiplot_emitir_a6(uuid,text,numeric,text) to authenticated;
comment on function public.grafiplot_emitir_a6(uuid,text,numeric,text) is 'Emisor A6 autenticado: QR activo libre 001-500, idempotente por id, conserva RLS.';

