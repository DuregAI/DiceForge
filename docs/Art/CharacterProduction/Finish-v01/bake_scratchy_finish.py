"""Bake a restrained fabric palette into Scratchy's existing UV atlas.

Run inspect_scratchy.py with Blender first, then run this with Pillow + NumPy.
Only textures are authored. Geometry, skinning, facial shapes and clips stay intact.
"""
import gzip
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).parent
PROJECT = HERE.parents[3]
ART = PROJECT / 'Assets/_Project/07_Art/Characters/Finish'
SOURCE = PROJECT / 'Assets/_Project/07_Art/Characters/Incoming/Tripo/Scratchy_v01/Scratchy_BaseColor_2K.jpg'
ART.mkdir(parents=True, exist_ok=True)
with gzip.open(HERE / 'scratchy-uv-world.json.gz', 'rt', encoding='utf-8') as stream:
    data = json.load(stream)
parts = json.loads((HERE / 'scratchy-components.json').read_text())
source = np.asarray(Image.open(SOURCE).convert('RGB'), dtype=np.float32) / 255
height, width = source.shape[:2]
points = np.asarray(data['points'], dtype=np.float32)
positions = np.zeros((height, width, 3), dtype=np.float32)
components = np.full((height, width), -1, dtype=np.int16)
eye = np.zeros((height, width), dtype=bool)

# Rasterize authored UVs so only the intended physical part changes colour.
for polygon in data['polygons']:
    for i in range(1, len(polygon['indices']) - 1):
        ids = [0, i, i + 1]
        uv = np.asarray([polygon['uvs'][j] for j in ids]) * [width - 1, height - 1]
        uv[:, 1] = height - 1 - uv[:, 1]
        minimum = np.maximum(np.floor(uv.min(axis=0)).astype(int), 0)
        maximum = np.minimum(np.ceil(uv.max(axis=0)).astype(int), [width - 1, height - 1])
        x, y = np.meshgrid(np.arange(minimum[0], maximum[0] + 1), np.arange(minimum[1], maximum[1] + 1))
        denominator = (uv[1, 1] - uv[2, 1]) * (uv[0, 0] - uv[2, 0]) + (uv[2, 0] - uv[1, 0]) * (uv[0, 1] - uv[2, 1])
        if abs(denominator) < 1e-8:
            continue
        a = ((uv[1, 1] - uv[2, 1]) * (x - uv[2, 0]) + (uv[2, 0] - uv[1, 0]) * (y - uv[2, 1])) / denominator
        b = ((uv[2, 1] - uv[0, 1]) * (x - uv[2, 0]) + (uv[0, 0] - uv[2, 0]) * (y - uv[2, 1])) / denominator
        c = 1 - a - b
        inside = (a >= -.002) & (b >= -.002) & (c >= -.002)
        ys, xs = y[inside], x[inside]
        p = points[[polygon['indices'][j] for j in ids]]
        positions[ys, xs] = a[inside, None] * p[0] + b[inside, None] * p[1] + c[inside, None] * p[2]
        components[ys, xs] = polygon['component']
        eye[ys, xs] = polygon['material'] == 1

hair_parts = [p['id'] for p in parts if p['center'][2] > .70 and p['count'] < 120]
hair_parts += [4]
hair = np.isin(components, hair_parts)
bell = components == 2
face = (components == 3) & (positions[:, :, 2] > .51) & (positions[:, :, 1] < -.20)
cloth = ((components == 0) | (components == 47) | ((components == 1) & (positions[:, :, 2] > .16)))
cloth |= face
output = source.copy()
r, g, b = source[:, :, 0], source[:, :, 1], source[:, :, 2]
maximum = source.max(axis=2)
lum = source @ np.array([.2126, .7152, .0722], dtype=np.float32)

