"""Small tileable, unlit earth/gravel detail maps; preserves the authored path colour map."""
from pathlib import Path
import numpy as np
from PIL import Image

out = Path(__file__).resolve().parents[4] / 'Assets/_Project/07_Art/BetaPolish'
out.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(23102026)
size = 512
y, x = np.mgrid[:size, :size].astype(np.float32) / size
height = np.zeros_like(x)
for frequency, amplitude in [(3,.35), (9,.18), (24,.10), (72,.04)]:
    for _ in range(12):
        u,v = rng.integers(-frequency,frequency+1,2)
        height += np.sin((x*u+y*v)*2*np.pi+rng.uniform(0,2*np.pi))*amplitude/np.sqrt(12)
for _ in range(120):
    cx,cy = rng.random(2); rx,ry = rng.uniform(.004,.014,2)
    dx=(x-cx+.5)%1-.5;dy=(y-cy+.5)%1-.5
    height += np.maximum(0,1-(dx/rx)**2-(dy/ry)**2)**2 * rng.uniform(.1,.4)
colour=np.clip(.5+height*.10, .32,.68)
rgb=np.stack([colour*1.02,colour,colour*.97],axis=-1)
Image.fromarray(np.uint8(rgb*255)).save(out/'GroundDetail.png')
dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.85
dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.85
normal=np.stack([-dx,dy,np.ones_like(dx)],axis=-1)
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).save(out/'GroundDetailNormal.png')
print('Ground detail maps:',out)
