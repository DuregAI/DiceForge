"""Deterministic tileable lakebed albedo and caustic masks. No baked lighting."""
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[4] / 'Assets/_Project/07_Art/LakeWater'
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(20261022)

size = 1024
y, x = np.mgrid[:size, :size].astype(np.float32) / size
noise = np.zeros_like(x)
for frequency, amplitude, terms in [(2, .35, 8), (7, .17, 12), (28, .08, 12), (130, .025, 12)]:
    for _ in range(terms):
        a, b = rng.integers(-frequency, frequency + 1, 2)
        noise += np.sin((x*a + y*b)*np.pi*2 + rng.uniform(0, np.pi*2)) * amplitude / np.sqrt(terms)
base = np.array([.57, .53, .40], np.float32)
rgb = base[None, None, :] * (1 + noise[..., None]*.24)
rgb += rng.normal(0, .012, (size, size, 1))
# Small, irregular rounded pebbles embedded in the albedo, with no directional shadow.
for _ in range(450):
    cx, cy = rng.random(2)
    rx, ry = rng.uniform(.002, .009, 2)
    dx = (x - cx + .5) % 1 - .5; dy = (y - cy + .5) % 1 - .5
    d = np.sqrt((dx/rx)**2 + (dy/ry)**2)
    mask = np.clip((1 - d)*8, 0, 1)[..., None]
    pebble = np.array(rng.choice([[.47,.46,.38], [.65,.61,.49], [.48,.51,.45], [.59,.55,.44]]))
    rgb = rgb*(1-mask*.8) + pebble*mask*.8
Image.fromarray(np.uint8(np.clip(rgb, 0, 1)*255)).save(OUT/'LakeBedColor.png')

size = 256
y, x = np.mgrid[:size, :size].astype(np.float32) / size
points = (np.stack(np.meshgrid(np.arange(7), np.arange(7)), -1).reshape(-1, 2) + rng.uniform(.15, .85, (49, 2))) / 7
first = np.full_like(x, 10); second = first.copy()
for px, py in points:
    dx = (x-px+.5)%1-.5; dy = (y-py+.5)%1-.5
    distance = np.sqrt(dx*dx + dy*dy)
    second = np.minimum(second, np.maximum(first, distance))
    first = np.minimum(first, distance)
mask = np.exp(-((second-first)/.007)**2)
Image.fromarray(np.uint8(mask*255)).save(OUT/'LakeCaustics.png')
print('Created lakebed albedo 1024 and seamless caustics 256:', OUT)
