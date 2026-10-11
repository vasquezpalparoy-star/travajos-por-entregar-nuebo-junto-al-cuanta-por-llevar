import test from 'node:test';
import assert from 'node:assert/strict';
import {candidateCodes, debtInSoles, findCloudRecord, startWifiReceiver} from '../qr-wifi.mjs';
test('QR numérico mantiene código exacto y admite 1 -> 001', () => {
 assert.deepEqual(candidateCodes(' 1 '), ['1','001']);assert.deepEqual(candidateCodes('001'),['001']);assert.deepEqual(candidateCodes('C1'),['C1']);
 assert.throws(()=>candidateCodes(''));assert.throws(()=>candidateCodes('x'.repeat(257)));
});
test('deuda usa total menos pago a cuenta con centavos',()=>{
 assert.equal(debtInSoles(20,5),'15.00');assert.equal(debtInSoles(20,25),'0.00');assert.equal(debtInSoles(0.3,0.1),'0.20');
});
function fakeApi(responses) {
 const calls=[];return {calls,from(table){const filters=[];const q={select(fields){calls.push({table,fields,filters});return q},eq(k,v){filters.push([k,v]);return q},limit(n){assert.equal(n,2);return Promise.resolve(responses.shift())}};return q}};
}
test('consulta solo los trabajos pendientes de la cuenta activa; consulta dato actual',async()=>{
 const row={id:'job',codigo:'001',total:20,pago:5};const api=fakeApi([{data:[],error:null},{data:[row],error:null}]);
 assert.equal(await findCloudRecord(api,'owner-123','1'),row);
 for(const c of api.calls){assert.equal(c.table,'pedidos_pendientes');assert.deepEqual(c.filters.slice(0,2),[['user_id','owner-123'],['estado','pendiente']]);}
 assert.equal(api.calls[1].filters[2][1],'001');
});
test('error de nube y códigos duplicados no se convierten en código libre',async()=>{
 await assert.rejects(findCloudRecord(fakeApi([{data:null,error:{message:'offline'}}]),'owner','001'));
 await assert.rejects(findCloudRecord(fakeApi([{data:[{},{}],error:null}]),'owner','001'));
 await assert.rejects(findCloudRecord(null,null,'001'));
});
test('lectura se confirma al cerrar la ventana y no antes',async()=>{
 let next,open=false;const requests=[];const shown=[];
 const fetcher=async(path,options)=>{requests.push([path,options]);return {ok:true,json:async()=>path==='/events'?{pendientes:1,events:[{evento:'e1',codigo:'001'}]}:{ok:true}}};
 const stop=startWifiReceiver({fetcher,canRead:()=>true,isModalOpen:()=>open,show:async code=>{shown.push(code);open=true;},status:()=>{},schedule:fn=>next=fn});
 await new Promise(setImmediate);assert.deepEqual(shown,['001']);assert.equal(requests.filter(r=>r[0]==='/ack').length,0);
 await next();assert.equal(requests.filter(r=>r[0]==='/ack').length,0);open=false;await next();
 assert.equal(requests.filter(r=>r[0]==='/ack').length,1);stop();
});
test('fallo al consultar nube mantiene evento pendiente sin ack',async()=>{
 let next;const requests=[];let error;
 const stop=startWifiReceiver({fetcher:async path=>{requests.push(path);return {ok:true,json:async()=>({pendientes:1,events:[{evento:'e',codigo:'001'}]})}},canRead:()=>true,isModalOpen:()=>false,show:async()=>{throw new Error('offline')},status:s=>error=s,schedule:fn=>next=fn});
 await new Promise(setImmediate);assert.equal(error,'offline');assert.deepEqual(requests,['/events']);stop();
});
test('cerrar ventana Windows confirma lectura y despierta sin depender del temporizador',async()=>{
 let next,wake,open=false,delivered=false,closed=false,unsubscribed=false;const requests=[];
 const fetcher=async(path)=>{requests.push(path);return {ok:true,json:async()=>path==='/events'?{pendientes:delivered?0:1,events:delivered?[]:[{evento:'native',codigo:'003'}]}:path==='/popup-state'?{closed,evento:'native'}:(delivered=true,{ok:true})}};
 const stop=startWifiReceiver({fetcher,canRead:()=>true,isModalOpen:()=>open,show:async(code,event)=>{assert.equal(event,'native');open=true;},closeModal:()=>open=false,status:()=>{},schedule:fn=>next=fn,subscribe:fn=>{wake=fn;return ()=>unsubscribed=true}});
 await new Promise(setImmediate);await wake();assert.equal(requests.includes('/ack'),false);
 closed=true;await wake();assert.equal(open,false);assert.equal(requests.filter(x=>x==='/ack').length,1);
 stop();assert.equal(unsubscribed,true);
});
