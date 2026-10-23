"""Run in Blender's bundled aud decoder; does not change the existing click."""
import aud
import hashlib
import json
from pathlib import Path
import numpy as np
import wave

root = Path(__file__).parent
project = root.parents[2]
path = project / 'Assets/_Project/08_Audio/SFX/ui_click_primary.ogg'
sound = aud.Sound(str(path)).rechannel(1).resample(44100)
samples = np.asarray(sound.data()).reshape(-1)
assert len(samples) > 0 and abs(samples).max() > .001
report = dict(file=str(path.relative_to(project)), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
              seconds=len(samples) / 44100, peakDbfs=float(20 * np.log10(abs(samples).max())),
              rmsDbfs=float(20 * np.log10(np.sqrt(np.mean(samples ** 2)))),
              samplesAtOrAboveFullScale=int((abs(samples) >= 1).sum()), modified=False)
(root / 'existing-click-audit.json').write_text(json.dumps(report, indent=2) + '\n')
with wave.open(str(root / 'existing-click-reference.wav'), 'wb') as wav:
    wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(44100)
    wav.writeframes(np.round(np.clip(samples, -1, 1) * 32767).astype('<i2').tobytes())
print(json.dumps(report))
