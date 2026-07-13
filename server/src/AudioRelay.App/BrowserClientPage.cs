namespace AudioRelay.App;

/// <summary>
/// Built-in browser client served at GET /. Same SDP flow as before (server offers, page answers),
/// but the received WebRTC audio is routed through a Web Audio AnalyserNode to drive a
/// rhythm-particle canvas (bass-impact reactive), matching the Avalonia/Flutter particle effect.
/// Exposes window.__ar_conn / window.__ar_track for automated checking. Same-origin, no CORS.
/// </summary>
public static class BrowserClientPage
{
    public const string Html = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Audio Relay</title>
<style>
body{margin:0;overflow:hidden;background:#050510;font-family:-apple-system,system-ui,sans-serif;color:#fff}
canvas{display:block;position:fixed;inset:0;z-index:0}
#controls{position:fixed;top:20px;left:20px;z-index:10;background:rgba(255,255,255,0.08);padding:16px 20px;border-radius:14px;backdrop-filter:blur(6px)}
#controls h2{margin:0 0 6px;font-weight:600;font-size:16px}
#controls p{margin:0 0 12px;color:#8E8E93;font-size:12px}
input{font-size:15px;padding:8px;border-radius:8px;border:1px solid #3A3A3C;background:#2C2C2E;color:#fff;width:96px;text-align:center}
button{font-size:15px;padding:8px 20px;background:#0A84FF;color:#fff;border:none;border-radius:8px;margin-left:8px;cursor:pointer}
button:disabled{opacity:.5}
#status{margin-top:12px;font-size:13px;opacity:.85;min-height:1.2em}
</style></head>
<body>
<canvas id='pc'></canvas>
<div id='controls'>
  <h2>Audio Relay</h2>
  <p>输入服务端显示的 PIN</p>
  <input id='pin' placeholder='PIN' maxlength='8' inputmode='numeric'>
  <button id='go'>连接</button>
  <div id='status'>就绪</div>
</div>
<script>
window.__ar_conn='new'; window.__ar_track=false;
const params=new URLSearchParams(location.search);
if(params.get('pin'))document.getElementById('pin').value=params.get('pin');
const status=document.getElementById('status'), goBtn=document.getElementById('go');
const canvas=document.getElementById('pc'), ctx=canvas.getContext('2d');
function resize(){canvas.width=innerWidth;canvas.height=innerHeight;}
addEventListener('resize',resize); resize();

// --- Rhythm particles (bass-reactive ring), mirroring the Avalonia/Flutter effect ---
class Particle{
  constructor(a,d,r,c){this.angle=a;this.baseDistance=d;this.distance=d;this.baseRadius=r;this.radius=r;this.color=c;}
  update(b){this.distance=this.baseDistance+b*150;this.radius=this.baseRadius+b*3;this.angle+=0.002;}
  draw(){const x=canvas.width/2+Math.cos(this.angle)*this.distance,y=canvas.height/2+Math.sin(this.angle)*this.distance;ctx.beginPath();ctx.arc(x,y,Math.max(0.5,this.radius),0,Math.PI*2);ctx.fillStyle=this.color;ctx.fill();}
}
let particles=[];
function initParticles(){particles=[];for(let i=0;i<300;i++){const hue=200+Math.random()*60;particles.push(new Particle(Math.random()*Math.PI*2,50+Math.random()*150,1+Math.random()*2,'hsl('+hue+',80%,60%)'));}}
initParticles();

let audioCtx,analyser,dataArray;
function ensureAudio(){audioCtx=audioCtx||new (window.AudioContext||window.webkitAudioContext)();if(audioCtx.state==='suspended')audioCtx.resume();return audioCtx;}
function tapStream(stream){
  const ac=ensureAudio();
  analyser=analyser||ac.createAnalyser();analyser.fftSize=256;
  const src=ac.createMediaStreamSource(stream);src.connect(analyser);analyser.connect(ac.destination);
  dataArray=new Uint8Array(analyser.frequencyBinCount);
}

function animate(){
  requestAnimationFrame(animate);
  ctx.fillStyle='rgba(5,5,16,0.2)';ctx.fillRect(0,0,canvas.width,canvas.height); // motion-blur trail
  let bass=0;
  if(analyser){analyser.getByteFrequencyData(dataArray);let s=0;for(let i=0;i<10;i++)s+=dataArray[i];bass=(s/10)/255;}
  for(const p of particles){p.update(bass);p.draw();}
}
animate();

// --- WebRTC signaling: server is the offerer ---
const post=(path,obj)=>fetch(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(obj)});
goBtn.onclick=async()=>{
  const pin=document.getElementById('pin').value.trim();
  if(!pin){status.textContent='请输入 PIN';return;}
  ensureAudio(); // user gesture: unlock playback
  goBtn.disabled=true;status.textContent='正在连接...';
  try{
    const pc=new RTCPeerConnection();
    pc.ontrack=(e)=>{window.__ar_track=true;tapStream(e.streams[0]);status.textContent='正在播放';};
    pc.onconnectionstatechange=()=>{window.__ar_conn=pc.connectionState;if(pc.connectionState==='failed'||pc.connectionState==='closed'){status.textContent='连接断开';goBtn.disabled=false;}};
    const offerResp=await post('/offer',{pin});
    if(!offerResp.ok){status.textContent='失败 '+offerResp.status+': '+await offerResp.text();goBtn.disabled=false;return;}
    const offer=await offerResp.json();
    await pc.setRemoteDescription({sdp:offer.sdp,type:'offer'});
    const answer=await pc.createAnswer();await pc.setLocalDescription(answer);
    await new Promise(r=>{if(pc.iceGatheringState==='complete')return r();pc.addEventListener('icegatheringstatechange',()=>{if(pc.iceGatheringState==='complete')r();});});
    const ansResp=await post('/answer',{sdp:pc.localDescription.sdp,type:'answer',pin});
    if(!ansResp.ok){status.textContent='失败 '+ansResp.status+': '+await ansResp.text();goBtn.disabled=false;return;}
    if(!window.__ar_track)status.textContent='等待音频...';
  }catch(e){status.textContent='错误: '+e.message;goBtn.disabled=false;}
};
</script>
</body></html>";
}
