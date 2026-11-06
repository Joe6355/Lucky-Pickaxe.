(function (root) {
  "use strict";
  let sdk, unity, receiver, options = {}, initialization;
  let gameReady = false, ready = false, local = false, failed = false;
  let sdkPaused = false, hidden = !!root.document.hidden, blurred = false, sentPause;
  let warnedStorage = false, rewardRequest, sentRewardedAvailable;
  let requestedLanguage, language, sentLanguage = false, languageApplied = false;
  let disposed = false, engineActive = false, gameplayReported = false, gameplayUnavailable = false;
  let device, sentDevice, viewportPaused = false;
  let account = { available: false, status: "unavailable", name: "" }, authPending = false, sentAccount, accountGeneration=0;
  const ratingName="mmr";
  let ratingCache, ratingLoadPending=false, ratingSendPending=false, ratingLoadAfter=0, ratingSendAfter=0, ratingLoadGeneration=-1;
  const removers = [];
  const loopback = /^(localhost|127\.0\.0\.1|\[::1\])$/.test(root.location.hostname);
  const journalKey = key => "LuckyPickaxe." + key + ".journal";
  const progressKey = "MineLoot.Progress.v2", cloudKey = "luckyPickaxeSave", cloudMetaKey = "LuckyPickaxe.cloud.v1";
  let playerHandle, cloudContext, latestProgress, cloudAccount = "", cloudMeta = {}, cloudState = {status:"unavailable"};
  let cloudEpoch = 0, cloudSerial = 0, cloudTimer, cloudBusy = false, cloudDecision, cloudAfter = 0, cloudRead, localMerge, pendingWarehouseClaim;
  const cloudInterval = 300000, cloudRetryInterval = 30000, cloudReadWindow = 20000;
  const cloudReadHandles=new WeakSet();
  // Per-device cumulative deltas form an idempotent ledger. Importing a shared snapshot twice
  // never adds its wallet twice. The ledger travels atomically with the local JSON journal.
  const sumFields = "coins tickets totalBlocks totalClicks totalRolls oresMined miningBlocks totalProduced totalOfflineCoins prestigeCount crystals totalCrystals videoTicketClaims enchantingExperience earnedCoins spentTickets spentEnchantingExperience".split(" ");
  const maxFields = "upgradeLevel forgeLevel swapLevel efficiencyLevel pressLevel treasureLevel workerCount drillCount cartCount railLevel warehouseLevel fortuneLevel hasteLevel".split(" ");
  const boardFields="stratum depths damage boardSeed lostColumns failedFloors rollPending pendingTypes pendingSwapUsed pendingNewStratum pendingSwapLimit pendingSwapCount batchActive pendingBatch batchRemaining batchCompleted batchReward".split(" ");
  const toolFields="upgradeLevel enchantments enchantCount enchantSeed pickWear wearCarry".split(" ");
  const warehouseFields=["offlineRewardSeconds","offlineElapsedSeconds","offlineRewardRateMilli","offlineRewardCapacity"];
  const warehouseMeta=data=>Object.fromEntries(warehouseFields.map(field=>[field,data[field]||1]));
  const limits = {tickets:1000000,totalBlocks:25000000,totalRolls:10000000,prestigeCount:1000000,crystals:10000000,totalCrystals:10000000,videoTicketClaims:1000000,enchantingExperience:1000000000};
  const clone = value => JSON.parse(JSON.stringify(value));
  function progressData(json) { const value=JSON.parse(json);delete value._sync;return value; }
  function fingerprint(value) {
    const json=typeof value==="string"?value:JSON.stringify(value,Object.keys(value).sort());let a=2166136261,b=3339675911;
    for(let i=0;i<json.length;i++) {a=Math.imul(a^json.charCodeAt(i),16777619);b=Math.imul(b^json.charCodeAt(i),2246822519);}
    return (a>>>0).toString(16).padStart(8,"0")+(b>>>0).toString(16).padStart(8,"0");
  }
  function newDeviceId() {return "device-"+Date.now().toString(36)+"-"+Math.random().toString(36).slice(2);}
  function unionRanges(ranges) {
    const result=[];
    for(const range of [...ranges].sort((a,b)=>a[0]-b[0]||a[1]-b[1])) {
      const last=result[result.length-1];if(last&&range[0]<=last[1])last[1]=Math.max(last[1],range[1]);else result.push([...range]);
    }
    return result;
  }
  function warehouseSummary(ledger) {
    const origins=ledger.parts.filter(p=>p.warehouseEarned>0).map(part=>({part,claimed:0,bonus:[]}));
    for(const p of ledger.parts) for(const claim of p.warehouseClaims||[]) {
      const origin=origins.find(o=>o.part.id===claim.origin);if(!origin) continue;
      origin.claimed=Math.max(origin.claimed,Math.min(origin.part.warehouseEarned,claim.upto));origin.bonus.push(...claim.bonus);
    }
    let pending=0,credit=0;
    for(const o of origins) {
      pending+=o.part.warehouseEarned-o.claimed;
      credit+=o.claimed+2*unionRanges(o.bonus.map(([a,b])=>[Math.min(a,o.claimed),Math.min(b,o.claimed)])).reduce((n,[a,b])=>n+b-a,0);
    }
    const actual=ledger.parts.reduce((n,p)=>n+(p.warehouseCredited||0),0);
    return {origins:origins.sort((a,b)=>a.part.id.localeCompare(b.part.id)),pending,correction:Math.min(0,credit-actual)};
  }
  function warehouseLedger(ledger,json) {
    if(ledger.warehouseVersion===1) return ledger;
    const result=clone(ledger),data=progressData(json);result.warehouseVersion=1;
    const already=result.parts.some(p=>p.warehouseEarned!==undefined);
    for(const p of result.parts) {p.warehouseEarned=p.warehouseEarned||0;p.warehouseCredited=p.warehouseCredited||0;p.warehouseClaims=p.warehouseClaims||[];}
    if(!already&&data.offlineCoins>0) {
      const fields=["offlineCoins","offlineRewardSeconds","offlineElapsedSeconds","offlineRewardRateMilli","offlineRewardCapacity"];
      const source=result.parts.filter(p=>fields.every(f=>p.state[f]===data[f])).sort((a,b)=>a.clock-b.clock||a.id.localeCompare(b.id))[0];
      const id="warehouse-"+fingerprint(JSON.stringify([source?.id||fingerprint(data),...fields.map(f=>data[f])]));
      result.parts.push({id,clock:0,totals:Object.fromEntries(sumFields.map(f=>[f,0])),xp:0,run:clone(ledgerRun(result)),state:{version:16},adjustment:true,warehouseEarned:data.offlineCoins,warehouseCredited:0,warehouseClaims:[],warehouseMeta:warehouseMeta(data)});
    }
    return result;
  }
  function validLedger(value) {
    if(!value || value.version!==1 || !Array.isArray(value.parts) || value.parts.length>48) throw new Error("Invalid merge ledger");
    const ids=new Set();
    for(const part of value.parts) {
      if(!part || !/^[a-zA-Z0-9-]{1,100}$/.test(part.id) || ids.has(part.id) || !Number.isSafeInteger(part.clock) || part.clock<0 || !part.totals || !part.state || !part.run || !Number.isSafeInteger(part.run.rank) || part.run.rank<0 || !/^[a-zA-Z0-9-]{1,100}$/.test(part.run.key) || !Number.isSafeInteger(part.xp) || Math.abs(part.xp)>1e12) throw new Error("Invalid merge component");
      for(const field of sumFields) if(!Number.isSafeInteger(part.totals[field]) || Math.abs(part.totals[field])>1e13) throw new Error("Invalid merge counter");
      if(part.state.version!==16) throw new Error("Unsupported merge snapshot");
      for(const field of ["boardClock","toolClock"]) if(part[field]!==undefined && (!Number.isSafeInteger(part[field]) || part[field]<0 || part[field]>part.clock)) throw new Error("Invalid merge domain clock");
      for(const field of ["warehouseEarned","warehouseCredited"]) if(part[field]!==undefined&&(!Number.isSafeInteger(part[field])||part[field]<0||part[field]>1e13))throw new Error("Invalid warehouse counter");
      if(value.warehouseVersion===1&&(!Number.isSafeInteger(part.warehouseEarned)||!Number.isSafeInteger(part.warehouseCredited)||!Array.isArray(part.warehouseClaims)))throw new Error("Missing warehouse ledger");
      if(part.warehouseClaims!==undefined) {
        if(!Array.isArray(part.warehouseClaims)||part.warehouseClaims.length>48)throw new Error("Invalid warehouse claims");
        for(const claim of part.warehouseClaims) {
          if(!claim||!/^[a-zA-Z0-9-]{1,100}$/.test(claim.origin)||!Number.isSafeInteger(claim.upto)||claim.upto<0||claim.upto>1e13||!Array.isArray(claim.bonus)||claim.bonus.length>1024)throw new Error("Invalid warehouse receipt");
          for(const range of claim.bonus)if(!Array.isArray(range)||range.length!==2||!range.every(Number.isSafeInteger)||range[0]<0||range[1]<range[0]||range[1]>claim.upto)throw new Error("Invalid warehouse bonus coverage");
        }
      }
      ids.add(part.id);
    }
    for(const part of value.parts)for(const claim of part.warehouseClaims||[])if(!value.parts.some(p=>p.id===claim.origin&&p.warehouseEarned>=claim.upto))throw new Error("Warehouse origin unavailable");
    return value;
  }
  function ledgerFor(json) {
    const data=progressData(json),idData=clone(data);delete idData.lastSeenUtc;
    const part={id:"legacy-"+fingerprint(idData),clock:0,totals:{},xp:data.experience||0,run:{rank:data.prestigeCount||0,key:"legacy-"+(data.prestigeCount||0)},state:clone(data)};
    for(const field of sumFields) {part.totals[field]=data[field]||0;delete part.state[field];}
    delete part.state.experience;return warehouseLedger({version:1,parts:[part]},json);
  }
  function joinedLedger(first,second) {
    const parts=new Map();
    for(const part of [...first.parts,...second.parts]) {
      const old=parts.get(part.id);
      if(old && old.clock===part.clock && JSON.stringify(old)!==JSON.stringify(part)) throw new Error("Conflicting merge component");
      if(!old || old.clock<part.clock) parts.set(part.id,clone(part));
    }
    return validLedger({version:1,warehouseVersion:1,parts:[...parts.values()].sort((a,b)=>a.id.localeCompare(b.id))});
  }
  function containsLedger(first,second) {return second.parts.every(p=>first.parts.some(q=>q.id===p.id&&q.clock>=p.clock));}
  function coversTransactions(first,second) {
    // Sanitize/ObserveMilestones may update a stale device's achievement display after another
    // device has already completed its paid draw. Such metadata must not resurrect that draw.
    return second.parts.every(p=>first.parts.some(q=>q.id===p.id&&(q.clock>=p.clock||q.xp===p.xp&&JSON.stringify(q.run)===JSON.stringify(p.run)&&sumFields.every(field=>q.totals[field]===p.totals[field]))));
  }
  function ledgerRun(ledger) {return ledger.parts.filter(p=>!p.adjustment).map(p=>p.run).sort((a,b)=>b.rank-a.rank||b.key.localeCompare(a.key))[0]||{rank:0,key:"legacy-0"};}
  function limitLedger(ledger) {
    const correction=warehouseSummary(ledger).correction;
    const run=ledgerRun(ledger),totals=Object.fromEntries(sumFields.map(field=>[field,-Math.max(0,ledger.parts.reduce((n,p)=>n+p.totals[field],0)+(["coins","earnedCoins","totalProduced","totalOfflineCoins"].includes(field)?correction:0)-(limits[field]||1e12))]));
    const xp=-Math.max(0,ledger.parts.filter(p=>p.run.key===run.key&&p.run.rank===run.rank).reduce((n,p)=>n+p.xp,0)-25480);
    if(xp || Object.values(totals).some(n=>n)) {
      // One deterministic correction prevents a saturated wallet from refunding later purchases
      // on the next merge. Two devices computing the same correction share its component ID.
      const id="limit-"+fingerprint(JSON.stringify(ledger.parts.map(p=>[p.id,p.clock,p.totals,p.xp,p.run])));
      ledger.parts.push({id,clock:0,totals,xp,run:clone(run),state:{version:16},adjustment:true,warehouseEarned:0,warehouseCredited:0,warehouseClaims:[]});
      ledger.parts.sort((a,b)=>a.id.localeCompare(b.id));
    }
    return validLedger(ledger);
  }
  function projectLedger(ledger) {
    const run=ledgerRun(ledger),parts=ledger.parts.filter(p=>p.run.key===run.key&&p.run.rank===run.rank);
    const recent=parts.filter(p=>!p.adjustment).sort((a,b)=>b.clock-a.clock||b.id.localeCompare(a.id));
    const board=[...recent].sort((a,b)=>(b.state.stratum||1)-(a.state.stratum||1)||(b.boardClock??b.clock)-(a.boardClock??a.clock)||b.id.localeCompare(a.id))[0];
    const data=clone(recent[0]?.state||{version:16});
    for(const field of sumFields) data[field]=Math.max(0,Math.min(limits[field]||1e12,ledger.parts.reduce((n,p)=>n+p.totals[field],0)));
    const warehouse=warehouseSummary(ledger);
    for(const field of ["coins","earnedCoins","totalProduced","totalOfflineCoins"]) data[field]=Math.max(0,Math.min(1e12,ledger.parts.reduce((n,p)=>n+p.totals[field],0)+warehouse.correction));
    data.experience=Math.max(0,Math.min(25480,parts.reduce((n,p)=>n+p.xp,0)));
    for(const field of maxFields) data[field]=Math.max(0,...parts.map(p=>p.state[field]||0));
    for(const field of ["milestoneClaims","prestigeUpgrades","oreUnlocks"]) data[field]=ledger.parts.reduce((n,p)=>n|(p.state[field]||0),0);
    data.milestoneBest=Array.from({length:16},(_,i)=>Math.max(0,...ledger.parts.map(p=>p.state.milestoneBest?.[i]||0)));
    for(const field of ["guideStep","claimedContracts","videoTicketsReadyUtc","lastSeenUtc"]) data[field]=Math.max(0,...ledger.parts.map(p=>p.state[field]||0));
    // A pick keeps the enchantments of its own material; a weaker inherited pick cannot lend it charms.
    const pick=recent.filter(p=>(p.state.upgradeLevel||0)===data.upgradeLevel).sort((a,b)=>(b.toolClock??b.clock)-(a.toolClock??a.clock)||b.id.localeCompare(a.id))[0]||recent[0];
    for(const field of ["enchantments","enchantCount","enchantSeed","pickWear","wearCarry"]) if(pick?.state[field]!==undefined) data[field]=clone(pick.state[field]);
    for(const field of boardFields) if(board?.state[field]!==undefined) data[field]=clone(board.state[field]);
    data.offlineCoins=Math.min(1e12,warehouse.pending);data.offlineMerged=false;
    if(data.offlineCoins>0) {
      const outstanding=warehouse.origins.filter(o=>o.part.warehouseEarned>o.claimed),states=outstanding.map(o=>o.part.warehouseMeta||o.part.state);
      for(const field of warehouseFields)data[field]=Math.max(1,...states.map(s=>s[field]||1));
      data.offlineMerged=outstanding.length>1 || data.offlineCoins>data.offlineRewardRateMilli*data.offlineRewardSeconds/2000+1;
    } else for(const field of warehouseFields)data[field]=0;
    data.ratingHistoryPartial=ledger.parts.some(p=>p.state.ratingHistoryPartial);data.ratingTestProfile=false;
    return JSON.stringify(data);
  }
  function restoreMerge() {
    if(localMerge) return;
    try {
      const journal=JSON.parse(stored(journalKey(progressKey))||"null"),meta=journal?._sync;
      if(meta?.version===2 && typeof meta.account==="string" && /^[a-zA-Z0-9-]{1,100}$/.test(meta.device)) {localMerge={account:meta.account,device:meta.device,ledger:validLedger(warehouseLedger(meta.ledger,JSON.stringify(meta.observed||journal))),observed:meta.observed||progressData(JSON.stringify(journal))};pendingWarehouseClaim=meta.pendingWarehouseClaim;}
    } catch { /* The ordinary progress journal remains readable if only its sync metadata is damaged. */ }
  }
  function captureProgress(json) {
    if(!localMerge) return;
    const data=progressData(json),previous=localMerge.observed;
    if(meaningful(JSON.stringify(data))===meaningful(JSON.stringify(previous))) {localMerge.observed=data;return;}
    const ledger=clone(localMerge.ledger);
    let part=ledger.parts.find(p=>p.id===localMerge.device);
    if(!part) {part={id:localMerge.device,clock:0,boardClock:0,toolClock:0,totals:Object.fromEntries(sumFields.map(f=>[f,0])),xp:0,run:clone(ledgerRun(ledger)),state:{version:16},warehouseEarned:0,warehouseCredited:0,warehouseClaims:[]};ledger.parts.push(part);}
    const previousClock=part.clock;
    part.clock=Math.max(...ledger.parts.map(p=>p.clock),0)+1;
    const changed=fields=>fields.some(field=>JSON.stringify(data[field])!==JSON.stringify(previous[field]));
    part.boardClock=changed(boardFields)?part.clock:part.boardClock??previousClock;
    part.toolClock=changed(toolFields)?part.clock:part.toolClock??previousClock;
    const warehouseDelta=(data.offlineCoins||0)-(previous.offlineCoins||0);
    if(warehouseDelta>0) {part.warehouseEarned+=warehouseDelta;part.warehouseMeta=warehouseMeta(data);}
    if(warehouseDelta<0) {
      const base=-warehouseDelta,credit=pendingWarehouseClaim?.base===base?pendingWarehouseClaim.credit:(data.totalOfflineCoins||0)-(previous.totalOfflineCoins||0);
      if(credit!==base&&credit!==base*3)throw new Error("Warehouse claim receipt unavailable");
      const summary=warehouseSummary(ledger);let left=base;
      for(const origin of summary.origins) {
        const amount=Math.min(left,origin.part.warehouseEarned-origin.claimed);if(!amount)continue;
        let receipt=part.warehouseClaims.find(c=>c.origin===origin.part.id);
        if(!receipt){receipt={origin:origin.part.id,upto:0,bonus:[]};part.warehouseClaims.push(receipt);}
        receipt.upto=Math.max(receipt.upto,origin.claimed+amount);
        if(credit===base*3)receipt.bonus=unionRanges([...receipt.bonus,[origin.claimed,origin.claimed+amount]]);
        left-=amount;if(!left)break;
      }
      if(left)throw new Error("Warehouse claim exceeds saved reward");
      part.warehouseCredited+=credit;
    }
    const reset=(data.prestigeCount||0)>(previous.prestigeCount||0);
    if(reset) {part.run={rank:Math.max(ledgerRun(ledger).rank+1,data.prestigeCount),key:localMerge.device+"-"+part.clock};part.xp=data.experience||0;}
    else part.xp+=(data.experience||0)-(previous.experience||0);
    for(const field of sumFields) part.totals[field]+=(data[field]||0)-(previous[field]||0);
    part.state=clone(data);for(const field of [...sumFields,"experience"]) delete part.state[field];
    validLedger(ledger);localMerge.ledger=ledger;localMerge.observed=data;pendingWarehouseClaim=null;
  }
  function persistJournal(json) {
    const data=progressData(json);
    if(localMerge) {
      data._sync={version:2,account:localMerge.account,device:localMerge.device,ledger:localMerge.ledger};
      if(meaningful(json)!==meaningful(JSON.stringify(localMerge.observed))) data._sync.observed=localMerge.observed;
      if(pendingWarehouseClaim)data._sync.pendingWarehouseClaim=pendingWarehouseClaim;
    }
    return store(journalKey(progressKey),JSON.stringify(data));
  }

  function stored(key) { try { return root.localStorage.getItem(key); } catch (error) { storageWarning(error); return null; } }
  function store(key,value) { try { root.localStorage.setItem(key,value); return true; } catch (error) { storageWarning(error); return false; } }
  function meaningful(json) {
    try { const data=progressData(json);delete data.lastSeenUtc;return JSON.stringify(data,Object.keys(data).sort()); } catch {return "";}
  }
  function cloudLocal() {
    const json=latestProgress || stored(journalKey(progressKey)) || cloudContext?.data || "";
    return json?JSON.stringify(progressData(json)):"";
  }
  function cloudCall(promise) {
    return new Promise((resolve,reject)=>{
      const timeout=root.setTimeout(()=>reject(new Error("Cloud timeout")),20000);
      Promise.resolve(promise).then(resolve,reject).finally(()=>root.clearTimeout(timeout));
    });
  }
  function readCloudData(player) {
    // getPlayer() and getData() cache their results. A Player is used for one read/merge/write
    // cycle only; reusing it must never turn its old local cache into a new server snapshot.
    if(cloudReadHandles.has(player)) {const error=new Error("Waiting for a fresh Player");error.cachedPlayer=true;return Promise.reject(error);}
    cloudReadHandles.add(player);return cloudCall(player.getData([cloudKey]));
  }
  function cloudNotify(state) { cloudState={...state,nextSyncUtc:Math.ceil(cloudAfter/1000)};send("OnBrowserCloud",JSON.stringify(cloudState)); }
  function saveCloudMeta() { store(cloudMetaKey,JSON.stringify(cloudMeta)); }
  function cancelCloudTimer() { if(cloudTimer!==undefined) root.clearTimeout(cloudTimer);cloudTimer=undefined; }
  function scheduleCloud() {
    if(disposed || !cloudContext || !cloudAccount || cloudBusy || cloudDecision || !["local","saved","error","waiting"].includes(cloudState.status)) return;
    if(cloudTimer===undefined) cloudTimer=root.setTimeout(()=>{cloudTimer=undefined;syncCloud();},Math.max(100,cloudAfter-Date.now()));
  }
  function validRemote(value) {
    if(value==null) return null;
    if(![1,2,3].includes(value.version) || typeof value.revision!=="string" || !/^[a-zA-Z0-9-]{1,100}$/.test(value.revision) || typeof value.data!=="string" || value.data.length>180000) throw new Error("Invalid cloud save");
    const data=JSON.parse(value.data);
    if(!data || !Number.isInteger(data.version) || data.version<1 || data.version>16) throw new Error("Unsupported cloud save");
    if(value.version>=2) validLedger(value.ledger);
    return value;
  }
  function offerCloud(remote,ledger,replaceAccount=false) {
    const token=cloudEpoch+"-"+(++cloudSerial);
    const data=projectLedger(ledger);
    cloudDecision={token,remote,ledger,data,replaceAccount};cancelCloudTimer();
    const authoritative=!!remote && (replaceAccount || coversTransactions(remote.version>=2?warehouseLedger(remote.ledger,remote.data):ledgerFor(remote.data),localMerge.ledger));
    cloudNotify({status:"merged",token,data,local:cloudLocal(),authoritative});
  }
  function reconcileCloud(remote,initial) {
    const remoteLedger=remote?(remote.version>=2?warehouseLedger(remote.ledger,remote.data):ledgerFor(remote.data)):{version:1,warehouseVersion:1,parts:[]};
    const current=cloudLocal();restoreMerge();
    if(!localMerge) {
      const same=remote && (meaningful(current)===meaningful(remote.data) || cloudMeta.account===cloudAccount && [cloudMeta.revision,cloudMeta.pending].includes(remote.revision));
      const switched=cloudMeta.account && cloudMeta.account!==cloudAccount;
      const fresh=initial && !cloudContext.existed;
      if(switched) {
        store(journalKey(progressKey)+".account-backup",stored(journalKey(progressKey))||current);
        localMerge={account:cloudMeta.account,device:newDeviceId(),ledger:ledgerFor(current),observed:progressData(current)};
        offerCloud(remote,remote?remoteLedger:ledgerFor('{"version":16,"coins":80,"tickets":2}'),true);return false;
      }
      localMerge={account:cloudAccount,device:newDeviceId(),ledger:clone(same?remoteLedger:switched||fresh&&remote?{version:1,parts:[]}:ledgerFor(current)),observed:progressData(same?remote.data:switched?'{"version":16}':fresh&&remote?cloudContext.data:current)};
      if(!switched && !(fresh&&remote&&meaningful(current)===meaningful(cloudContext.data))) captureProgress(current);
    }
    if(localMerge.account!==cloudAccount) {
      // Account changes never donate another person's wallet. Its entire journal is retained.
      store(journalKey(progressKey)+".account-backup",stored(journalKey(progressKey))||current);
      offerCloud(remote,remote?remoteLedger:ledgerFor('{"version":16,"coins":80,"tickets":2}'),true);return false;
    }
    if(containsLedger(localMerge.ledger,remoteLedger)) {
      persistJournal(current);
      cloudMeta={account:cloudAccount,revision:remote?.revision||"",pending:""};saveCloudMeta();
      cloudNotify({status:remote?.version===3&&containsLedger(remoteLedger,localMerge.ledger)?"saved":"local"});scheduleCloud();return true;
    }
    const joined=limitLedger(joinedLedger(localMerge.ledger,remoteLedger));
    const independent=!coversTransactions(localMerge.ledger,remoteLedger)&&!coversTransactions(remoteLedger,localMerge.ledger);
    const unfinished=json=>{const p=progressData(json);return p.rollPending||p.batchActive?"roulette":"";};
    const localPending=unfinished(current),remotePending=remote?unfinished(remote.data):"";
    if(independent && (localPending||remotePending)) {cloudNotify({status:"waiting",waitingFor:localPending?"local-"+localPending:"remote-"+remotePending});scheduleCloud();return false;}
    const data=projectLedger(joined);
    if(meaningful(current)!==meaningful(data)) {offerCloud(remote,joined);return false;}
    localMerge.ledger=joined;localMerge.observed=progressData(current);persistJournal(current);
    cloudMeta={account:cloudAccount,revision:remote?.revision||"",pending:""};saveCloudMeta();
    cloudNotify({status:remote?.version===3 && containsLedger(remoteLedger,joined)?"saved":"local"});scheduleCloud();return true;
  }
  async function beginCloud() {
    cancelCloudTimer();const epoch=++cloudEpoch;cloudBusy=false;cloudDecision=null;cloudRead=null;cloudAccount="";cloudAfter=0;
    if(!cloudContext) return;
    if(!playerHandle || account.status!=="authorized") {cloudNotify({status:"guest"});return;}
    try {
      if(typeof playerHandle.getUniqueID!=="function" || typeof playerHandle.getData!=="function" || typeof playerHandle.setData!=="function") throw new Error("Cloud methods unavailable");
      cloudAccount=String(playerHandle.getUniqueID()||"");if(!cloudAccount) throw new Error("Account ID unavailable");
      try {cloudMeta=JSON.parse(stored(cloudMetaKey)||"{}");} catch {cloudMeta={};}
      if(!cloudMeta || typeof cloudMeta!=="object" || Array.isArray(cloudMeta)) cloudMeta={};
      cloudNotify({status:"loading"});cloudBusy=true;
      const data=await readCloudData(playerHandle);
      if(disposed || epoch!==cloudEpoch) return;
      cloudRead={handle:playerHandle,result:data,epoch,readAt:Date.now()};cloudAfter=Date.now()+cloudInterval;
      cloudBusy=false;if(reconcileCloud(validRemote(data?.[cloudKey]),true)) await syncCloud();
    } catch(error) {if(!disposed && epoch===cloudEpoch) {cloudRead=null;cloudBusy=false;cloudAfter=Date.now()+cloudRetryInterval;cloudNotify({status:error.cachedPlayer?"local":"error"});scheduleCloud();}}
  }
  async function syncCloud() {
    if(disposed || !cloudAccount || cloudBusy || cloudDecision || !cloudContext || account.status!=="authorized") return false;
    if(cloudRead && (cloudRead.epoch!==cloudEpoch || Date.now()-cloudRead.readAt>cloudReadWindow)) cloudRead=null;
    if(!cloudRead && Date.now()<cloudAfter) {scheduleCloud();return false;}
    cancelCloudTimer();cloudBusy=true;
    const epoch=cloudEpoch;
    cloudNotify({status:"syncing"});
    try {
      if(!cloudRead) {
        const handle=await cloudCall(sdk.getPlayer());if(disposed || epoch!==cloudEpoch) return false;
        if(accountFrom(handle).status!=="authorized" || String(handle.getUniqueID())!==cloudAccount) {readAccount();return false;}
        const result=await readCloudData(handle);if(disposed || epoch!==cloudEpoch) return false;
        cloudRead={handle,result,epoch,readAt:Date.now()};cloudAfter=Date.now()+cloudInterval;
      }
      const {handle,result}=cloudRead;
      cloudBusy=false;
      if(!reconcileCloud(validRemote(result?.[cloudKey]),false)) return false;
      cloudRead=null;
      const data=cloudLocal();
      const remote=validRemote(result?.[cloudKey]);
      if(remote?.version===3 && containsLedger(remote.ledger,localMerge.ledger)) return true;
      cloudBusy=true;cancelCloudTimer();cloudNotify({status:"syncing"});
      const revision=Date.now().toString(36)+"-"+(++cloudSerial)+"-"+Math.random().toString(36).slice(2);
      const ledger=clone(localMerge.ledger);
      const envelope={version:3,revision,parent:cloudMeta.revision,updatedAt:Date.now(),data,ledger};
      if(JSON.stringify(envelope).length>175000) throw new Error("Cloud ledger capacity exceeded");
      // Persist the in-flight revision first: a lost acknowledgement can be recovered on the next read.
      cloudMeta.pending=revision;saveCloudMeta();
      await cloudCall(handle.setData({[cloudKey]:envelope},true));
      if(disposed || epoch!==cloudEpoch) return false;
      cloudMeta={account:cloudAccount,revision,pending:""};saveCloudMeta();cloudBusy=false;
      cloudNotify({status:meaningful(cloudLocal())===meaningful(data)?"saved":"local"});
      scheduleCloud();return true;
    } catch(error) {if(!disposed && epoch===cloudEpoch) {cloudRead=null;cloudBusy=false;cloudAfter=Date.now()+cloudRetryInterval;cloudNotify({status:error.cachedPlayer?"local":"error"});scheduleCloud();}return false;}
  }
  function resolveCloud(token,useCloud) {
    if(!cloudDecision || token!==cloudDecision.token) return false;
    const decision=cloudDecision;cloudDecision=null;
    if(!useCloud) {reconcileCloud(decision.remote,false);return true;}
    store(journalKey(progressKey)+".before-merge",stored(journalKey(progressKey))||cloudLocal());
    if(decision.remote) store(journalKey(progressKey)+".remote-backup",JSON.stringify(decision.remote));
    if(decision.replaceAccount) localMerge={account:cloudAccount,device:newDeviceId(),ledger:decision.ledger,observed:progressData(decision.data)};
    else {localMerge.ledger=decision.ledger;localMerge.observed=progressData(decision.data);}
    latestProgress=decision.data;
    persistJournal(decision.data);
    cloudNotify({status:"local"});scheduleCloud();
    // Run after Unity has journaled its validated/normalized snapshot, on the same fresh read.
    Promise.resolve().then(()=>syncCloud());return true;
  }

  function send(method, value) {
    if (unity && receiver) unity.SendMessage(receiver, method, value);
  }
  function notify(force) {
    if (disposed) return;
    if (unity && receiver && device && (force || sentDevice !== device)) {
      sentDevice = device; send("OnBrowserDevice", device);
    }
    if (unity && receiver && language && !sentLanguage) {
      sentLanguage = true;
      send("OnBrowserLanguage", language);
    }
    const paused = isPaused();
    if (unity && receiver && (force || paused !== sentPause)) {
      sentPause = paused;
      send("OnBrowserPause", paused ? "1" : "0");
    }
    const available = !!(ready && !local && sdk && sdk.adv && typeof sdk.adv.showRewardedVideo === "function");
    if (unity && receiver && (force || available !== sentRewardedAvailable)) {
      sentRewardedAvailable = available;
      send("OnRewardedAvailable", available ? "1" : "0");
    }
    if (options.onPause) options.onPause(ready && paused);
    const accountJson=JSON.stringify(account);
    if(unity && receiver && (force || accountJson!==sentAccount)) { sentAccount=accountJson;send("OnBrowserAccount",accountJson); }
    if(force && cloudContext) send("OnBrowserCloud",JSON.stringify(cloudState));
    syncGameplay();
  }
  function isPaused() { return disposed || sdkPaused || hidden || blurred || viewportPaused || !!rewardRequest || authPending || !ready; }
  function selectDevice() {
    const reported = sdk?.deviceInfo?.type;
    const touchBrowser = /Android|iPhone|iPad|iPod/i.test(root.navigator?.userAgent || "") ||
      /Macintosh/i.test(root.navigator?.userAgent || "") && root.navigator?.maxTouchPoints > 1;
    device = ["mobile", "tablet", "desktop", "tv"].includes(reported) ? reported : touchBrowser ? "mobile" : "desktop";
    if (options.onDevice) options.onDevice(device);
    notify();
  }
  function requestFullscreen() {
    if (disposed || local || !sdk?.screen?.fullscreen || typeof sdk.screen.fullscreen.request !== "function") return Promise.resolve(false);
    if (sdk.screen.fullscreen.status === "on") return Promise.resolve(true);
    // Called directly from the HTML play button to retain the browser's user gesture.
    try { return Promise.resolve(sdk.screen.fullscreen.request()).then(() => true, () => false); }
    catch { return Promise.resolve(false); }
  }
  function syncGameplay() {
    if (!sdk || local || gameplayUnavailable) return;
    const active = engineActive && !isPaused();
    if (active === gameplayReported) return;
    try {
      const gameplay = sdk.features?.GameplayAPI;
      if (typeof gameplay?.start !== "function" || typeof gameplay?.stop !== "function") return;
      // Set before calling the SDK so synchronous platform events cannot repeat the same signal.
      gameplayReported = active;
      if (active) gameplay.start(); else gameplay.stop();
    } catch (error) {
      gameplayReported = false; gameplayUnavailable = true;
      root.console.warn("Lucky Pickaxe: gameplay reporting unavailable; local pause remains active", error);
    }
  }
  function showRewarded(id) {
    if (isPaused() || local || !unity || !receiver || !sdk || !sdk.adv || typeof sdk.adv.showRewardedVideo !== "function" || typeof id !== "string" || !/^[a-zA-Z0-9-]{1,64}$/.test(id)) return false;
    const request = { id, paid: false };
    rewardRequest = request; notify();
    const finish = result => {
      if (rewardRequest !== request) return;
      // Deliver completion while the ad pause still holds Unity input and audio.
      // The controller saves the result before this pause reason is released.
      send("OnRewardedVideo", id + ":" + result);
      rewardRequest = null; notify();
    };
    try {
      sdk.adv.showRewardedVideo({ callbacks: {
        onOpen() { if (rewardRequest === request) notify(); },
        onRewarded() {
          if (rewardRequest !== request || request.paid) return;
          request.paid = true;
          send("OnRewardedVideo", id + ":rewarded");
        },
        onClose() { finish("closed"); },
        onError() { finish("error"); }
      } });
    } catch (error) { finish("error"); }
    return true;
  }
  function accountFrom(player) {
    const authorized=!!(player && typeof player.isAuthorized==="function" && player.isAuthorized());
    return {available:true,status:authorized?"authorized":"guest",name:authorized&&typeof player.getName==="function"?String(player.getName()||"").slice(0,40):""};
  }
  function readAccount() {
    if(!sdk || typeof sdk.getPlayer!=="function" || typeof sdk.auth?.openAuthDialog!=="function") return;
    const generation=++accountGeneration;account={available:true,status:"checking",name:""};notify();
    Promise.resolve().then(()=>disposed?null:sdk.getPlayer()).then(player=>{if(!disposed && generation===accountGeneration){playerHandle=player;account=accountFrom(player);notify();beginCloud();}}).catch(()=>{if(!disposed && generation===accountGeneration){playerHandle=null;account={available:true,status:"guest",name:""};notify();beginCloud();}});
  }
  function authorize() {
    if(disposed || !ready || local || isPaused() || !account.available || authPending || account.status==="authorized") return false;
    const generation=++accountGeneration;ratingCache=null;ratingLoadAfter=0;authPending=true;account={available:true,status:"pending",name:""};notify();
    let dialog;
    try { dialog=sdk.auth.openAuthDialog(); }
    catch(error) { authPending=false;account={available:true,status:"guest",name:""};notify();return false; }
    Promise.resolve(dialog).then(()=>disposed?null:sdk.getPlayer()).then(player=>{if(!disposed && generation===accountGeneration) {playerHandle=player;account=accountFrom(player);}}).catch(()=>{if(!disposed && generation===accountGeneration) {playerHandle=null;account={available:true,status:"guest",name:""};}}).finally(()=>{if(!disposed && generation===accountGeneration){authPending=false;notify();beginCloud();}});
    return true;
  }
  function fail(error) {
    if (disposed) return;
    failed = true; ready = false; notify();
    root.console.error("Lucky Pickaxe: browser initialization failed", error);
    if (options.onError) options.onError(error);
  }
  function ratingFailure(error) {
    return error?.code==="LEADERBOARD_PLAYER_NOT_PRESENT";
  }
  function requestRating() {
    if(disposed || !ready || local || !sdk?.leaderboards || typeof sdk.leaderboards.getEntries!=="function") return false;
    if(ratingLoadPending && ratingLoadGeneration===accountGeneration) return true;
    if(Date.now()<ratingLoadAfter) {if(ratingCache) send("OnBrowserRating",JSON.stringify(ratingCache));return true;}
    const generation=accountGeneration;ratingLoadGeneration=generation;ratingLoadPending=true;ratingLoadAfter=Date.now()+30000;
    send("OnBrowserRating",JSON.stringify({status:"loading",entries:[]}));
    Promise.resolve().then(()=>sdk.leaderboards.getEntries(ratingName,{quantityTop:5,includeUser:account.status==="authorized",quantityAround:2})).then(data=>{
      if(disposed || generation!==accountGeneration) return;
      const userRank=Number.isSafeInteger(data?.userRank)&&data.userRank>0?data.userRank:0;
      const ranks=new Set();
      const entries=(Array.isArray(data?.entries)?data.entries:[]).slice(0,20).filter(e=>e && Number.isSafeInteger(e.rank)&&e.rank>0&&Number.isSafeInteger(e.score)&&e.score>=0&&e.score<=2147483647&&!ranks.has(e.rank)&&ranks.add(e.rank)).map(e=>{
        let extra;try {if(typeof e.extraData==="string" && e.extraData.length<=512) extra=JSON.parse(e.extraData);} catch {}
        const hasStats=extra?.version===1&&Number.isSafeInteger(extra.blocks)&&extra.blocks>=0&&extra.blocks<=1e12&&Number.isSafeInteger(extra.resets)&&extra.resets>=0&&extra.resets<=1e6;
        return {rank:e.rank,score:e.score,name:String(e.player?.publicName||"Игрок").slice(0,40),me:userRank>0&&e.rank===userRank,hasStats,blocks:hasStats?extra.blocks:0,resets:hasStats?extra.resets:0};
      }).sort((a,b)=>a.rank-b.rank);
      ratingCache={status:"ready",userRank,entries};send("OnBrowserRating",JSON.stringify(ratingCache));
    }).catch(()=>{if(!disposed && generation===accountGeneration) {ratingCache={status:"error",entries:[]};send("OnBrowserRating",JSON.stringify(ratingCache));}}).finally(()=>{if(generation===ratingLoadGeneration) ratingLoadPending=false;});
    return true;
  }
  function submitRating(json) {
    if(disposed || !ready || local || account.status!=="authorized" || authPending || ratingSendPending || Date.now()<ratingSendAfter || !sdk?.leaderboards || typeof sdk.leaderboards.setScore!=="function" || typeof sdk.leaderboards.getPlayerEntry!=="function" || typeof sdk.isAvailableMethod!=="function" || typeof json!=="string" || json.length>512) return false;
    let data;try {data=JSON.parse(json);} catch {return false;}
    if(data?.version!==1 || !Number.isSafeInteger(data.score) || data.score<=0 || data.score>2147483647 || ![data.blocks,data.coins,data.tickets,data.xp].every(n=>Number.isSafeInteger(n)&&n>=0&&n<=1e12) || !Number.isSafeInteger(data.resets) || data.resets<0 || data.resets>1e6) return false;
    const generation=accountGeneration;ratingSendPending=true;ratingSendAfter=Date.now()+30000;
    Promise.resolve().then(async()=>{
      if(!await sdk.isAvailableMethod("leaderboards.setScore") || !await sdk.isAvailableMethod("leaderboards.getPlayerEntry")) throw new Error("Rating unavailable");
      let previous=0;
      try {const entry=await sdk.leaderboards.getPlayerEntry(ratingName);if(Number.isSafeInteger(entry?.score)&&entry.score>0) previous=entry.score;}
      catch(error) {if(!ratingFailure(error)) throw error;}
      if(disposed || generation!==accountGeneration) return;
      if(data.score>previous) await sdk.leaderboards.setScore(ratingName,data.score,JSON.stringify(data));
      if(disposed || generation!==accountGeneration) return;
      ratingCache=null;ratingLoadAfter=0;
      send("OnBrowserRatingSaved",JSON.stringify({status:"saved",score:Math.max(data.score,previous)}));
    }).catch(()=>{if(!disposed && generation===accountGeneration) send("OnBrowserRatingSaved",JSON.stringify({status:"error"}));}).finally(()=>{ratingSendPending=false;});
    return true;
  }
  function finishReady() {
    if (disposed || ready || failed || !gameReady || !unity || !languageApplied || (!sdk && !local)) return;
    ready = true;
    // Remove our loading cover before reporting that the Unity interface is usable.
    if (options.onReady) options.onReady(local);
    try { if (sdk) sdk.features.LoadingAPI.ready(); }
    catch (error) { fail(error); return; }
    notify(true);
  }
  function listen(target, event, callback) {
    target.addEventListener(event, callback);
    removers.push(() => target.removeEventListener(event, callback));
  }
  function selectLanguage(value) {
    requestedLanguage = typeof value === "string" && /^[a-z]{2}(?:[-_][a-z0-9]{2,8})*$/i.test(value.trim())
      ? value.trim().toLowerCase().split(/[-_]/)[0] : "ru";
    // The draft currently declares one complete translation: Russian.
    language = requestedLanguage === "ru" ? requestedLanguage : "ru";
    notify();
  }
  function init(settings) {
    if (disposed) return initialization || Promise.resolve();
    if (initialization) return initialization;
    options = settings || {};
    local = loopback && new URLSearchParams(root.location.search).get("local") === "1";
    listen(root.document, "visibilitychange", () => { hidden = !!root.document.hidden;if(hidden){send("OnBrowserSave", "");syncCloud();}notify(); });
    listen(root, "blur", () => { blurred = true; notify(); });
    listen(root, "focus", () => { blurred = false; hidden = !!root.document.hidden; notify(); });
    listen(root, "pagehide", event => {
      send("OnBrowserSave", "");syncCloud(); hidden = true; notify();
      if (event?.persisted === false) dispose();
    });
    listen(root, "pageshow", () => { hidden = !!root.document.hidden; blurred = false; notify(); });
    if (local) { initialization = Promise.resolve(); selectDevice(); selectLanguage("ru"); finishReady(); return initialization; }
    initialization = new Promise((resolve, reject) => {
      if (root.YaGames) { resolve(); return; }
      const script = root.document.createElement("script");
      script.src = "/sdk.js"; script.async = true;
      script.onload = resolve; script.onerror = () => reject(new Error("Yandex SDK unavailable"));
      root.document.head.appendChild(script);
    }).then(() => disposed ? null : root.YaGames.init()).then(value => {
      if (disposed) return;
      if (!value || !value.features || !value.features.LoadingAPI || typeof value.features.LoadingAPI.ready !== "function" || typeof value.on !== "function")
        throw new Error("Incomplete Yandex SDK");
      sdk = value;
      selectDevice();
      const pause = () => { sdkPaused = true; notify(); };
      const resume = () => { sdkPaused = false; notify(); };
      sdk.on("game_api_pause", pause); sdk.on("game_api_resume", resume);
      removers.push(() => { sdk.off("game_api_pause", pause); sdk.off("game_api_resume", resume); });
      selectLanguage(sdk.environment?.i18n?.lang);
      readAccount();
      finishReady();
    }).catch(error => { if (!disposed) { fail(error); throw error; } });
    return initialization;
  }
  function storageWarning(error) {
    if (warnedStorage) return;
    warnedStorage = true;
    root.console.warn("Lucky Pickaxe: synchronous journal unavailable; PlayerPrefs remains active", error);
  }
  function dispose() {
    if (disposed) return;
    disposed = true; ready = false; engineActive = false;cloudRead=null;cancelCloudTimer();++cloudEpoch;
    if (sentPause !== true) { sentPause = true; send("OnBrowserPause", "1"); }
    syncGameplay(); rewardRequest = null;authPending=false;
    while (removers.length) {
      try { removers.pop()(); }
      catch (error) { root.console.warn("Lucky Pickaxe: platform listener cleanup failed", error); }
    }
    unity = receiver = null;
  }
  root.LuckyPickaxe = {
    init,
    attachReceiver(name) { if (!disposed) { receiver = name; notify(true); } },
    attachUnity(instance) { if (!disposed) { unity = instance; notify(true); finishReady(); } },
    markReady() { if (!disposed) { gameReady = true; finishReady(); } },
    confirmLanguage(value) {
      if (disposed || languageApplied || value !== language || !language) return;
      languageApplied = true;
      if (options.onLanguage) options.onLanguage(language);
      finishReady();
    },
    setGameplay(active) { if (!disposed) { engineActive = active === true; syncGameplay(); } },
    setViewportPaused(paused) { if (!disposed && viewportPaused !== (paused === true)) { viewportPaused = paused === true; notify(); } },
    cancelInput() { if (!disposed) send("OnBrowserInputCancel", ""); },
    requestFullscreen,
    showRewarded,
    authorize,
    startCloud(json,existed) {if(disposed) return;cloudContext={data:json,existed:!!existed};beginCloud();},
    syncCloud,
    resolveCloud,
    noteOfflineClaim(json) {
      try {const claim=JSON.parse(json);if(Number.isSafeInteger(claim.base)&&claim.base>0&&claim.base<=1e12&&Number.isSafeInteger(claim.credit)&&(claim.credit===claim.base||claim.credit===claim.base*3))pendingWarehouseClaim=claim;}catch{}
    },
    requestRating,
    submitRating,
    readProgress(key) {
      try {
        const json=root.localStorage.getItem(journalKey(key));
        if(key===progressKey && json) {restoreMerge();return JSON.stringify(progressData(json));}
        return json;
      }
      catch (error) { storageWarning(error); return null; }
    },
    writeProgress(key, json) {
      // setItem replaces one complete JSON atomically; no asynchronous write window.
      // Cloud must also see the newest snapshot when localStorage is blocked or full.
      if(key===progressKey) {
        try {
          restoreMerge();const changed=meaningful(cloudLocal())!==meaningful(json);latestProgress=json;
          try {if(localMerge)captureProgress(json);else pendingWarehouseClaim=null;} catch(error) {cloudNotify({status:"error"});storageWarning(error);}
          const saved=persistJournal(json);
          if(changed && cloudState.status==="saved") cloudNotify({status:"local"});scheduleCloud();return saved;
        } catch(error) {storageWarning(error);return false;}
      }
      try { root.localStorage.setItem(journalKey(key), json);return true; }
      catch (error) { storageWarning(error); return false; }
    },
    dispose,
    status() { return { ready, local, sdk: !!sdk, paused: isPaused(), failed, device, viewportPaused, rewardedPending: !!rewardRequest, requestedLanguage, language, languageApplied, engineActive, gameplayReported, disposed, accountStatus:account.status, authorizationPending:authPending, ratingLoadPending, ratingSendPending,cloudStatus:cloudState.status }; }
  };
})(window);
