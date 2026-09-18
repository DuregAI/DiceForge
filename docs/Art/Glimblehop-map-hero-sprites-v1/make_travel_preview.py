"""Compact proof from actual Unity Game View captures (sampled preview, not full-rate footage)."""
from pathlib import Path
from PIL import Image

here=Path(__file__).resolve().parent
frames=[Image.open(path).convert('RGB').resize((960,540),Image.Resampling.LANCZOS)
        for path in sorted((here/'travel-capture').glob('frame-*.png'))]
if not frames: raise RuntimeError('Run VerifyWoodlandTravel.Check in Unity first')
frames[0].save(here/'bridge-travel-preview.webp',save_all=True,append_images=frames[1:],
               duration=[200]*(len(frames)-1)+[900],loop=0,quality=88,method=5)
print(here/'bridge-travel-preview.webp')
