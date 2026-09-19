(() => {
  'use strict';
  const overlay = document.getElementById('loading');
  const bar = document.getElementById('progress');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let target = 0, shown = 0, frame = 0, failed = false, completed = false, lastTime = 0;
  const vines = 'M35 119 C55 114 44 58 79 56 C117 52 110 35 147 48 S203 71 236 49 S287 29 320 47 S370 62 406 43 S452 60 481 53 S520 45 548 73 M39 122 C74 144 90 116 126 139 S188 158 220 140 S278 132 312 147 S368 139 400 144 S459 127 489 139 S525 129 549 119';
  const board = 'M42 65 Q39 54 56 53 L185 49 193 54 333 49 340 53 521 54 Q539 55 538 68 L534 91 541 97 536 127 Q538 141 520 142 L366 147 357 143 212 148 204 144 56 143 Q40 142 43 128 L39 111 44 104Z';
  bar.innerHTML = `<svg class="sign" viewBox="0 0 580 200" aria-hidden="true">
    <defs>
      <linearGradient id="wood" x2="0" y2="1"><stop stop-color="#bd8b4e"/><stop offset=".13" stop-color="#9a6434"/><stop offset=".48" stop-color="#754523"/><stop offset=".52" stop-color="#63391e"/><stop offset=".57" stop-color="#89532c"/><stop offset="1" stop-color="#57321c"/></linearGradient>
      <linearGradient id="leaf" x2=".6" y2="1"><stop stop-color="#c4df70"/><stop offset=".5" stop-color="#73a744"/><stop offset="1" stop-color="#2d643a"/></linearGradient>
      <linearGradient id="gold" x2="0" y2="1"><stop stop-color="#fff5c0"/><stop offset="1" stop-color="#e9b964"/></linearGradient>
      <radialGradient id="petal"><stop stop-color="#fff8cb"/><stop offset=".65" stop-color="#ffde9a"/><stop offset="1" stop-color="#d99487"/></radialGradient>
      <clipPath id="board-clip"><path d="${board}"/></clipPath>
      <clipPath id="grown"><rect id="growth-clip" x="20" y="0" width="0" height="200"/></clipPath>
      <path id="leaf-shape" d="M0 0 C-17 -2 -26 -18 -19 -32 C-2 -29 9 -14 0 0Z" fill="url(#leaf)" stroke="#386c36" stroke-width="1"/>
    </defs>
    <path d="${board}" transform="translate(0 7)" fill="#352717" stroke="#302315" stroke-width="5"/>
    <path d="${board}" fill="url(#wood)" stroke="#d3a364" stroke-width="2"/>
    <g clip-path="url(#board-clip)" fill="none" stroke="#321c12" opacity=".3" stroke-width="1.5">
      ${Array.from({length:12},(_,i)=>`<path d="M35 ${60+i*7} Q${130+i*6} ${48+i*7} 290 ${61+i*7} T545 ${58+i*7}"/>`).join('')}
      <ellipse cx="111" cy="100" rx="27" ry="8"/><ellipse cx="111" cy="100" rx="17" ry="4"/><path d="M450 53 439 67 466 62 M71 140 88 128 79 144" stroke-width="3"/>
    </g>
    <path d="M51 98H530" stroke="#331f15" stroke-width="2" opacity=".45"/>
    <g fill="#4e3620" stroke="#ce9b58"><circle cx="61" cy="75" r="4"/><circle cx="519" cy="75" r="4"/><circle cx="61" cy="124" r="4"/><circle cx="519" cy="124" r="4"/></g>
    <text x="290" y="118" class="engraving" fill="#372619" stroke="#c28d51" stroke-width=".5">Loading</text>
    <g clip-path="url(#grown)">
      <text x="290" y="118" class="engraving" fill="url(#gold)" stroke="#ffe7a0" stroke-width=".5">Loading</text>
      <path class="vine-stroke" d="${vines}" stroke="#233c20" stroke-width="10" transform="translate(0 2)"/>
      <path class="vine-stroke" d="${vines}" stroke="#669744" stroke-width="7"/>
      <path class="vine-stroke" d="${vines}" stroke="#bfcc6a" stroke-width="1.5" transform="translate(0 -2)"/>
    </g>
    <g id="leaves"></g><g id="flowers"></g>
  </svg>`;
  const leafNodes = [];
  const ns = 'http://www.w3.org/2000/svg';
  function growthNode(parent, x, y, rotation, markup, type) {
    const anchor = document.createElementNS(ns,'g');
    anchor.setAttribute('transform',`translate(${x} ${y}) rotate(${rotation})`);
    const node = document.createElementNS(ns,'g');
    node.setAttribute('class',`growth ${type}`);
    node.innerHTML = markup;
    anchor.appendChild(node);parent.appendChild(anchor);
    leafNodes.push({node, threshold:(x-20)/540*100});
  }
  const leaves = document.getElementById('leaves');
  for (let i=0;i<17;i++) {
    const x=55+i*29;
    const y=50+Math.sin(i*1.8)*9;
    growthNode(leaves,x,y,i%2 ? 55 : -20,'<use href="#leaf-shape"/><path d="M0 0Q-8 -15 -19 -30" fill="none" stroke="#dbdf8c" opacity=".7"/>','leaf');
    growthNode(leaves,x+9,140+Math.sin(i*1.6)*7,i%2 ? 190 : 135,'<use href="#leaf-shape"/><path d="M0 0Q-8 -15 -19 -30" fill="none" stroke="#d6dc8a" opacity=".6"/>','leaf');
  }
  const flowers = document.getElementById('flowers');
  [94,193,293,398,515].forEach((x,i)=>growthNode(flowers,x,i%2?149:46,0,
    `${Array.from({length:5},(_,p)=>`<ellipse cx="0" cy="-7" rx="5.5" ry="9" fill="url(#petal)" transform="rotate(${p*72})"/>`).join('')}<circle r="4" fill="#e5a734"/><circle cx="-1" cy="-1" r="1.5" fill="#fff3b6"/>`,'flower'));
  const clip = document.getElementById('growth-clip');
  // Keep the light attached to the painted flower, including cover cropping
  // and the forest's gentle breathing transform.
  const forest = overlay.querySelector('.forest');
  const magic = document.createElement('div');
  magic.className = 'flower-magic';
  magic.setAttribute('aria-hidden', 'true');
  magic.innerHTML = '<div class="flower-aura"></div><div class="flower-core"></div>';
  forest.appendChild(magic);
  const flowerSparks = [];
  for (let i = 0; i < 40; i++) {
    const carrier = document.createElement('span');
    carrier.className = 'flower-spark-carrier';
    const spark = document.createElement('i');
    spark.className = 'flower-spark' + (i % 4 === 0 ? ' star' : '');
    const spread = ((i * 73 + 19) % 241) - 120;
    const rise = 55 + (i * 47) % 145;
    spark.style.cssText = `--start-x:${spread * .14}px;--start-y:${12 + i % 17}px;--mid-x:${spread * .6}px;--mid-y:${-rise * .5}px;--end-x:${spread}px;--end-y:${-rise}px;--life:${2.6 + (i % 9) * .3}s;--phase:-${(i * .73) % 5}s;--size:${i % 4 === 0 ? 10 : 2 + i % 3}px`;
    carrier.appendChild(spark);
    magic.appendChild(carrier);
    flowerSparks.push(carrier);
  }
  function positionFlowerMagic() {
    const portrait = window.matchMedia('(max-aspect-ratio: 4/5)').matches;
    const source = portrait ? { width: 940, height: 1672, x: 450, y: 676 } : { width: 1672, height: 941, x: 820, y: 391 };
    const width = forest.clientWidth, height = forest.clientHeight;
    const scale = Math.max(width / source.width, height / source.height);
    const verticalPosition = portrait ? .5 : window.matchMedia('(max-height: 500px)').matches ? .4 : .43;
    magic.style.left = `${(width - source.width * scale) * .5 + source.x * scale}px`;
    magic.style.top = `${(height - source.height * scale) * verticalPosition + source.y * scale}px`;
    magic.style.transform = `scale(${scale})`;
  }
  const flowerResizeObserver = new ResizeObserver(positionFlowerMagic);
  flowerResizeObserver.observe(forest);
  positionFlowerMagic();
  function render(value) {
    clip.setAttribute('width', 540*value/100);
    for (const item of leafNodes) item.node.classList.toggle('open', value>=item.threshold);
    const strength = value / 100;
    magic.style.setProperty('--light', .08 + strength * .74);
    magic.style.setProperty('--bloom-scale', .55 + strength * .65);
    for (let i = 0; i < flowerSparks.length; i++) {
      flowerSparks[i].classList.toggle('lit', i < Math.floor(3 + strength * 37));
    }
  }
  function tick(now) {
    const delta = Math.min(64, now-(lastTime||now)); lastTime=now;
    shown = reducedMotion.matches ? target : shown+(target-shown)*(1-Math.exp(-delta/150));
    if (Math.abs(target-shown)<.03) shown=target;
    render(shown);
    frame = shown===target ? 0 : requestAnimationFrame(tick);
  }
  function update(value) {
    target=value;bar.setAttribute('aria-valuenow',Math.floor(value));
    if (!frame) { lastTime=0;frame=requestAnimationFrame(tick); }
  }
  const particles = document.getElementById('fireflies');
  for (let i=0;i<18;i++) {
    const spark=document.createElement('i');spark.className='spark';
    spark.style.cssText=`--x:${(i*47+11)%100}%;--y:${(i*31+17)%85}%;--duration:${5+i%7}s;--delay:-${i%9}s`;
    particles.appendChild(spark);
  }
  window.GlimbleHopLoading = {
    progress(value) {
      if (failed||completed||!Number.isFinite(value)) return;
      // Engine initialization, not elapsed time, determines progress.
      update(Math.max(target,Math.min(99,Math.max(0,value*100))));
    },
    complete() {
      if (failed||completed) return;
      completed=true;update(100);
      // Let the final leaves bloom before the overlay dissolves.
      window.setTimeout(()=>{
        overlay.classList.add('depart');
        window.setTimeout(()=>{
          cancelAnimationFrame(frame);flowerResizeObserver.disconnect();overlay.remove();
          document.getElementById('unity-canvas').focus();
        },reducedMotion.matches?0:800);
      },reducedMotion.matches?0:450);
    },
    fail(reason) {
      if (completed||failed) return;
      failed=true;
      console.error('GlimbleHop loading failed:',reason);
      const error=document.getElementById('error');
      error.textContent='Unable to load the game. Check your connection and try again.';
      error.hidden=false;document.getElementById('retry').hidden=false;
      bar.setAttribute('aria-label','Loading failed');
    }
  };
  document.getElementById('retry').addEventListener('click',()=>window.location.reload());
})();
