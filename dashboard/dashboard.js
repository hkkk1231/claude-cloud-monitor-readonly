(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const state = { quota: null, performance: null, errors: {}, busy: new Set(), paused: false, cards: new Map(), history: {cpu: [], memory: []} };
  const number = value => typeof value === 'number' && Number.isFinite(value);
  const percent = value => number(value) && value >= 0 && value <= 100 ? value : null;
  const text = (node, value) => { if (node.textContent !== String(value)) node.textContent = value; };
  const stamp = value => { const ms = typeof value === 'string' ? Date.parse(value) : NaN; return Number.isFinite(ms) ? ms : null; };
  const time = value => stamp(value) === null ? '未知' : new Date(value).toLocaleTimeString('zh-CN', {timeZone:'Asia/Shanghai', hour12:false,hour:'2-digit',minute:'2-digit'});
  const age = value => {const ms=stamp(value);if(ms===null)return '时间未知';const m=Math.max(0,Math.floor((Date.now()-ms)/60000));return m<1?'刚刚更新':m<60?`${m} 分钟前`:m<1440?`${Math.floor(m/60)} 小时前`:`${Math.floor(m/1440)} 天前`;};
  const bytes = value => {if(!number(value)||value<0)return '—';if(value>=1073741824)return `${(value/1073741824).toFixed(1)} GB`;if(value>=1048576)return `${(value/1048576).toFixed(1)} MB`;if(value>=1024)return `${(value/1024).toFixed(1)} KB`;return `${Math.round(value)} B`;};
  function resetText(value) {
    const ms=stamp(value);if(ms===null)return '重置时间未知';
    if(ms<=Date.now())return '已到重置时间 · 等待新数据';
    const minutes=Math.ceil((ms-Date.now())/60000), hours=Math.floor(minutes/60);
    const distance=hours>=24?`${Math.floor(hours/24)}天 ${hours%24}小时`:hours?`${hours}小时 ${minutes%60}分`:`${minutes}分钟`;
    return `${distance}后重置 · ${new Date(ms).toLocaleString('zh-CN',{timeZone:'Asia/Shanghai',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hour12:false})}`;
  }
  function quotaModel(account) {
    const sampled=account.usageStatus==='ok'?account.usageFetchedAt:account.lastGoodFetchedAt;
    const old=stamp(sampled)===null || Date.now()-stamp(sampled)>300000;
    const cached=account.usageStatus!=='ok' || old || Boolean(state.errors.quota);
    const usage=(account.usageStatus==='ok'?account.usage:account.lastGoodUsage) || {};
    const windows=[['fiveHour','5 小时',usage.fiveHour],['sevenDay','7 天',usage.sevenDay]];
    for(const [i,w] of (Array.isArray(usage.scoped)?usage.scoped:[]).entries())windows.push([`scoped-${i}`,w.name || '模型额度',w]);
    return {sampled,cached,windows:windows.map(([key,label,w])=>({key,label,remaining:w&&percent(w.pct)!==null?100-w.pct:null,reset:w&&w.resetsAt}))};
  }
  function empty(title, detail) {
    const box=document.createElement('div');box.className='empty-state';
    const icon=document.createElement('span');icon.className='offline-mark';text(icon,'◌');
    const h=document.createElement('h3');text(h,title);const p=document.createElement('p');text(p,detail);box.append(icon,h,p);$('accounts').replaceChildren(box);
  }
  function drawAccounts(data) {
    const accounts=Array.isArray(data.accounts)?data.accounts:[], keep=new Set();
    if(!accounts.length){state.cards.clear();empty('暂无已登记账号','服务器已连接，但账号池没有返回可显示的账号。');}
    else if(!state.cards.size)$('accounts').replaceChildren();
    let attention=0,unknown=0;const plans=new Map();
    accounts.forEach((a,index)=>{
      const key=String(a.number??index);keep.add(key);let card=state.cards.get(key);
      if(!card){card=$('account-template').content.firstElementChild.cloneNode(true);card.rows=new Map();state.cards.set(key,card);$('accounts').append(card);}
      const q=selector=>card.querySelector(selector),model=quotaModel(a);
      const alias=a.alias==='primary'?'A':a.alias&&/^[a-d]$/i.test(a.alias)?a.alias.toUpperCase():a.alias||String(a.number??index+1);
      text(q('h3'),`账号 ${alias}`);text(q('.avatar'),alias.slice(0,1).toUpperCase());
      text(q('.account-email'),a.email||'邮箱未提供');q('.account-email').title=a.email||'';
      const plan=a.subscriptionStatus==='ok'&&a.subscriptionType?a.subscriptionType:'待确认';
      text(q('.plan-badge'),plan);q('.plan-badge').className='plan-badge'+(plan==='Max'?' max':'');if(plan!=='待确认')plans.set(plan,(plans.get(plan)||0)+1);
      q('.default-badge').hidden=!a.active;card.classList.toggle('stale',model.cached);
      const missing=model.windows.some(w=>w.remaining===null),low=model.windows.some(w=>w.remaining!==null&&w.remaining<=20);
      if(low)attention++;if(missing||model.cached)unknown++;
      text(q('.account-state'),missing?'额度待获取':model.cached?'历史额度 · 待更新':low?'额度偏低':'额度正常');
      q('.account-state').className='account-state'+(missing||model.cached||low?' warn':'');
      const rowKeys=new Set();model.windows.forEach(w=>{
        rowKeys.add(w.key);let row=card.rows.get(w.key);
        if(!row){row=document.createElement('div');row.innerHTML='<div class="quota-heading"><span></span><strong></strong></div><div class="meter"><span></span></div><div class="reset-time"></div>';card.rows.set(w.key,row);q('.quota-list').append(row);}
        row.className='quota-row '+(w.remaining===null?'unknown':w.remaining<=5?'danger':w.remaining<=20?'warn':'');
        text(row.querySelector('.quota-heading>span'),w.label);text(row.querySelector('strong'),w.remaining===null?'—':`${Math.round(w.remaining)}%`);
        row.querySelector('.meter>span').style.width=`${w.remaining??0}%`;text(row.querySelector('.reset-time'),resetText(w.reset));
      });
      for(const [key,row] of card.rows)if(!rowKeys.has(key)){row.remove();card.rows.delete(key);}
      text(q('.sample-time'),`${model.cached?'历史样本':'额度采样'} · ${age(model.sampled)}`);
      text(q('.pool-state'),a.disabled?'账号已停用':a.usageStatus==='rate_limited'?'查询冷却中':'');
      q('.pool-state').title=a.usageRetryAt?`下次查询 ${time(a.usageRetryAt)}`:'';
    });
    for(const [key,card] of state.cards)if(!keep.has(key)){card.remove();state.cards.delete(key);}
    text($('account-count'),accounts.length);text($('plans'),[...plans].map(([p,n])=>`${p} × ${n}`).join(' / ')||'待确认');
    text($('attention'),attention);text($('attention-label'),unknown?`个偏低 · ${unknown} 个待更新`:'个账号');
    text($('quota-time'),time(data.checkedAt));text($('quota-age'),age(data.checkedAt));
  }
  function metric(id,value,detail) {
    const node=$(id),pct=percent(value);text(node.querySelector('strong'),pct===null?'—':pct.toFixed(1));
    node.querySelector('.meter>span').style.width=`${pct??0}%`;text(node.querySelector('.metric-detail'),detail);
    node.classList.toggle('warn',pct!==null&&pct>=80&&pct<90);node.classList.toggle('danger',pct!==null&&pct>=90);
  }
  function drawPerformance(data,record=true) {
    metric('cpu',data.cpuPercent,'最近采样期间的处理器使用率');metric('memory',data.memoryPercent,`${bytes(data.memoryUsed)} / ${bytes(data.memoryTotal)}`);
    metric('disk',data.diskPercent,`${bytes(data.diskUsed)} / ${bytes(data.diskTotal)}`);text($('disk-free'),`可用 ${bytes(data.diskFree)}`);
    text($('cpu-cores'),number(data.cpuCount)?`${data.cpuCount} 核`:'核数未知');
    text($('network-rx'),`${bytes(data.networkRxPerSecond)}/s`);text($('network-tx'),`${bytes(data.networkTxPerSecond)}/s`);
    text($('load'),`系统负载 ${Array.isArray(data.load)?data.load.map(n=>number(n)?n.toFixed(2):'—').join(' / '):'—'} · 1/5/15 分钟`);
    text($('swap'),`交换空间 ${bytes(data.swapUsed)} / ${bytes(data.swapTotal)}`);text($('perf-time'),`采样 ${time(data.checkedAt)}`);
    const hours=number(data.uptimeSeconds)?Math.floor(data.uptimeSeconds/3600):null;
    text($('uptime'),hours===null?'运行时长未知':hours===0?`已运行 ${Math.floor(data.uptimeSeconds/60)} 分钟`:`已运行 ${Math.floor(hours/24)}天 ${hours%24}小时`);
    if(record)for(const [id,value] of [['cpu',data.cpuPercent],['memory',data.memoryPercent]]){
      if(percent(value)===null)continue;const history=state.history[id];history.push(value);if(history.length>30)history.shift();
      $(id).querySelector('.spark path').setAttribute('d',history.map((v,i)=>`${i?'L':'M'} ${i*240/29} ${34-v*.32}`).join(' '));
    }
  }
  function status() {
    const stale=state.performance&&(stamp(state.performance.checkedAt)===null||Date.now()-stamp(state.performance.checkedAt)>30000);
    const connected=Boolean(state.performance)&&!state.errors.performance&&!stale;
    const pressure=connected&&(['memoryPercent','diskPercent'].some(k=>number(state.performance[k])&&state.performance[k]>=90)||
      (state.performance.cpuPercent>=90 && state.performance.load?.[1]>=state.performance.cpuCount));
    text($('server-state'),state.errors.performance?(state.performance?'连接中断 · 保留上次采样':'连接失败 · 暂无采样'):stale?'采样已过期':pressure?'资源压力较高':connected?'运行正常':'等待首次采样');
    $('server-dot').className='status-dot '+(!connected?'neutral':pressure?'warn':'');
    document.querySelector('.server-panel').classList.toggle('stale',!connected);
    const label=state.paused?'刷新已暂停':state.errors.performance?'服务器未连接':state.errors.quota?'账号查询异常':connected?'服务器已连接':'等待连接';
    $('connection').className='pill '+(state.paused||!connected?'neutral':state.errors.quota?'warn':'');
    const icon=document.createElement('i');$('connection').replaceChildren(icon,document.createTextNode(label));
    const messages=Object.entries(state.errors).map(([k,v])=>`${k==='quota'?'账号':'服务器'}：${v}`);
    if(stale&&!state.errors.performance)messages.push('性能采样已过期，恢复刷新后将重新获取。');
    $('notice').hidden=!messages.length;text($('notice'),messages.join(' · '));
    $('refresh').disabled=state.busy.size>0;$('refresh').classList.toggle('loading',state.busy.size>0);
    text($('refresh').querySelector('span'),state.busy.size?'刷新中':'刷新');
    text($('pause'),state.paused?'继续刷新':'暂停刷新');$('pause').setAttribute('aria-pressed',String(state.paused));
    text($('refresh-mode'),state.paused?'自动刷新已暂停':'自动刷新已开启 · 最小化时暂停采样');
    text($('quota-status'),state.busy.has('quota')?'正在查询…':state.errors.quota?'稍后自动重试':'每 60 秒更新');
  }
  function receive(message) {
    if(!message||typeof message!=='object')return;const {type,kind}=message;
    if(type==='config')text($('server-address'),message.host||'服务器');
    if(type==='paused')state.paused=Boolean(message.value);
    if(!['quota','performance'].includes(kind)){status();return;}
    if(type==='loading')state.busy.add(kind);
    if(type==='settled')state.busy.delete(kind);
    if(type==='error'){
      state.errors[kind]=message.message||'暂时无法更新';
      if(kind==='quota'&&!state.quota)empty('暂时无法获取账号','服务器或账号查询暂不可用。恢复连接后将自动更新，不会把未知额度当作已用尽。');
      if(kind==='quota'&&state.quota)drawAccounts(state.quota);
    }
    if(type==='data'&&message.data&&message.data.ok===true){state[kind]=message.data;delete state.errors[kind];kind==='quota'?drawAccounts(message.data):drawPerformance(message.data);}
    status();
  }
  function tick() {
    const now=new Date();text($('clock'),now.toLocaleTimeString('zh-CN',{timeZone:'Asia/Shanghai',hour12:false,hour:'2-digit',minute:'2-digit'}));
    text($('date'),now.toLocaleDateString('zh-CN',{timeZone:'Asia/Shanghai',year:'numeric',month:'long',day:'numeric',weekday:'long'}));
    if(state.quota)drawAccounts(state.quota);status();
  }
  const send=action=>window.chrome?.webview?.postMessage(action);
  $('refresh').addEventListener('click',()=>send('refresh'));$('pause').addEventListener('click',()=>send(state.paused?'resume':'pause'));
  window.chrome?.webview?.addEventListener('message',event=>receive(event.data));
  // Explicit test hook only handles the same read-only data messages; no commands.
  window.dashboard={receive,quotaModel};tick();setInterval(tick,15000);send('ready');
})();
