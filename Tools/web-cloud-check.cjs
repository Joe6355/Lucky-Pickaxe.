"use strict";
const {fixture,start}=require("./web-platform-check.cjs");
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path");
let checks=0;
function check(value,label){checks++;assert.ok(value,label);console.log("PASS "+checks+": "+label);}
const key="MineLoot.Progress.v2",journal="LuckyPickaxe."+key+".journal",meta="LuckyPickaxe.cloud.v1",cloudKey="luckyPickaxeSave";
const progress=(values={})=>JSON.stringify({version:16,coins:80,tickets:2,totalClicks:0,lastSeenUtc:123,...values});
const envelope=(data,revision="remote-a")=>({version:1,revision,updatedAt:123,data});
async function settle(){for(let i=0;i<8;i++) await new Promise(resolve=>setImmediate(resolve));}
async function device(cloud,data,existed,storage=new Map(),extra={}){
  const f=fixture({auth:true,authorized:true,cloud,storage,...extra});await start(f);
  f.api.writeProgress(key,data);f.api.startCloud(data,existed);await settle();return f;
}
function event(f){return JSON.parse(f.calls.filter(c=>c.method==="OnBrowserCloud").at(-1).value);}
async function choose(f,remote){const choice=event(f);if(remote) f.api.writeProgress(key,choice.data||progress());check(f.api.resolveCloud(choice.token,remote),"Only the current decision token is acknowledged");await settle();}
async function run(){
  let cloud={data:{},writes:[]};let f=await device(cloud,progress({coins:777,tickets:0,rollPending:true,pendingTypes:[4,1,2,1,1]}),true);
  check(f.api.status().cloudStatus==="local" && cloud.writes.length===0,"Existing local progress is retained until the cloud acknowledges a write");
  await f.api.syncCloud();await settle();
  check(cloud.writes.length===1 && cloud.writes[0].flush===true && event(f).status==="saved","Cloud uses setData with flush=true and confirms only its successful promise");
  check(JSON.parse(cloud.data[cloudKey].data).rollPending && JSON.parse(cloud.data[cloudKey].data).tickets===0,"Paid pending picks and the spent final ticket travel together");
  f.api.writeProgress(key,progress({coins:778}));f.advanceClock(15000);f.api.writeProgress(key,progress({coins:779}));f.advanceClock(15000);await settle();
  check(cloud.writes.length===2 && JSON.parse(cloud.data[cloudKey].data).coins===779,"Continuous play cannot postpone the thirty-second autosave indefinitely");
  const storage=f.data;f.api.dispose();f=await device(cloud,storage.get(journal),true,storage);
  check(event(f).status==="saved" && cloud.writes.length===2,"A same-device reload recognizes the acknowledged revision without a duplicate payment or write");
  let second=await device(cloud,progress(),false);
  check(event(second).status==="remote","A fresh second device receives the existing cloud version");await choose(second,true);
  check(JSON.parse(second.data.get(journal)).coins===779 && event(second).status==="saved","Second-device load restores the cloud wallet without adding the starter wallet");
  f.api.writeProgress(key,progress({coins:800}));second.api.writeProgress(key,progress({coins:900}));await second.api.syncCloud();await settle();f.advanceClock(30000);await settle();
  check(event(f).status==="choice" && cloud.writes.length===3,"A changed remote revision is offered as a conflict before any overwrite");
  const token=event(f).token;check(!f.api.resolveCloud(token+"-stale",true),"A stale conflict response cannot replace the current save");await choose(f,true);
  check(JSON.parse(f.data.get(journal)).coins===900,"Choosing cloud replaces the local wallet exactly");
  f.api.dispose();second.api.dispose();
  cloud={data:{[cloudKey]:envelope(progress({coins:4444}))},writes:[]};
  f=await device(cloud,progress({coins:5555}),true);
  check(event(f).status==="choice" && cloud.writes.length===0,"An existing unbound local game never silently overwrites another device");await choose(f,false);
  check(JSON.parse(cloud.data[cloudKey].data).coins===5555 && cloud.writes.length===1,"Explicit local choice uploads one complete snapshot");f.api.dispose();
  const switchedStorage=new Map([[meta,JSON.stringify({account:"old-account",revision:"old"})]]);
  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:9999}),true,switchedStorage,{accountID:"other-account"});
  check(event(f).status==="choice" && cloud.writes.length===0,"Changing accounts requires a choice even when the new cloud is empty");await choose(f,true);
  check(JSON.parse(f.data.get(journal)).coins===80,"An empty cloud can start a fresh game without leaking the previous account's wallet");f.api.dispose();
  let fail=true;cloud={data:{},writes:[],read:async()=>{if(fail) throw new Error("offline");return cloud.data;}};
  f=await device(cloud,progress({coins:321}),true);
  check(event(f).status==="error" && f.data.get(journal).includes("321") && cloud.writes.length===0,"A failed cloud read keeps the local save and prohibits a blind upload");
  fail=false;f.advanceClock(30000);await settle();check(event(f).status==="saved" && JSON.parse(cloud.data[cloudKey].data).coins===321,"Cloud recovers automatically after the network returns");f.api.dispose();
  let lost=true;cloud={data:{},writes:[],write:async(value,flush)=>{cloud.data=JSON.parse(JSON.stringify(value));cloud.writes.push({value,flush});if(lost) {lost=false;throw new Error("acknowledgement lost");}}};
  f=await device(cloud,progress({coins:432}),true);await f.api.syncCloud();await settle();
  check(event(f).status==="error" && JSON.parse(f.data.get(meta)).pending,"An ambiguous write retains its pending revision durably");
  f.api.dispose();f=await device(cloud,progress({coins:432}),true,f.data);
  check(event(f).status==="local","An unacknowledged cached revision is retried rather than claimed as saved");f.advanceClock(30000);await settle();
  check(event(f).status==="saved" && cloud.writes.length===2 && JSON.parse(cloud.data[cloudKey].data).coins===432,"Lost acknowledgement recovery repeats an idempotent snapshot without duplicating currency");f.api.dispose();
  cloud={data:{[cloudKey]:{version:99,data:"broken"}},writes:[]};f=await device(cloud,progress({coins:876}),true);
  check(event(f).status==="error" && cloud.writes.length===0 && JSON.parse(f.data.get(journal)).coins===876,"Corrupt cloud data cannot overwrite either copy automatically");f.api.dispose();
  let resolveLate;cloud={data:{},writes:[],read:()=>new Promise(resolve=>{resolveLate=resolve;})};
  f=await device(cloud,progress(),false);f.api.writeProgress(key,progress({coins:91,totalClicks:1}));
  resolveLate({[cloudKey]:envelope(progress({coins:700}))});await settle();
  check(event(f).status==="choice" && JSON.parse(f.data.get(journal)).coins===91,"A player who starts mining during a slow first cloud read receives a choice instead of losing new clicks");f.api.dispose();
  cloud={data:{},writes:[],read:()=>new Promise(resolve=>{resolveLate=resolve;})};
  f=await device(cloud,progress({coins:765}),true);f.advanceClock(20000);await settle();
  check(event(f).status==="error" && JSON.parse(f.data.get(journal)).coins===765,"A hung SDK request times out after twenty seconds while local play remains saved");
  resolveLate({[cloudKey]:envelope(progress({coins:999}))});await settle();
  check(event(f).status==="error" && cloud.writes.length===0,"A response arriving after its timeout cannot install an obsolete snapshot");f.api.dispose();
  let releaseWrite;cloud={data:{},writes:[],write:async(value,flush)=>{cloud.data=JSON.parse(JSON.stringify(value));cloud.writes.push({value,flush});await new Promise(resolve=>{releaseWrite=resolve;});}};
  f=await device(cloud,progress({coins:100}),true);const writing=f.api.syncCloud();await settle();
  f.api.writeProgress(key,progress({coins:110,totalClicks:1}));releaseWrite();await writing;await settle();
  check(event(f).status==="local" && JSON.parse(f.data.get(journal)).coins===110,"Mining during an in-flight write keeps newer local progress dirty after the older snapshot is acknowledged");f.api.dispose();
  cloud={data:{},writes:[]};f=await device(cloud,progress({coins:100}),true,new Map(),{storageBlocked:true});
  f.api.writeProgress(key,progress({coins:111,totalClicks:1}));await f.api.syncCloud();await settle();
  check(event(f).status==="saved" && JSON.parse(cloud.data[cloudKey].data).coins===111,"Cloud uploads the latest in-memory snapshot even when localStorage is blocked or full");f.api.dispose();
  cloud={data:{},writes:[]};f=await device(cloud,progress(),true,new Map(),{authorized:false});
  check(event(f).status==="guest" && cloud.writes.length===0,"A guest stays local and is never forced into the login dialog");f.api.dispose();
  fs.writeFileSync(path.join(__dirname,"../Logs/web-cloud-check.json"),JSON.stringify({result:"PASS",checks}));
}
run().catch(error=>{console.error(error);process.exitCode=1;});
