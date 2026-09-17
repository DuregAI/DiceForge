"""Reproducible, user-authorized checkerboard removal and static composition QA."""
from pathlib import Path
from collections import deque
import json, uuid, re
import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'Assets/_Project/07_Art/UI/GlimblehopMap'
MENU = OUT.parent / 'GlimblehopMenu'

def flood(mask):
    h,w=mask.shape
    seen=np.zeros_like(mask)
    q=deque()
    for x in range(w):
        for y in (0,h-1):
            if mask[y,x] and not seen[y,x]: seen[y,x]=True; q.append((y,x))
    for y in range(h):
        for x in (0,w-1):
            if mask[y,x] and not seen[y,x]: seen[y,x]=True; q.append((y,x))
    while q:
        y,x=q.popleft()
        for yy,xx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
            if 0<=yy<h and 0<=xx<w and mask[yy,xx] and not seen[yy,xx]:
                seen[yy,xx]=True; q.append((yy,xx))
    return seen

names=['node-stone','badge-completed','badge-locked','title-plaque','button-back']
manifest=[]
for name in names:
    rgb=Image.open(HERE/'Drafts'/f'{name}-OPAQUE.png').convert('RGB')
    a=np.asarray(rgb).astype(float)
    chroma=a.max(2)-a.min(2)
    # Background is neutral gray; retain warm object pigments, close tiny contour gaps.
    seed=Image.fromarray(np.uint8(chroma>19)*255).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
    solid=~flood(np.asarray(seed)==0)
    # Discard disconnected crumbs introduced by compression/background variation.
    seen=np.zeros_like(solid); components=[]
    for y,x in zip(*np.where(solid)):
        if seen[y,x]: continue
        q=deque([(y,x)]); seen[y,x]=True; comp=[]
        while q:
            yy,xx=q.popleft(); comp.append((yy,xx))
            for ny,nx in ((yy-1,xx),(yy+1,xx),(yy,xx-1),(yy,xx+1)):
                if 0<=ny<solid.shape[0] and 0<=nx<solid.shape[1] and solid[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True; q.append((ny,nx))
        components.append(comp)
    clean=np.zeros_like(solid)
    for comp in components:
        if len(comp)>150:
            for y,x in comp: clean[y,x]=True
    alpha=Image.fromarray(np.uint8(clean)*255).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.45))
    rgba=rgb.convert('RGBA'); rgba.putalpha(alpha)
    box=alpha.getbbox(); box=(max(0,box[0]-4),max(0,box[1]-4),min(rgb.width,box[2]+4),min(rgb.height,box[3]+4))
    rgba=rgba.crop(box); rgba.save(OUT/f'{name}.png')
    manifest.append(dict(file=f'{name}.png',width=rgba.width,height=rgba.height,sourceCrop=box,alpha='segmented neutral background; filled opaque interior'))

# Rebuild glow analytically, avoiding any checkerboard contamination in translucent pixels.
w,h=512,256
yy,xx=np.mgrid[:h,:w]
r=np.sqrt(((xx-w/2)/218)**2+((yy-h/2)/77)**2)
d=np.abs(r-1)
alpha=np.clip(np.exp(-(d/.025)**2)*.90+np.exp(-(d/.12)**2)*.22,0,1)
rgba=np.zeros((h,w,4),dtype=np.uint8); rgba[:,:,:3]=[255,205,92]; rgba[:,:,3]=(alpha*255).astype('uint8')
Image.fromarray(rgba).save(OUT/'node-glow.png')
manifest.append(dict(file='node-glow.png',width=w,height=h,alpha='analytical soft elliptical ring; transparent center'))

template=(MENU/'button-primary.png.meta').read_text()
for p in OUT.glob('*.png'):
    meta=p.with_suffix('.png.meta')
    if not meta.exists(): meta.write_text(re.sub(r'(?m)^guid: .*$', 'guid: '+uuid.uuid4().hex,template))
folder=OUT.with_suffix('.meta')
if not folder.exists(): folder.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

fontpath='C:/Windows/Fonts/georgiab.ttf'
def font(n): return ImageFont.truetype(fontpath,n)
def place(canvas,path,xy,size):
    im=Image.open(path).convert('RGBA'); im.thumbnail(size,Image.Resampling.LANCZOS)
    canvas.alpha_composite(im,(int(xy[0]-im.width/2),int(xy[1]-im.height/2)))

contact=Image.new('RGBA',(1200,900),'#ede6d6'); draw=ImageDraw.Draw(contact)
for i,name in enumerate(names+['node-glow']):
    x=(i%3)*400; y=(i//3)*450
    draw.rectangle((x,y+220,x+399,y+420),fill='#253b35')
    for yc in (y+115,y+320): place(contact,OUT/f'{name}.png',(x+200,yc),(350,160))
    draw.text((x+20,y+425),name,font=font(18),fill='#342a20')
contact.convert('RGB').save(HERE/'contact-sheet.png')

preview=Image.open(OUT/'background-landscape.png').convert('RGBA')
W,H=preview.size
nodes=[(.145,.65),(.32,.47),(.465,.565),(.66,.465),(.565,.265),(.785,.27)]
layout=[]
for i,(x,y) in enumerate(nodes,1):
    cx,cy=x*W,y*H
    place(preview,OUT/'node-stone.png',(cx,cy),(165,100))
    if i==3: place(preview,OUT/'node-glow.png',(cx,cy-16),(155,68))
    if i!=3: place(preview,OUT/('badge-completed.png' if i<3 else 'badge-locked.png'),(cx+60,cy-28),(45,45))
    ImageDraw.Draw(preview).text((cx,cy+15),str(i),anchor='mm',font=font(28),fill='#382418')
    layout.append(dict(id=f'C1_{i:02}',centerTopLeftNormalized=[x,y],sizeAtReference=[165,100],numberOffset=[0,15],heroFeetOffset=[0,-12]))
place(preview,OUT/'title-plaque.png',(W*.5,78),(600,140))
draw=ImageDraw.Draw(preview)
draw.text((W*.5,55),'CHAPTER 1',anchor='mm',font=font(22),fill='#ffebc3')
draw.text((W*.5,92),'WOODLAND TRAIL',anchor='mm',font=font(24),fill='#ffebc3')
place(preview,OUT/'button-back.png',(72,70),(92,92))
place(preview,MENU/'counter-panel.png',(180,H-95),(310,110))
place(preview,MENU/'button-primary.png',(W-260,H-95),(440,130))
draw=ImageDraw.Draw(preview)
draw.text((180,H-112),'PROGRESS',anchor='mm',font=font(23),fill='#fff1cf')
draw.text((180,H-78),'2 / 6',anchor='mm',font=font(30),fill='#fff1cf')
draw.text((W-280,H-95),'PLAY LEVEL 3',anchor='mm',font=font(28),fill='#fff1cf')
place(preview,MENU/'play-arrow.png',(W-133,H-95),(22,28))
preview.convert('RGB').save(HERE/'assembled-preview.png')
(HERE/'manifest.json').write_text(json.dumps(manifest,indent=2))
(HERE/'layout-landscape.json').write_text(json.dumps(dict(referenceSize=[W,H],origin='top-left',nodes=layout,heroIncluded=False),indent=2))
for item in manifest:
    im=Image.open(OUT/item['file']); ar=np.asarray(im.getchannel('A'))
    assert ar.min()==0 and ar.max()>200
    print(item['file'],im.size,'transparent',round(float((ar==0).mean()),3))
print('Contact and assembly previews exported.')
