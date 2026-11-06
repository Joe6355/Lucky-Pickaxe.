"use strict";
const {fixture,start}=require("./web-platform-check.cjs");
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path");
let checks=0;
function check(value,label){checks++;assert.ok(value,label);console.log("PASS "+checks+": "+label);}
const key="MineLoot.Progress.v2",journal="LuckyPickaxe."+key+".journal",meta="LuckyPickaxe.cloud.v1",cloudKey="luckyPickaxeSave";
const progress=(values={})=>JSON.stringify({version:16,coins:80,tickets:2,totalClicks:0,lastSeenUtc:123,...values});
const envelope=(data,revision="remote-a")=>({version:1,revision,updatedAt:123,data});
const read=f=>JSON.parse(f.api.readProgress(key));
const remote=cloud=>JSON.parse(cloud.data[cloudKey].data);
const event=f=>JSON.parse(f.calls.filter(c=>c.method==="OnBrowserCloud").at(-1).value);
const crate=(amount)=>({offlineCoins:amount,offlineRewardSeconds:10,offlineElapsedSeconds:10,offlineRewardRateMilli:amount*200,offlineRewardCapacity:14400});
function claimWarehouse(f,multiplier=1,base=read(f).offlineCoins){
  const p=read(f),credit=base*multiplier;
  f.api.noteOfflineClaim(JSON.stringify({base,credit}));
  f.api.writeProgress(key,JSON.stringify({...p,coins:p.coins+credit,offlineCoins:p.offlineCoins-base,earnedCoins:(p.earnedCoins||0)+credit,totalProduced:(p.totalProduced||0)+credit,totalOfflineCoins:(p.totalOfflineCoins||0)+credit}));
}
async function settle(){for(let i=0;i<12;i++) await new Promise(resolve=>setImmediate(resolve));}
async function device(cloud,data,existed,storage=new Map(),extra={}){
  const f=fixture({auth:true,authorized:true,cloud,storage,playerDataCache:true,memoizedPlayer:true,...extra});await start(f);
  f.api.readProgress(key);f.api.writeProgress(key,data);f.api.startCloud(data,existed);await settle();return f;
}
async function apply(f){
  const pending=event(f);if(pending.status!=="merged") return false;
  assert.ok(f.api.resolveCloud(pending.token,true));
  // Unity first validates the snapshot, acknowledges the ledger, then journals its normalized state.
  f.api.writeProgress(key,pending.data);await settle();return true;
}
async function sync(f){
  f.advanceClock(300001);await settle();await apply(f);
  if(event(f).status==="local") {f.advanceClock(300001);await settle();await apply(f);}
}
async function run(){
  let cloud={data:{},writes:[]};let f=await device(cloud,progress({coins:777,tickets:0,rollPending:true,pendingTypes:[4,1,2,1,1]}),true);
  check(event(f).status==="saved" && cloud.writes.length===1,"An existing local game reads, reconciles and saves at startup without a choice dialog");
  await f.api.syncCloud();await settle();
  check(cloud.writes.length===1 && cloud.writes[0].flush===true && cloud.data[cloudKey].version===3,"Cloud commits the complete merge and reward ledger with flush=true");
  check(remote(cloud).coins===777 && remote(cloud).rollPending && remote(cloud).tickets===0,"A paid draw and its spent final ticket remain one snapshot");
  f.api.writeProgress(key,JSON.stringify({...read(f),lastSeenUtc:124}));
  check(event(f).status==="saved" && cloud.writes.length===1,"Updating only last-seen time does not mark an unchanged cloud snapshot as unsaved");
  f.api.writeProgress(key,progress({coins:778}));f.advanceClock(150000);f.api.writeProgress(key,progress({coins:779}));f.advanceClock(150001);await settle();
  check(cloud.writes.length===2 && remote(cloud).coins===779,"Continuous play cannot postpone the five-minute cloud cycle");
  const storage=f.data;f.api.dispose();f=await device(cloud,storage.get(journal),true,storage);await apply(f);
  check(read(f).coins===779,"Reload restores the atomic journal and its merge metadata without adding the wallet again");
  const before=cloud.writes.length;await sync(f);
  check(remote(cloud).coins===779 && cloud.writes.length===before,"Polling an unchanged cloud does not write or duplicate rewards");
  let second=await device(cloud,progress(),false);check(event(second).status==="merged","A fresh device automatically receives the cloud snapshot");await apply(second);
  check(read(second).coins===779 && read(second).tickets===2,"A fresh device does not add another starter wallet or starter tickets");
  f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true,new Map(),{privatePlayerTrap:true});
  check(event(f).status==="saved" && remote(cloud).coins===100,"Startup works through documented Player methods even when accessing the private transport throws");
  f.api.writeProgress(key,progress({coins:125}));f.advanceClock(299999);await f.api.syncCloud();await settle();
  check(remote(cloud).coins===100 && read(f).coins===125 && cloud.writes.length===1,"Manual requests cannot upload through the old cached Player before five minutes");
  f.advanceClock(2);await settle();
  check(remote(cloud).coins===125 && cloud.writes.length===2,"The first five-minute refresh uploads the newest local snapshot through a new Player");f.api.dispose();

  cloud={data:{[cloudKey]:envelope(progress({coins:40}))},writes:[]};f=await device(cloud,progress({coins:10}),true);
  f.advanceClock(21000);await apply(f);
  check(read(f).coins===50 && cloud.writes.length===0,"A delayed merge preserves both wallets locally but cannot write through its expired read transaction");
  await sync(f);check(remote(cloud).coins===50 && cloud.writes.length===1,"The next fresh Player commits that delayed merge once");f.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100,tickets:1}),true);await f.api.syncCloud();await settle();
  f.api.writeProgress(key,JSON.stringify({...read(f),tickets:0,totalRolls:1,spentTickets:1,rollPending:true,pendingTypes:[1,2,1,1,1],pendingSwapLimit:1}));await sync(f);
  second=await device(cloud,progress(),false);await apply(second);
  second.api.writeProgress(key,JSON.stringify({...read(second),coins:130,earnedCoins:30,rollPending:false,pendingTypes:[0,0,0,0,0],pendingSwapLimit:0,depths:[1,2,1,1,1]}));await sync(second);
  f.api.writeProgress(key,JSON.stringify({...read(f),milestoneBest:[0,0,1]}));await sync(f);
  check(!read(f).rollPending && read(f).coins===130 && read(f).tickets===0,"Achievement observation in a stale tab cannot resurrect a draw completed on another device");
  await sync(f);await sync(second);check(!remote(cloud).rollPending && remote(cloud).coins===130,"That transferred draw's completion and reward remain stable after both devices return");
  f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100,enchantments:1,enchantingExperience:100}),true);await f.api.syncCloud();await settle();
  second=await device(cloud,progress(),false);await apply(second);
  second.api.writeProgress(key,JSON.stringify({...read(second),enchantments:16,enchantingExperience:90,spentEnchantingExperience:10,enchantCount:1}));await sync(second);
  for(let i=1;i<=5;i++) f.api.writeProgress(key,JSON.stringify({...read(f),milestoneBest:[i]}));
  await sync(f);check(read(f).enchantments===16 && read(f).enchantingExperience===90,"New achievement metadata cannot roll back a paid enchantment from the other device");
  f.api.dispose();second.api.dispose();

  cloud={data:{[cloudKey]:envelope(progress({coins:4444,tickets:8,earnedCoins:3000,miningBlocks:40,upgradeLevel:20}))},writes:[]};
  f=await device(cloud,progress({coins:5555,tickets:3,earnedCoins:2000,miningBlocks:20,upgradeLevel:5}),true);
  check(event(f).status==="merged" && !f.api.status().paused,"Divergent legacy saves merge automatically without pausing gameplay");
  const stale=event(f).token;check(!f.api.resolveCloud(stale+"-old",true),"A stale merge token cannot install a snapshot");await apply(f);await sync(f);
  check(read(f).coins===9999 && read(f).tickets===11 && read(f).earnedCoins===5000 && read(f).miningBlocks===60,"Independent legacy wallets, tickets and rating counters are added once");
  check(read(f).upgradeLevel===20,"Merging preserves the stronger purchased pick");
  check(f.data.has(journal+".before-merge") && f.data.has(journal+".remote-backup"),"Both original saves are backed up before automatic merge");
  for(let i=0;i<4;i++) await sync(f);
  check(read(f).coins===9999 && remote(cloud).coins===9999,"Repeated synchronization cannot add the same imported legacy wallet again");
  f.api.dispose();

  cloud={data:{[cloudKey]:envelope(progress({coins:1e12,tickets:1000000,experience:25480}))},writes:[]};
  f=await device(cloud,progress({coins:500,tickets:7,experience:100}),true);await apply(f);await sync(f);
  check(read(f).coins===1e12 && read(f).tickets===1000000 && read(f).experience===25480,"Merged resources obey the existing economy caps");
  f.api.writeProgress(key,JSON.stringify({...read(f),coins:1e12-120,tickets:999999}));await sync(f);
  second=await device(cloud,progress(),false);await apply(second);await sync(f);
  check(read(second).coins===1e12-120 && read(second).tickets===999999,"A saturated import cannot regenerate coins or spent tickets on the next device");
  f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100,tickets:5,experience:100,earnedCoins:100,upgradeLevel:2}),true);await f.api.syncCloud();await settle();
  second=await device(cloud,progress(),false);await apply(second);
  f.api.writeProgress(key,JSON.stringify({...read(f),coins:70,experience:110,upgradeLevel:5,miningBlocks:1}));
  second.api.writeProgress(key,JSON.stringify({...read(second),coins:125,experience:120,earnedCoins:125,miningBlocks:2}));
  await sync(second);await sync(f);await sync(second);
  check(read(f).coins===95 && read(second).coins===95 && remote(cloud).coins===95,"Two-device income and purchases rebase against shared history instead of adding the shared balance twice");
  check(read(f).experience===130 && read(f).miningBlocks===3 && read(f).earnedCoins===125,"Shared XP and MMR history are counted once, new activity from both devices is retained");
  check(read(second).upgradeLevel===5,"The other device receives the higher material without losing an already-paid upgrade");
  check(f.playerCalls>1 && second.playerCalls>1,"Synchronization refreshes server data even when getPlayer and getData both cache the same Player");
  const canonical=JSON.stringify(read(f));for(let i=0;i<3;i++){await sync(f);await sync(second);}
  check(JSON.stringify(read(f))===canonical && read(second).coins===95,"The devices converge and remain stable after repeated reads");
  const partCount=cloud.data[cloudKey].ledger.parts.length;
  for(let i=0;i<4;i++){second.api.writeProgress(key,JSON.stringify({...read(second),coins:read(second).coins+10,earnedCoins:read(second).earnedCoins+10}));await sync(second);await sync(f);}
  check(read(f).coins===135 && cloud.data[cloudKey].ledger.parts.length===partCount,"Many saves update one device component rather than accumulating import receipts forever");
  // A prestige is a real reset, not an invitation to resurrect old pick upgrades from another component.
  f.api.writeProgress(key,JSON.stringify({...read(f),prestigeCount:1,experience:0,upgradeLevel:0,coins:80,tickets:2}));await sync(f);await sync(second);
  check(read(second).prestigeCount===1 && read(second).upgradeLevel===0 && read(second).experience===0,"A new mine keeps its intended reset and does not resurrect the previous run's equipment");
  f.api.dispose();second.api.dispose();

  // Force simultaneous reads: the last server write temporarily omits the other device's component.
  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true);await f.api.syncCloud();await settle();second=await device(cloud,progress(),false);await apply(second);
  f.api.writeProgress(key,JSON.stringify({...read(f),coins:110}));second.api.writeProgress(key,JSON.stringify({...read(second),coins:120}));
  const snapshot=JSON.parse(JSON.stringify(cloud.data)),releases=[];cloud.read=()=>new Promise(resolve=>releases.push(()=>resolve(snapshot)));
  f.advanceClock(300001);second.advanceClock(300001);await settle();check(releases.length===2,"The race fixture holds both remote reads before either write");
  releases.forEach(release=>release());await settle();delete cloud.read;await apply(f);await apply(second);
  await sync(f);await sync(second);await sync(f);
  check(read(f).coins===130 && read(second).coins===130 && remote(cloud).coins===130,"A later poll repairs concurrent last-write-wins writes without losing or duplicating either contribution");
  f.api.dispose();second.api.dispose();

  let lost=true;cloud={data:{},writes:[],write:async(value,flush)=>{cloud.data=JSON.parse(JSON.stringify(value));cloud.writes.push({value,flush});if(lost){lost=false;throw new Error("acknowledgement lost");}}};
  f=await device(cloud,progress({coins:432}),true);await f.api.syncCloud();await settle();
  check(event(f).status==="error" && remote(cloud).coins===432,"An ambiguous acknowledgement retains both the local ledger and the server snapshot");
  const lostStorage=f.data;f.api.dispose();f=await device(cloud,lostStorage.get(journal),true,lostStorage);await apply(f);await sync(f);
  check(read(f).coins===432 && remote(cloud).coins===432 && cloud.writes.length===1,"Reload after a lost acknowledgement does not apply the import or payment a second time");f.api.dispose();

  cloud={data:{[cloudKey]:envelope(progress({coins:600}))},writes:[]};
  f=await device(cloud,progress({coins:100,tickets:0,rollPending:true,pendingTypes:[1,2,1,4,1]}),true);
  check(event(f).status==="waiting" && event(f).waitingFor==="local-roulette" && read(f).rollPending && !f.api.status().paused && cloud.writes.length===0,"An unrelated merge identifies this device's paid draw without blocking tabs or losing its picks");
  f.api.writeProgress(key,JSON.stringify({...read(f),rollPending:false,coins:130}));await sync(f);
  check(read(f).coins===730 && !read(f).rollPending,"Completing that draw allows automatic merge with its earned reward intact");f.api.dispose();

  cloud={data:{[cloudKey]:envelope(progress({coins:600,...crate(100)}))},writes:[]};f=await device(cloud,progress({coins:50,...crate(40)}),true);await apply(f);await sync(f);
  check(read(f).coins===650 && read(f).offlineCoins===140 && !f.api.status().paused,"Independent unclaimed warehouses combine automatically without blocking the main wallet or MMR");
  claimWarehouse(f,3);await sync(f);
  check(read(f).coins===1070 && read(f).offlineCoins===0,"The combined warehouse retains the full x3 video option");f.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100,...crate(90)}),true);await f.api.syncCloud();await settle();second=await device(cloud,progress(),false);await apply(second);
  claimWarehouse(f);claimWarehouse(second,3);await sync(f);await sync(second);await sync(f);
  check(read(f).coins===370 && read(second).coins===370 && !read(f).offlineCoins,"Concurrent normal and x3 claims of the same warehouse keep one best reward instead of paying both");
  check(read(f).earnedCoins===270 && read(f).totalOfflineCoins===270 && read(f).totalProduced===270,"Duplicate claim reconciliation also corrects gross income and MMR counters");
  const settledWarehouse=read(f).coins;for(let i=0;i<4;i++){await sync(f);await sync(second);}
  check(read(f).coins===settledWarehouse && !remote(cloud).offlineCoins,"Repeated reload-free synchronization cannot resurrect or repay a claimed warehouse");
  f.api.writeProgress(key,JSON.stringify({...read(f),...crate(50)}));await sync(f);await sync(second);
  claimWarehouse(second);await sync(second);await sync(f);
  check(read(f).coins===420 && !read(f).offlineCoins,"A later absence creates a new reward after the earlier shared one is settled");
  f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100,...crate(100)}),true);await f.api.syncCloud();await settle();second=await device(cloud,progress(),false);await apply(second);
  claimWarehouse(f,1,40);claimWarehouse(second,3);await sync(f);await sync(second);await sync(f);
  check(read(f).coins===400 && !read(f).offlineCoins,"Overlapping partial and full bonus claims deduplicate the shared portion exactly");
  f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true);await f.api.syncCloud();await settle();
  f.api.writeProgress(key,JSON.stringify({...read(f),...crate(100)}));await sync(f);
  const legacyJournal=JSON.parse(f.data.get(journal)),legacyEnvelope=JSON.parse(JSON.stringify(cloud.data[cloudKey]));
  for(const ledger of [legacyJournal._sync.ledger,legacyEnvelope.ledger]) {delete ledger.warehouseVersion;for(const part of ledger.parts){delete part.warehouseEarned;delete part.warehouseCredited;delete part.warehouseClaims;delete part.warehouseMeta;}}
  legacyEnvelope.version=2;cloud.data[cloudKey]=legacyEnvelope;const oldStorage=new Map([[journal,JSON.stringify(legacyJournal)]]);f.api.dispose();
  f=await device(cloud,JSON.stringify(legacyJournal),true,oldStorage);await apply(f);await sync(f);
  check(read(f).offlineCoins===100 && cloud.data[cloudKey].version===3,"Upgrading the previous cloud format preserves its unclaimed reward exactly once");
  claimWarehouse(f,3);await sync(f);second=await device(cloud,JSON.stringify(legacyJournal),true,new Map([[journal,JSON.stringify(legacyJournal)]]));await apply(second);await sync(second);
  check(read(second).coins===400 && !read(second).offlineCoins,"An older copy cannot restore a warehouse already claimed after migration");f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:1e12-90,...crate(90)}),true);await f.api.syncCloud();await settle();second=await device(cloud,progress(),false);await apply(second);
  claimWarehouse(f);claimWarehouse(second);await sync(f);await sync(second);await sync(f);
  check(read(f).coins===1e12 && read(second).coins===1e12,"Duplicate warehouse receipts are reconciled before wallet saturation");
  f.api.writeProgress(key,JSON.stringify({...read(f),coins:1e12-120}));await sync(f);await sync(second);
  check(read(second).coins===1e12-120,"A purchase after capped duplicate receipts remains paid after synchronization");f.api.dispose();second.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true);await f.api.syncCloud();await settle();const otherStorage=f.data;f.api.dispose();
  cloud={data:{[cloudKey]:envelope(progress({coins:20,tickets:1}))},writes:[]};f=await device(cloud,otherStorage.get(journal),true,otherStorage,{accountID:"other-account"});
  f.api.writeProgress(key,JSON.stringify({...read(f),coins:150}));const switchToken=event(f).token;f.api.resolveCloud(switchToken,false);await settle();await apply(f);await sync(f);
  check(read(f).coins===20 && remote(cloud).coins===20 && f.data.has(journal+".account-backup"),"Changing accounts archives the previous account and never donates its wallet to the new one");f.api.dispose();

  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:321}),true,new Map(),{permanentPlayerCache:true});
  f.api.writeProgress(key,progress({coins:400}));await sync(f);
  check(event(f).status==="local" && read(f).coins===400 && remote(cloud).coins===321 && cloud.writes.length===1,"A longer-lived SDK cache cannot blindly overwrite the cloud or discard newer local income");f.api.dispose();

  let fail=true;cloud={data:{},writes:[],read:async()=>{if(fail) throw new Error("offline");return cloud.data;}};f=await device(cloud,progress({coins:321}),true);
  check(event(f).status==="error" && read(f).coins===321 && cloud.writes.length===0,"A failed cloud read retains local progress and prohibits a blind upload");
  fail=false;await sync(f);check(remote(cloud).coins===321,"Cloud recovers after the network returns");f.api.dispose();
  cloud={data:{[cloudKey]:{version:2,revision:"bad",data:progress(),ledger:{version:1,parts:[{id:"broken"}]}}},writes:[]};f=await device(cloud,progress({coins:876}),true);
  check(event(f).status==="error" && cloud.writes.length===0 && read(f).coins===876,"A corrupt merge ledger cannot overwrite either save");f.api.dispose();
  let resolveLate;cloud={data:{},writes:[],read:()=>new Promise(resolve=>{resolveLate=resolve;})};f=await device(cloud,progress({coins:765}),true);f.advanceClock(20000);await settle();
  check(event(f).status==="error" && read(f).coins===765,"A hung SDK request times out without blocking local play");resolveLate({[cloudKey]:envelope(progress({coins:999}))});await settle();
  check(event(f).status==="error" && cloud.writes.length===0,"A response arriving after its deadline cannot install an obsolete snapshot");f.api.dispose();
  cloud={data:{},writes:[],read:()=>new Promise(resolve=>{resolveLate=resolve;})};f=await device(cloud,progress(),false);
  f.api.writeProgress(key,progress({coins:91,totalClicks:1}));resolveLate({[cloudKey]:envelope(progress({coins:700}))});await settle();
  check(event(f).status==="merged" && JSON.parse(event(f).data).coins===711,"Mining during a slow first cloud read adds only new earnings, without duplicating starter coins");
  const oldToken=event(f).token;f.api.writeProgress(key,progress({coins:101,totalClicks:2}));f.api.resolveCloud(oldToken,false);await settle();
  check(JSON.parse(event(f).data).coins===721 && !f.api.resolveCloud(oldToken,true),"Clicks after a merge proposal are rebased and its obsolete token is invalidated");await apply(f);f.api.dispose();
  let releaseWrite;cloud={data:{},writes:[],write:async(value,flush)=>{cloud.data=JSON.parse(JSON.stringify(value));cloud.writes.push({value,flush});await new Promise(resolve=>{releaseWrite=resolve;});}};
  f=await device(cloud,progress({coins:100}),true);const writing=f.api.syncCloud();await settle();f.api.writeProgress(key,progress({coins:110,totalClicks:1}));releaseWrite();await writing;await settle();
  check(event(f).status==="local" && read(f).coins===110,"Earnings during an in-flight cloud write remain dirty and saved locally");
  cloud.write=undefined;await sync(f);check(remote(cloud).coins===110,"The next autosave uploads those newer earnings exactly once");f.api.dispose();
  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true,new Map(),{storageBlocked:true});f.api.writeProgress(key,progress({coins:111}));await sync(f);
  check(remote(cloud).coins===111,"Blocked localStorage still uploads the latest in-memory snapshot");f.api.dispose();
  cloud={data:{},writes:[]};f=await device(cloud,progress(),true,new Map(),{authorized:false});check(event(f).status==="guest"&&cloud.writes.length===0,"Guests remain local without a forced login");f.api.dispose();
  fs.writeFileSync(path.join(__dirname,"../Logs/web-cloud-check.json"),JSON.stringify({result:"PASS",checks}));
}
run().catch(error=>{console.error(error);process.exitCode=1;});
