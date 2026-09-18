"""Deterministic chroma-key extraction of the user's existing animation, no generated imagery."""
from pathlib import Path
import sys, json, hashlib
sys.path.insert(0,'C:/Backforge/.tools/goblin-video')
import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OUT=ROOT/'Assets/_Project/Resources/Map/HeroSprites'
OUT.mkdir(parents=True,exist_ok=True)
SOURCE=Path('C:/Users/tehno/Downloads/grok-video-690af12e-755c-4416-bfff-77a95f16da44.mp4')
reader=imageio_ffmpeg.read_frames(str(SOURCE),pix_fmt='rgb24')
metadata=next(reader)
source=[Image.frombytes('RGB',metadata['size'],x) for x in reader]

def key(im):
    rgb=np.asarray(im).astype(np.float32)
    excess=rgb[:,:,1]-np.maximum(rgb[:,:,0],rgb[:,:,2])
    alpha=np.clip((85-excess)/50,0,1)
    alpha=alpha*alpha*(3-2*alpha)
    bg=np.median(np.concatenate([rgb[:8].reshape(-1,3),rgb[-8:].reshape(-1,3)]),axis=0)
    edge=(alpha>0)&(alpha<1)
    rgb[edge]=np.clip((rgb[edge]-(1-alpha[edge,None])*bg)/alpha[edge,None],0,255)
    rgb[edge,1]=np.minimum(rgb[edge,1],np.maximum(rgb[edge,0],rgb[edge,2])+8)
    rgb[alpha==0]=0
    return Image.fromarray(np.dstack([rgb,alpha*255]).astype(np.uint8),'RGBA')

indices=list(range(49,69))+list(range(10))
cutouts=[key(source[i]) for i in indices]
# One scale for the full take; horizontal tracking only, so vertical stride motion is retained.
bounds=[im.getbbox() for im in cutouts]
top=min(b[1] for b in bounds);bottom=max(b[3] for b in bounds)
scale=220/(bottom-top)
frames=[]
for im,b in zip(cutouts,bounds):
    # Keep the original y range and put the global floor at y=240.
    cx=(b[0]+b[2])/2
    crop=im.crop((round(cx-256/(2*scale)),top,round(cx+256/(2*scale)),bottom))
    crop=crop.resize((256,220),Image.Resampling.LANCZOS)
    frame=Image.new('RGBA',(256,256))
    frame.alpha_composite(crop,(0,20));frames.append(frame)
atlas=Image.new('RGBA',(2048,1024))
for i,frame in enumerate(frames): atlas.alpha_composite(frame,((i%8)*256,(i//8)*256))
atlas.save(OUT/'goblin-atlas.png')

def backdrop(frame):
    bg=Image.new('RGBA',(320,300),'#384b35')
    d=ImageDraw.Draw(bg);d.ellipse((112,247,208,260),fill='#263621')
    bg.alpha_composite(frame,(32,16))
    return bg.convert('RGB')
run=[backdrop(x) for x in frames[:20]]
run[0].save(HERE/'run-preview.webp',save_all=True,append_images=run[1:],duration=42,loop=0,lossless=True)
idle=[frames[20]]+[frames[20+i] for i in list(range(1,10))+list(range(8,0,-1))]
idle=[backdrop(x) for x in idle]
idle[0].save(HERE/'idle-preview.webp',save_all=True,append_images=idle[1:],duration=[1600]+[65]*(len(idle)-1),loop=0,lossless=True)
contact=Image.new('RGB',(256*5,280*4),'#384b35');draw=ImageDraw.Draw(contact)
for i,frame in enumerate(frames[:20]):
    x,y=i%5*256,i//5*280
    contact.paste(frame,(x,y),frame);draw.text((x+10,y+258),f'Run {i:02d} / source {indices[i]}',fill='white')
contact.save(HERE/'run-contact.png')
frames[20].save(HERE/'idle-transparent.png')
(HERE/'manifest.json').write_text(json.dumps({
    'source':str(SOURCE),'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    'fps':24,'runFrames':list(range(49,69)),'idleFrames':list(range(10)),
    'cellSize':256,'columns':8,'rows':4,'atlas':'Assets/_Project/Resources/Map/HeroSprites/goblin-atlas.png',
    'scale':scale,'sourceVerticalBounds':[top,bottom],
    'notes':'Original light outline retained; chroma spill removed only from partial-alpha edges. Run is a best-match section of generated footage, not a perfect authored loop.'
},indent=2))
print('Exported',len(frames),'frames',OUT/'goblin-atlas.png')
