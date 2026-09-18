from pathlib import Path
import sys
sys.path.insert(0,'C:/Backforge/.tools/goblin-video')
import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw
HERE=Path(__file__).resolve().parent
reader=imageio_ffmpeg.read_frames('C:/Users/tehno/Downloads/grok-video-690af12e-755c-4416-bfff-77a95f16da44.mp4',pix_fmt='rgb24')
meta=next(reader)
frames=[Image.frombytes('RGB',meta['size'],x) for x in reader]
thumbs=[]
for im in frames:
    a=np.asarray(im.resize((96,96))).astype(float)/255
    mask=(a[:,:,1]-np.maximum(a[:,:,0],a[:,:,2])<.25)
    a*=mask[:,:,None]
    thumbs.append(a)
thumbs=np.array(thumbs)
scores=[]
for i in range(20,110):
    for length in range(16,33):
        j=i+length
        if j>135:continue
        score=np.mean(abs(thumbs[i]-thumbs[j]))
        derivative=np.mean(abs((thumbs[i+1]-thumbs[i])-(thumbs[j+1]-thumbs[j])))
        scores.append((float(score+.3*derivative),i,j))
print(sorted(scores)[:15])
for start,end in [(sorted(scores)[0][1],sorted(scores)[0][2]),(34,60)]:
    sheet=Image.new('RGB',(180*7,205*4),'#26332a');d=ImageDraw.Draw(sheet)
    for i,idx in enumerate(range(start,end+1)):
        if i>=28:break
        im=frames[idx].resize((178,178)); x,y=i%7*180,i//7*205
        sheet.paste(im,(x,y+22));d.text((x+6,y+4),str(idx),fill='white')
    sheet.save(HERE/f'cycle-{start}-{end}.jpg')
