from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[3]
source = Path('C:/Users/tehno/.codex/generated_images/01a0af5a-737c-70f1-bdcd-42ac00371ac5/exec-9c2e4809-d363-47b8-b98d-d33eb9795a86.png')
rgb = np.array(Image.open(source).convert('RGB')).astype(np.float32)
# The generated backdrop is neutral gray; retain the saturated olive plant.
chroma = rgb.max(axis=2) - rgb.min(axis=2)
alpha = np.clip((chroma - 18) / 24, 0, 1)
rgba = np.dstack([rgb, alpha * 255]).astype(np.uint8)
image = Image.fromarray(rgba)
bounds = image.getchannel('A').getbbox()
image = image.crop(bounds)
image.thumbnail((512, 512), Image.Resampling.LANCZOS)
canvas = Image.new('RGBA', (image.width + 16, image.height + 16))
canvas.paste(image, (8, 8))
out = root / 'Assets/_Project/Resources/Map/Foliage/woodland-shrub.png'
canvas.save(out)
print(out, canvas.size)
