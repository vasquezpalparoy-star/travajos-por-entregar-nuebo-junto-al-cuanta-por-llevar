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
export function startWifiReceiver({fetcher = fetch, canRead, isModalOpen, show, status, schedule = (fn) => setTimeout(fn, 1200)}) {
    let stopped = false;
    let confirming = null;
    async function json(path, options = {}) {
        const response = await fetcher(path, {...options, credentials:'same-origin', cache:'no-store', signal:AbortSignal.timeout(12000)});
        if (!response.ok) throw new Error('Receptor local no disponible');
        return response.json();
    }
    async function tick() {
        try {
            if (stopped || !canRead()) return;
            if (confirming) {
                if (isModalOpen()) return;
                await json('/ack', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({evento:confirming})});
                confirming = null;
            }
            if (isModalOpen()) return;
            const result = await json('/events');
            if (stopped || !canRead()) return;
            status(`${result.pendientes} lecturas pendientes · lector Wi-Fi listo`);
            const event = result.events?.[0];
            if (event) {
                const shown = await show(event.codigo);
                if (shown !== false) confirming = event.evento;
            }
        } catch (error) { status(error.message || 'Sin conexión; lecturas pendientes'); }
        finally { if (!stopped) schedule(tick); }
    }
    tick();
    return () => { stopped = true; };
}
