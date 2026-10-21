export function candidateCodes(text) {
    const code = String(text ?? '').trim();
    if (!code || code.length > 256) throw new Error('Código QR inválido');
    const codes = [code];
    if (/^\d{1,3}$/.test(code)) {
        const padded = String(Number(code)).padStart(3, '0');
        if (padded !== code) codes.push(padded);
    }
    return codes;
}
export function debtInSoles(total, paid) {
    const cents = Math.max(0, Math.round((Number(total) || 0) * 100) - Math.round((Number(paid) || 0) * 100));
    return (cents / 100).toFixed(2);
}
export async function findCloudRecord(api, ownerId, code) {
    if (!api || !ownerId) throw new Error('Inicia sesión en Grafiplot');
    for (const candidate of candidateCodes(code)) {
        const {data, error} = await api.from('pedidos_pendientes')
            .select('id,nombre,telefono,items,total,estado,guardado_en,codigo,ubicacion,descripcion,hojas,pago')
            .eq('user_id', ownerId).eq('estado', 'pendiente').eq('codigo', candidate).limit(2);
        if (error) throw new Error('No se pudo consultar la base de datos. La lectura sigue pendiente.');
        if (data?.length > 1) throw new Error('Hay varios trabajos con este código. Revisa sus registros antes de continuar.');
        if (data?.length === 1) return data[0];
    }
    return null;
}
export function startWifiReceiver({fetcher = fetch, canRead, isModalOpen, show, status, closeModal, register, subscribe, schedule = (fn) => setTimeout(fn, 300)}) {
    let stopped = false;
    let running = false;
    let unsubscribe;
    let confirming = null;
    async function json(path, options = {}) {
        const response = await fetcher(path, {...options, credentials:'same-origin', cache:'no-store', signal:AbortSignal.timeout(12000)});
        if (!response.ok) throw new Error('Receptor local no disponible');
        return response.json();
    }
    async function tick(reschedule = true) {
        if (running || stopped) return;
        running = true;
        try {
            if (stopped || !canRead()) return;
            if (confirming) {
                if (isModalOpen() && closeModal) {
                    const popup = await json('/popup-state');
                    if (popup.editing) return;
                    if (popup.evento === confirming && popup.action && register) {
                        const action=popup.action;
                        try {await register(action,confirming);}
                        catch(error){await json('/popup-action-ack',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({evento:confirming,id:action.id,message:error.message || 'No se pudo guardar. Reintenta.'})});}
                    }
                    if (popup.closed && popup.evento === confirming) closeModal();
                }
                if (!isModalOpen()) {
                await json('/ack', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({evento:confirming})});
                confirming = null;
                }
            }
            if (isModalOpen() && !confirming) return;
            const result = await json('/events');
            if (stopped || !canRead()) return;
            status(`${result.pendientes} lecturas pendientes · lector Wi-Fi listo`);
            const event = result.latest || result.events?.[0];
            if (event && event.evento !== confirming) {
                const shown = await show(event.codigo, event.evento);
                if (shown !== false) { confirming = event.evento; if(result.latest) await json('/ack-before',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({evento:confirming})}); }
            }
        } catch (error) { status(error.message || 'Sin conexión; lecturas pendientes'); }
        finally { running = false; if (!stopped && reschedule) schedule(() => tick()); }
    }
    if (subscribe) unsubscribe = subscribe(() => tick(false));
    tick();
    return () => { stopped = true; if (unsubscribe) unsubscribe(); };
}

export function registrationData(action,ownerId) {
 if(!ownerId || !/^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(action?.id || ''))throw new Error('Registro inválido');
 const d=action.data || {};
 const fields={nombre:200,telefono:64,descripcion:1200,ubicacion:100};const row={};
 for(const [key,max] of Object.entries(fields)){
  if(typeof d[key]!=='string' || d[key].trim().length>max)throw new Error('Revisa '+key);
  row[key]=d[key].trim();
 }
 if(!row.nombre || !row.descripcion || !row.ubicacion)throw new Error('Completa nombre, detalle y ubicación');
 row.codigo=candidateCodes(d.codigo)[0];
 for(const key of ['total','pago','hojas']){
  if(typeof d[key]!=='string' || !/^\d+(?:\.\d{1,2})?$/.test(d[key]))throw new Error('Revisa '+key);
  row[key]=Number(d[key]);
  if(!Number.isFinite(row[key]) || row[key]>1000000)throw new Error('Revisa '+key);
 }
 if(!Number.isInteger(row.hojas)||row.hojas<1||row.pago>row.total)throw new Error('Revisa hojas y pago (no puede superar el total)');
 row.nombre=row.nombre.toUpperCase();row.id=action.id;row.user_id=ownerId;row.estado='pendiente';
 row.items=[{name:row.descripcion,price:row.total,quantities:['1'],isCustom:true}];return row;
}
export async function createQrRecord(api,ownerId,action) {
 const row=registrationData(action,ownerId);
 const prior=await api.from('pedidos_pendientes').select('id').eq('user_id',ownerId).eq('id',row.id).limit(1);
 if(prior.error)throw new Error('No se pudo consultar. Reintenta cuando vuelva Internet.');
 if(prior.data?.length)return;
 const existing=await findCloudRecord(api,ownerId,row.codigo);
 if(existing)throw new Error('Este código ya tiene un trabajo pendiente. Cierra y vuelve a escanear.');
 const result=await api.from('pedidos_pendientes').insert(row);
 if(result.error)throw new Error('No se pudo guardar. Revisa tu conexión y vuelve a intentar.');
}
