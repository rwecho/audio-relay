namespace AudioRelay.App;

/// <summary>
/// Built-in browser test client served at GET /. The server is the SDP offerer: this page
/// POSTs /offer to get it, sets it as remote description, creates an answer, and POSTs /answer.
/// Exposes window.__ar_conn / window.__ar_track for automated checking. Same-origin, no CORS.
/// </summary>
public static class BrowserClientPage
{
    public const string Html = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Audio Relay</title>
<style>body{font-family:-apple-system,system-ui,sans-serif;background:#1C1C1E;color:#fff;display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;margin:0}h2{font-weight:600;margin-bottom:4px}p{color:#8E8E93;margin:0 0 16px;font-size:13px}button{font-size:16px;padding:12px 28px;background:#0A84FF;color:#fff;border:none;border-radius:12px;margin-top:12px;cursor:pointer}input{font-size:16px;padding:10px;border-radius:10px;border:1px solid #3A3A3C;background:#2C2C2E;color:#fff;width:120px;text-align:center}#status{margin-top:18px;opacity:.85;min-height:1.2em;max-width:90%;text-align:center}</style></head>
<body>
<h2>Audio Relay</h2>
<p>输入服务端显示的 PIN</p>
<input id='pin' placeholder='PIN' maxlength='8' inputmode='numeric'>
<button id='go'>连接</button>
<button id='playbtn' style='display:none;background:#34C759'>🔊 点击播放声音</button>
<div id='status'>就绪</div>
<script>
window.__ar_conn = 'new';
window.__ar_track = false;
let _audio = null;
const params = new URLSearchParams(location.search);
if (params.get('pin')) document.getElementById('pin').value = params.get('pin');
const status = document.getElementById('status');
const playBtn = document.getElementById('playbtn');
const post = (path, obj) => fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(obj) });
function tryPlay() {
  if (!_audio) return;
  _audio.play().then(() => { status.textContent = '正在播放'; playBtn.style.display = 'none'; window.__ar_track = true; })
    .catch(() => { playBtn.style.display = ''; status.textContent = '点上面的绿色按钮以播放'; });
}
playBtn.onclick = () => tryPlay();
document.getElementById('go').onclick = async () => {
  const pin = document.getElementById('pin').value.trim();
  if (!pin) { status.textContent = '请输入 PIN'; return; }
  status.textContent = '正在连接...';
  try {
    const pc = new RTCPeerConnection();
    pc.ontrack = (e) => {
      window.__ar_track = true;
      console.log('[ar] ontrack kind=' + e.track.kind);
      _audio = new Audio();
      _audio.srcObject = e.streams[0];
      tryPlay();
    };
    pc.onconnectionstatechange = () => { window.__ar_conn = pc.connectionState; console.log('[ar] conn=' + pc.connectionState); };

    const offerResp = await post('/offer', { pin });
    if (!offerResp.ok) { status.textContent = '失败 ' + offerResp.status + ': ' + await offerResp.text(); return; }
    const offer = await offerResp.json();
    await pc.setRemoteDescription({ sdp: offer.sdp, type: 'offer' });

    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    await new Promise(r => {
      if (pc.iceGatheringState === 'complete') return r();
      pc.addEventListener('icegatheringstatechange', () => { if (pc.iceGatheringState === 'complete') r(); });
    });

    const ansResp = await post('/answer', { sdp: pc.localDescription.sdp, type: 'answer', pin });
    if (!ansResp.ok) { status.textContent = '失败 ' + ansResp.status + ': ' + await ansResp.text(); return; }
    if (!window.__ar_track) status.textContent = '等待音频...';
  } catch (e) { status.textContent = '错误: ' + e.message; }
};
</script>
</body></html>";
}