def soften(mask, radius=1):
    return np.asarray(Image.fromarray((mask * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255

def expand(mask, pixels=3):
    return np.asarray(Image.fromarray((mask * 255).astype('uint8')).filter(ImageFilter.MaxFilter(pixels * 2 + 1))) > 0

def mix(target, mask, amount=1):
    global output
    weight = np.clip(mask * amount, 0, 1)[:, :, None]
    output = output * (1 - weight) + target * weight

# Keep the original shading and strand variation, replacing the dominant orange
# with flax. Deep rust, charcoal strands and leaves remain in the palette.
warm_yarn = expand(hair) & (r > g * 1.40) & (g > b * 1.35) & (g / np.maximum(r, .001) > .40) & (maximum > .27)
flax = np.array([.86, .75, .57]) * np.clip(.57 + lum[:, :, None] * .70, .55, 1.08)
mix(flax, soften(warm_yarn), .86)
rust = expand(hair) & (r > g * 2.05) & (r > b * 1.8) & (maximum > .25)
rust_colour = np.array([.66, .25, .16]) * np.clip(.60 + maximum[:, :, None] * .50, .6, 1)
mix(rust_colour, soften(rust), .62)

# Lift warm jute to linen without whitening its holes, seams or brown shoes.
beige = expand(cloth, 2) & (r > g * 1.06) & (g > b * 1.23) & (g / np.maximum(r, .001) > .54) & (maximum > .38)
linen = source * np.array([.99, 1.045, 1.16]) + np.array([.018, .018, .012])
mix(linen, soften(beige), .74)
cheeks = face & (np.abs(positions[:, :, 0]) > .09) & (positions[:, :, 2] > .53) & (positions[:, :, 2] < .61) & (r > g * 1.8) & (maximum > .25)
rose = np.array([.81, .40, .34]) * np.clip(.63 + maximum[:, :, None] * .42, .6, 1.02)
mix(rose, soften(cheeks), .70)

# The bell is still one existing mesh. Metal is local to its atlas island.
gold = np.array([.75, .51, .19]) * np.clip(.60 + lum[:, :, None] * .42, .6, 1.03)
bell_bright = bell & (maximum > .16)
mix(gold, soften(bell_bright), .74)

# Region masks include the nearby authored UV padding for filtering/mips;
# the remaining atlas retains its original colour rather than black halos.
colour = Image.fromarray(np.clip(output * 255, 0, 255).astype('uint8'))
colour.save(ART / 'Scratchy_Flax_BaseColor.png')

# Metallic in R and smoothness in A, as expected by URP Lit. Textile stays matte.
packed = np.zeros((height, width, 4), dtype=np.uint8)
packed[:, :, 3] = 128
packed[hair, 3] = 148
packed[(components == 1) & (positions[:, :, 2] < .16), 3] = 235
metal_weight = soften(bell_bright, .8)
packed[:, :, 0] = (metal_weight * 122).astype('uint8')
packed[:, :, 3] = np.maximum(packed[:, :, 3], (metal_weight * 255).astype('uint8'))
Image.fromarray(packed).save(ART / 'Scratchy_Surface.png')

# Shallow relief from existing painted weave avoids adding new UV seams or
# changing the silhouette. Eyes and the bell stay smooth.
blurred = np.asarray(Image.fromarray((lum * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.1)), dtype=np.float32) / 255
relief = lum - blurred
gy, gx = np.gradient(relief)
normal = np.stack([-gx * .85, gy * .85, np.ones_like(gx)], axis=2)
normal /= np.linalg.norm(normal, axis=2)[:, :, None]
normal[eye | bell] = [0, 0, 1]
Image.fromarray(np.clip((normal * .5 + .5) * 255, 0, 255).astype('uint8')).save(ART / 'Scratchy_Weave_Normal.png')

# Review masks are documentation only, never shipped.
review = np.zeros_like(source)
review[hair] = [.90, .64, .30]
review[face] = [.38, .65, .86]
review[bell] = [.95, .92, .15]
review[eye] = [.22, .8, .4]
Image.fromarray((review * 255).astype('uint8')).resize((768, 768)).save(HERE / 'uv-regions.png')
report = dict(revision='Finish-v01', source=str(SOURCE.relative_to(PROJECT)),
              vertices=len(data['points']), polygons=len(data['polygons']),
              hairTexels=int(hair.sum()), flaxTexels=int(warm_yarn.sum()), bellTexels=int(bell.sum()),
              atlasSize=[width, height], geometryChanged=False,
              preserved=['rig', 'blendshapes', 'animations', 'one bell', 'portrait art'],
              outputs=['Scratchy_Flax_BaseColor.png', 'Scratchy_Surface.png', 'Scratchy_Weave_Normal.png'])
(HERE / 'texture-report.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
