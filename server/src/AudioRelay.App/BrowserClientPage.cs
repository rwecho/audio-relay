namespace AudioRelay.App;

/// <summary>
/// Built-in browser client served at GET /. Connects via SDP (/offer, /answer), then receives raw
/// interleaved stereo Float32 PCM over a libdatachannel DATA CHANNEL ("audio"), plays it via a Web
/// Audio ScriptProcessor (downmixed to mono; no Opus decode), and drives a rhythm-particle field
/// from the played audio's bass. Exposes window.__ar_conn / window.__ar_recv for automated checks.
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
window.__ar_conn='new'; window.__ar_recv=0; window.__ar_level=0;
const params=new URLSearchParams(location.search);
if(params.get('pin'))document.getElementById('pin').value=params.get('pin');
const status=document.getElementById('status'), goBtn=document.getElementById('go');
const canvas=document.getElementById('pc'), ctx=canvas.getContext('2d');
function resize(){canvas.width=innerWidth;canvas.height=innerHeight;} addEventListener('resize',resize); resize();

class Particle{constructor(a,d,r,c){this.angle=a;this.baseDistance=d;this.distance=d;this.baseRadius=r;this.radius=r;this.color=c;}
 update(b){this.distance=this.baseDistance+b*150;this.radius=this.baseRadius+b*3;this.angle+=0.002;}
 draw(){const x=canvas.width/2+Math.cos(this.angle)*this.distance,y=canvas.height/2+Math.sin(this.angle)*this.distance;ctx.beginPath();ctx.arc(x,y,Math.max(0.5,this.radius),0,Math.PI*2);ctx.fillStyle=this.color;ctx.fill();}}
let particles=[];
for(let i=0;i<300;i++){const hue=200+Math.random()*60;particles.push(new Particle(Math.random()*Math.PI*2,50+Math.random()*150,1+Math.random()*2,'hsl('+hue+',80%,60%)'));}

let ac,proc,an,data,ring=[];
async function ensureAudio(){ac=ac||new (window.AudioContext||window.webkitAudioContext)();if(ac.state==='suspended')ac.resume();return ac;}

goBtn.onclick=async()=>{
  const pin=document.getElementById('pin').value.trim();
  if(!pin){status.textContent='请输入 PIN';return;}
  await ensureAudio();
  goBtn.disabled=true;status.textContent='正在连接...';
  try{
    const pc=new RTCPeerConnection();
    pc.onconnectionstatechange=()=>{window.__ar_conn=pc.connectionState;if(pc.connectionState==='failed'||pc.connectionState==='closed'){status.textContent='连接断开';goBtn.disabled=false;}};
    pc.ondatachannel=e=>{
      const dc=e.channel; dc.binaryType='arraybuffer';
      dc.onmessage=ev=>{
        const pcm=new Float32Array(ev.data); window.__ar_recv+=pcm.length;
        for(let i=0;i<pcm.length;i+=2) ring.push((pcm[i]+pcm[i+1])*0.5); // downmix stereo -> mono
        if(!proc){
          proc=ac.createScriptProcessor(2048,0,1);
          proc.onaudioprocess=oe=>{const out=oe.outputBuffer.getChannelData(0);for(let i=0;i<out.length;i++)out[i]=ring.length?ring.shift():0;};
          an=ac.createAnalyser();an.fftSize=256;data=new Uint8Array(an.frequencyBinCount);
          proc.connect(an);an.connect(ac.destination);
          status.textContent='正在播放';
        }
      };
    };
    const offerResp=await fetch('/offer',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pin})});
    if(!offerResp.ok){status.textContent='失败 '+offerResp.status+': '+await offerResp.text();goBtn.disabled=false;return;}
    const offer=await offerResp.json();
    await pc.setRemoteDescription({sdp:offer.sdp,type:'offer'});
    const answer=await pc.createAnswer();await pc.setLocalDescription(answer);
    await new Promise(r=>{if(pc.iceGatheringState==='complete')return r();pc.addEventListener('icegatheringstatechange',()=>{if(pc.iceGatheringState==='complete')r();});});
    const ansResp=await fetch('/answer',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sdp:pc.localDescription.sdp,type:'answer',pin})});
    if(!ansResp.ok){status.textContent='失败 '+ansResp.status+': '+await ansResp.text();goBtn.disabled=false;return;}
    status.textContent='等待音频...';
  }catch(e){status.textContent='错误: '+e.message;goBtn.disabled=false;}
};

function animate(){
  requestAnimationFrame(animate);
  ctx.fillStyle='rgba(5,5,16,0.2)';ctx.fillRect(0,0,canvas.width,canvas.height);
  let bass=0;
  if(an){an.getByteFrequencyData(data);let s=0;for(let i=0;i<10;i++)s+=data[i];bass=(s/10)/255;window.__ar_level=bass;}
  for(const p of particles){p.update(bass);p.draw();}
}
animate();
</script>
</body></html>";
}
