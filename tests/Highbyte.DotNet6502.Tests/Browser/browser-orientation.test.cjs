const assert=require('node:assert/strict');
    const {fixture}=require('./browser-zoom-fixture.cjs');
    const test = require('node:test');
function phone(settings={}) {
 const calls=[]; let orientation; let timeout;
    const f=fixture(390,844,({document,window,elements})=>{
  elements.get('browser-orientation-status').hidden=true;
  window.matchMedia=()=>({matches:settings.touch!==false,addEventListener(){}});
  window.clearTimeout=()=>{};
  window.setTimeout=callback=>{timeout=callback;return 1;};
  function resize(w,h){document.documentElement.clientWidth=w; document.documentElement.clientHeight=h;window.innerHeight=h;window.handlers.resize?.();}
  orientation={type:'portrait-primary',handlers:{},addEventListener(event,callback){this.handlers[event]=callback;},
   async lock(target){calls.push(['lock',target]); if(settings.lockError)throw {name:settings.lockError};
    this.type=target+'-primary';resize(target==='landscape'?844:390,target==='landscape'?390:844);this.handlers.change();},
   unlock(){calls.push(['unlock']);if(settings.unlockError)throw {name:'NotSupportedError'};}
  };
  window.screen={orientation};
  document.fullscreenEnabled=settings.fullscreen!==false;
  if(settings.noLock)delete orientation.lock;
  document.documentElement.requestFullscreen=async()=>{
   calls.push(['fullscreen']);if(settings.fullscreenError)throw {name:'NotAllowedError'};
   document.fullscreenElement=document.documentElement;document.handlers.fullscreenchange();
  };
  document.exitFullscreen=async()=>{
   calls.push(['exit']);if(settings.exitError)throw {name:'NotAllowedError'};
   document.fullscreenElement=null;document.handlers.fullscreenchange();
  };
  if(settings.alreadyFullscreen)document.fullscreenElement=document.documentElement;
 });
 return {...f,calls,orientation,rotate:()=>f.elements.get('browser-orientation-toggle').handlers.click(),
  button:()=>f.elements.get('browser-orientation-toggle'),status:()=>f.elements.get('browser-orientation-status'),dismiss:()=>timeout()};
}
test('Orientation requests, fullscreen ownership, Reset and rejected requests', async (t) => {
 const f=phone();
    f.size(500,300);
    assert.deepEqual(f.calls,[],'no automatic fullscreen or orientation request on startup');
    assert.equal(f.button().hidden,false);
    assert.match(f.button().attributes['aria-label'],/landscape.*fullscreen/);
    const rotation=f.rotate();
    assert.equal(f.button().disabled,true);
    assert.equal(f.elements.get('browser-zoom-reset').disabled,true);
    await f.rotate();
    assert.equal(f.calls.filter(c=>c[0]==='fullscreen').length,1,'rapid second tap ignored');
    await rotation;
    assert.equal(f.button().disabled,false);
    assert.equal(f.elements.get('browser-zoom-reset').disabled,false);
    assert.equal(f.orientation.type,'landscape-primary');
    assert.match(f.button().attributes['aria-label'],/portrait/);
    assert.equal(f.elements.get('browser-zoom-controls').hidden,false,'controls stay reachable when locked app fits');
    assert.equal(f.status().hidden,false);
    await f.rotate();
    assert.equal(f.orientation.type,'portrait-primary');
    assert.equal(f.calls.filter(c=>c[0]==='fullscreen').length,1,'second rotation reuses fullscreen');
    f.click('reset');
    await Promise.resolve();
    assert.equal(f.zoom(),1);
    assert.equal(f.document.fullscreenElement,null);
    assert.ok(f.calls.some(c=>c[0]==='unlock'));
    assert.ok(f.calls.some(c=>c[0]==='exit'));
    f.dismiss();
    assert.equal(f.status().hidden,true);
    const previouslyFullscreen=phone({alreadyFullscreen:true});
    previouslyFullscreen.size(1258,764);
    await previouslyFullscreen.rotate();
    previouslyFullscreen.click('reset');
    assert.equal(previouslyFullscreen.calls.some(c=>c[0]==='exit'),false,'Reset preserves fullscreen entered outside Rotate');
    const exiting=phone();
    exiting.size(500,300);
    await exiting.rotate();
    await exiting.document.exitFullscreen();
    assert.ok(exiting.calls.some(c=>c[0]==='unlock'),'browser fullscreen exit restores auto orientation');
    assert.equal(exiting.elements.get('browser-zoom-controls').hidden,true,'when unlocked app fits controls hide again');
 for(const setup of [{touch:false},{fullscreen:false},{noLock:true}]) {
  const absent=phone(setup);absent.size(1258,764);
    assert.equal(absent.button().hidden,true);
    await absent.rotate();
    assert.deepEqual(absent.calls,[]);
 }
 const warnings=[];
    const savedWarn=console.warn; t.after(() => { console.warn=savedWarn; }); console.warn=(...args)=>warnings.push(args);
    const denied=phone({fullscreenError:true});
    denied.size(1258,764);
    await denied.rotate();
    assert.equal(denied.calls.some(c=>c[0]==='lock'),false,'fullscreen denial does not request rotation');
    assert.equal(denied.button().disabled,false);
    assert.match(denied.status().textContent,/Fullscreen was not allowed/);
    assert.equal(warnings.at(-1)[1].stage,'fullscreen');
    assert.equal(warnings.at(-1)[1].error,'NotAllowedError');
    const unsupported=phone({lockError:'NotSupportedError',unlockError:true});
    unsupported.size(1258,764);
    await unsupported.rotate();
    assert.ok(unsupported.calls.some(c=>c[0]==='exit'),'failed orientation rolls back fullscreen it entered');
    assert.equal(unsupported.document.fullscreenElement,null);
    assert.equal(unsupported.button().hidden,false);
    assert.equal(unsupported.button().disabled,true);
    assert.match(unsupported.status().textContent,/device toolbar/);
    assert.equal(warnings.at(-1)[1].stage,'orientation-lock');
    assert.equal(warnings.at(-1)[1].error,'NotSupportedError');
    assert.match(unsupported.button().attributes['aria-label'],/unavailable/);
    const callCount=unsupported.calls.length;
    await unsupported.rotate();
    assert.equal(unsupported.calls.length,callCount,'unsupported rotation cannot be retried');
    unsupported.click('reset');
    assert.equal(unsupported.status().hidden,true,'Reset dismisses unsupported explanation');
    const deniedLock=phone({alreadyFullscreen:true,lockError:'SecurityError'});deniedLock.size(1258,764);
    await deniedLock.rotate();
    assert.equal(deniedLock.calls.some(c=>c[0]==='exit'),false,'failed rotation preserves preexisting fullscreen');
    assert.equal(deniedLock.button().hidden,false);
    assert.equal(deniedLock.button().disabled,false);
    const blockedExit=phone({lockError:'NotSupportedError',exitError:true});blockedExit.size(1258,764);
    await blockedExit.rotate();
    assert.match(blockedExit.status().textContent,/browser controls to exit fullscreen/);
 console.warn=savedWarn;
});
