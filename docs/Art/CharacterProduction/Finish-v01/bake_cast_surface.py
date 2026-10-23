"""Shallow cloth/leather relief from existing cast atlases; colours untouched."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).parent
PROJECT = HERE.parents[3]
ART = PROJECT / 'Assets/_Project/07_Art/Characters'
sources = {
    'Tish': ART / 'Incoming/Tripo/TishDialogue_v01/Tish_BaseColor_2K.jpg',
    'Luma': ART / 'Incoming/Tripo/Luma_v02/Luma_ForestOutfit_BaseColor_2K.png',
    'Bum': ART / 'Incoming/Tripo/Bum_v01/Bum_BaseColor_2K.jpg',
}
report = {}
for name, path in sources.items():
    image = Image.open(path).convert('RGB').resize((1024, 1024), Image.Resampling.LANCZOS)
    rgb = np.asarray(image, dtype=np.float32) / 255
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    # Brown cloth/leather and pale linen. Golden/olive skin and bright eyes are
    # excluded, including Bum's living arm. No surface displacement is added.
    brown = (r > g * 1.38) & (g > b * 1.16) & (high < .73)
    linen = (low > .35) & ((high - low) < .20) & (high < .83)
    mask = np.asarray(Image.fromarray(((brown | linen) * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.1)), dtype=np.float32) / 255
    lum = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)
    blur = np.asarray(Image.fromarray((lum * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.3)), dtype=np.float32) / 255
    gy, gx = np.gradient(lum - blur)
    normal = np.stack([-gx * mask * 1.4, gy * mask * 1.4, np.ones_like(gx)], axis=2)
    normal /= np.linalg.norm(normal, axis=2)[:, :, None]
    output = ART / f'Finish/{name}_Cloth_Normal.png'
    Image.fromarray(np.clip((normal * .5 + .5) * 255, 0, 255).astype('uint8')).save(output)
    report[name] = dict(source=str(path.relative_to(PROJECT)), normal=str(output.relative_to(PROJECT)), colourChanged=False, resolution=[1024, 1024])
(HERE / 'cast-surface-report.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
