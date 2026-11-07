create or replace function public.grafiplot_notas_emitir_a6(
 p_id uuid, p_nombre text, p_total numeric, p_items jsonb, p_nota text default ''
) returns jsonb
language plpgsql security invoker set search_path = ''
as $function$
declare
 v_uid uuid := auth.uid(); v_row public.pedidos_pendientes%rowtype;
 v_item jsonb; v_q jsonb; v_items jsonb := '[]'::jsonb;
 v_quantities jsonb; v_n numeric; v_price numeric; v_sum numeric := 0;
 v_qty numeric; v_hojas integer := 0; v_name text; v_mode text;
 v_code text; v_i integer; v_names text := ''; v_description text;
begin
 if v_uid is null then raise exception 'Inicia sesión para registrar.' using errcode='28000'; end if;
 if p_id is null then raise exception 'Falta el identificador.'; end if;
 if btrim(coalesce(p_nombre,''))='' or length(p_nombre)>80 then raise exception 'Escribe un cliente de hasta 80 caracteres.'; end if;
 if length(coalesce(p_nota,''))>300 then raise exception 'La nota admite 300 caracteres.'; end if;
 if p_total is null or p_total<=0 or p_total>9999999.99 or p_total<>round(p_total,2) then raise exception 'Total inválido.'; end if;
 if p_items is null or jsonb_typeof(p_items)<>'array' then raise exception 'Faltan las filas.'; end if;
 if jsonb_array_length(p_items)<1 or jsonb_array_length(p_items)>50 then raise exception 'Usa de 1 a 50 filas con importe.'; end if;
 for v_item in select value from jsonb_array_elements(p_items) loop
  v_name := btrim(coalesce(v_item->>'name','')); v_mode := coalesce(v_item->>'mode','');
  if v_name='' or length(v_name)>60 or v_mode not in ('copias','otros') then raise exception 'Formato inválido.'; end if;
  if coalesce(v_item->>'price','') !~ '^\d{1,7}(\.\d{1,2})?$' then raise exception 'Precio inválido.'; end if;
  v_price := (v_item->>'price')::numeric;
  if v_price<=0 or v_price>9999999.99 then raise exception 'El precio debe ser mayor a cero.'; end if;
  if jsonb_typeof(v_item->'quantities') is distinct from 'array' then raise exception 'Cantidad inválida.'; end if;
  if jsonb_array_length(v_item->'quantities')<1 or jsonb_array_length(v_item->'quantities')>100 then raise exception 'Cantidad inválida.'; end if;
  v_qty:=0; v_quantities:='[]'::jsonb;
  for v_q in select value from jsonb_array_elements(v_item->'quantities') loop
   if (v_q #>> '{}') !~ '^\d{1,7}$' then raise exception 'Usa cantidades enteras.'; end if;
   v_n := (v_q #>> '{}')::numeric; v_qty:=v_qty+v_n;
   v_quantities:=v_quantities||jsonb_build_array(v_n::text);
  end loop;
  if v_qty<=0 or v_qty>9999999 then raise exception 'Cantidad fuera de rango.'; end if;
  if v_mode='otros' and (v_qty<>1 or jsonb_array_length(v_quantities)<>1) then raise exception 'Otros admite un importe directo.'; end if;
  v_sum:=v_sum+round(v_price*v_qty,2);
  if v_mode='copias' then v_hojas:=v_hojas+v_qty::integer; end if;
  v_items:=v_items||jsonb_build_array(jsonb_build_object('name',v_name,'price',v_price,'quantities',v_quantities,'isCustom',true,'emisor_notas_a6',true,'mode',v_mode));
  v_names:=v_names||case when v_names='' then '' else ', ' end||v_name;
 end loop;
 if v_sum<>p_total then raise exception 'El total no coincide con las filas.'; end if;
 v_description:=case when coalesce(p_nota,'')='' then v_names else p_nota||' | '||v_names end;
 perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(v_uid::text,71321));
 select * into v_row from public.pedidos_pendientes where id=p_id and user_id=v_uid for update;
 if found then
  if v_row.estado<>'pendiente' then raise exception 'El trabajo ya no está pendiente.'; end if;
  if v_row.nombre<>btrim(p_nombre) or v_row.total<>p_total or v_row.items<>v_items or v_row.descripcion<>v_description then
   raise exception 'El trabajo fue modificado. Revísalo en la web; no se creará otro registro.';
  end if;
 else
  for v_i in 1..500 loop
   v_code:=lpad(v_i::text,3,'0');
   if not exists(select 1 from public.pedidos_pendientes where user_id=v_uid and estado='pendiente' and codigo=v_code) then
    insert into public.pedidos_pendientes(id,user_id,nombre,telefono,items,total,pago,estado,codigo,ubicacion,descripcion,hojas)
    values(p_id,v_uid,btrim(p_nombre),'',v_items,p_total,0,'pendiente',v_code,'E1',v_description,greatest(v_hojas,1))
    on conflict do nothing returning * into v_row;
    if found then exit; end if;
   end if;
  end loop;
  if v_row.id is null then raise exception 'No hay QR libres del 001 al 500.'; end if;
 end if;
 return jsonb_build_object('id',v_row.id,'user_id',v_row.user_id,'codigo',v_row.codigo,'nombre',v_row.nombre,'total',v_row.total,'estado',v_row.estado,'guardado_en',v_row.guardado_en,'items',v_row.items,'descripcion',v_row.descripcion);
end
$function$;
revoke all on function public.grafiplot_notas_emitir_a6(uuid,text,numeric,jsonb,text) from public,anon;
grant execute on function public.grafiplot_notas_emitir_a6(uuid,text,numeric,jsonb,text) to authenticated;
comment on function public.grafiplot_notas_emitir_a6(uuid,text,numeric,jsonb,text) is 'Notas A6: detalle por formato, suma validada, RLS y QR idempotente; compatible con el emisor anterior.';
