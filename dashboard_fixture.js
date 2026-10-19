// Only injected by the explicit --ui-check diagnostic mode. Never loaded by the homepage.
window.dashboardTest = {
  seed(mode) {
    const receive=window.dashboard.receive;
    if(mode==='offline'){receive({type:'error',kind:'quota',message:'测试：连接超时'});receive({type:'error',kind:'performance',message:'测试：连接超时'});return;}
    const now=new Date().toISOString(),future=new Date(Date.now()+3600000).toISOString();
    const quota={ok:true,checkedAt:now,accounts:[0,1,2,3].map((i)=>({number:i+1,alias:'ABCD'[i],email:`preview${i+1}@example.com`,subscriptionStatus:'ok',subscriptionType:i<2?'Max':'Pro',usageStatus:'ok',usageFetchedAt:now,active:i===0,usage:{fiveHour:{pct:[0,75,95,100][i],resetsAt:future},sevenDay:{pct:[21,33,88,40][i],resetsAt:future},scoped:[]}}))};
    if(mode==='edge'){
      quota.accounts[0].usage.fiveHour=null;quota.accounts[0].subscriptionStatus='timeout';
      quota.accounts[1].usageStatus='rate_limited';quota.accounts[1].lastGoodUsage=quota.accounts[1].usage;quota.accounts[1].lastGoodFetchedAt='2026-10-01T00:00:00Z';
      quota.accounts[1].usageRetryAt=future;quota.accounts[2].usage.sevenDay.resetsAt='2020-01-01T00:00:00Z';
      quota.accounts[2].usage.scoped=[{name:'Opus 单独窗口',pct:100,resetsAt:future}];quota.accounts[3].disabled=true;
    }
    receive({type:'data',kind:'quota',data:quota});
    receive({type:'data',kind:'performance',data:{ok:true,checkedAt:now,cpuPercent:mode==='edge'?99.2:13.7,cpuCount:2,load:[.21,.33,.28],memoryUsed:1200000000,memoryTotal:4100000000,memoryPercent:29.3,swapUsed:0,swapTotal:8000000000,diskUsed:39000000000,diskTotal:75000000000,diskFree:36000000000,diskPercent:52,networkRxPerSecond:151200,networkTxPerSecond:17800,uptimeSeconds:137000}});
    if(mode==='stale'){receive({type:'error',kind:'quota',message:'测试：暂时无法更新'});receive({type:'error',kind:'performance',message:'测试：服务器连接中断'});}
    if(mode==='paused')receive({type:'paused',value:true});
  },
  audit() {
    const cards=[...document.querySelectorAll('.account-card')];
    return {width:innerWidth,height:innerHeight,scrollWidth:document.documentElement.scrollWidth,noHorizontalOverflow:document.documentElement.scrollWidth<=innerWidth,
      title:document.title,accounts:cards.length,connection:document.getElementById('connection').textContent,
      quotas:cards.map(c=>({name:c.querySelector('h3').textContent,state:c.querySelector('.account-state').textContent,plan:c.querySelector('.plan-badge').textContent,remaining:[...c.querySelectorAll('.quota-heading strong')].map(n=>n.textContent)})),
      noCommands:!document.querySelector('input,textarea,iframe'),buttons:[...document.querySelectorAll('button')].map(b=>b.textContent),
      notices:document.getElementById('notice').textContent,serverState:document.getElementById('server-state').textContent,
      sensitiveText:/Bearer |access_token|refresh_token/.test(document.body.textContent)};
  }
};
