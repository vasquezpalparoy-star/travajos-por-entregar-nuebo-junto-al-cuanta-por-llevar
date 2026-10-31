const fs=require('fs');
const parser=require('@babel/parser');
(async()=>{
 const html=fs.readFileSync('index.html','utf8');
 const app=html.match(/<script type="module">([\s\S]*?)<\/script>/)[1];
 parser.parse(app,{sourceType:'module'});
 const bridge=fs.readFileSync('service-bridge.html','utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
 parser.parse(bridge,{sourceType:'script'});
 const response=await fetch('https://raw.githubusercontent.com/vasquezpalparoy-star/llevado-de-cuentas-actulisble-a-travajos-por-recoger/ac5e04fdf4be849c88b3a260d1fb509e8a740647/index.html');
 if(!response.ok)throw new Error('No se pudo leer la página PC');
 const pc=await response.text();
 const scripts=[...pc.matchAll(/<script[^>]*type="text\/babel"[^>]*>([\s\S]*?)<\/script>/g)];
 if(!scripts.length)throw new Error('No se encontró la interfaz PC');
 for(const script of scripts)parser.parse(script[1],{sourceType:'script',plugins:['jsx']});
 console.log('PASS: sintaxis web, puente de servicio y formulario React de PC');
})().catch(error=>{console.error(error);process.exit(1);});
