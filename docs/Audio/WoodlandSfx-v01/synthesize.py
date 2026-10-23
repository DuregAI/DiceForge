"""Original procedural woodland SFX. No recordings, voices, or external services.

Requires NumPy. Run from any directory. Output remains in this review directory
until --install copies the authored WAVs and deterministic Unity metadata.
"""
import argparse
import hashlib
import json
from pathlib import Path
import uuid
import wave
import numpy as np

ROOT = Path(__file__).parent
PROJECT = ROOT.parents[2]
RATE = 44100
RNG = np.random.default_rng(461091)
OUTPUT = ROOT / 'Clips'
OUTPUT.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
args = parser.parse_args()

def clock(seconds):
    return np.arange(round(seconds * RATE)) / RATE

def noise(seconds, low=200, high=5000):
    t = clock(seconds)
    value = RNG.normal(0, 1, len(t))
    frequency = np.fft.rfftfreq(len(t), 1 / RATE)
    response = (1 - np.exp(-(frequency / low) ** 4)) * np.exp(-(frequency / high) ** 4)
    value = np.fft.irfft(np.fft.rfft(value) * response, len(t))
    return value / max(np.std(value), 1e-6)

def envelope(t, attack=.004, decay=.08):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)

def wood(seconds, frequency, decay=.07):
    t = clock(seconds)
    value = np.zeros(len(t))
    for ratio, gain in [(1, 1), (2.71, .24), (4.23, .09), (5.4, .035)]:
        value += gain * np.sin(2 * np.pi * frequency * ratio * t) * envelope(t, .0015, decay / ratio ** .35)
    return value

def add(destination, value, seconds, gain=1):
    first = round(seconds * RATE)
    count = min(len(value), len(destination) - first)
    if count > 0:
        destination[first:first + count] += value[:count] * gain

def bell(seconds, frequency):
    t = clock(seconds)
    value = np.zeros(len(t))
    for ratio, gain, decay in [(1, 1, .31), (2.01, .21, .17), (2.76, .10, .12), (4.04, .04, .08)]:
        value += gain * np.sin(2 * np.pi * frequency * ratio * t) * envelope(t, .003, decay)
    return value

def brush(seconds, reverse=False):
    t = clock(seconds)
    shape = np.sin(np.pi * np.clip(t / seconds, 0, 1)) ** 1.8
    if reverse:
        shape *= np.linspace(1.3, .4, len(t))
    else:
        shape *= np.linspace(.4, 1.3, len(t))
    return noise(seconds, 240, 4200) * shape * .15

def window(opening, variant):
    duration = .27 if opening else .22
    value = brush(duration, not opening)
    add(value, wood(.16, (690 if opening else 545) * (1 + variant * .04), .043), .025 if opening else .018, .55)
    if opening:
        add(value, bell(.17, 1380 * (1 + variant * .03)), .075, .075)
    return value

def transition(opening):
    duration = .48 if opening else .42
    t = clock(duration)
    value = brush(duration, not opening) * .65
    value += noise(duration, 140, 1700) * np.sin(np.pi * t / duration) ** 2 * .10
    add(value, wood(.2, 780 if opening else 450, .06), .12 if opening else .055, .09)
    return value

def jump(variant):
    seconds = .19 + variant * .008
    t = clock(seconds)
    frequency = np.linspace(210 + variant * 13, 440 + variant * 8, len(t))
    tone = np.sin(2 * np.pi * np.cumsum(frequency) / RATE) * envelope(t, .014, .045)
    return tone * .35 + brush(seconds) * .28 + wood(seconds, 165 + variant * 8, .025) * .27

def foot(variant, landing=False):
    seconds = .21 if landing else .14
    t = clock(seconds)
    decay = .043 if landing else .027
    frequency = (92 if landing else 132) + variant * 7
    phase = 2 * np.pi * (frequency * t - frequency * .24 * t * t / seconds)
    body = np.sin(phase) * envelope(t, .002, decay)
    crunch = noise(seconds, 850, 4300) * envelope(t, .001, .023 if landing else .019)
    leather = noise(seconds, 150, 1050) * envelope(t, .002, .035)
    return body * .68 + crunch * .14 + leather * .20 + wood(seconds, 290 + variant * 17, .027) * .16

def completion():
    value = np.zeros(round(1.18 * RATE))
    for i, frequency in enumerate([587.33, 739.99, 880.0, 1174.66]):
        add(value, wood(.45, frequency, .13), i * .105, .52 if i < 3 else .40)
    add(value, bell(.72, 1174.66), .38, .20)
    add(value, brush(.32), .10, .25)
    return value

def retry():
    value = np.zeros(round(.67 * RATE))
    for i, frequency in enumerate([392.0, 329.63, 293.66]):
        add(value, wood(.35, frequency, .10), i * .11, .45)
    return value

def exit_cue():
    value = np.zeros(round(.34 * RATE))
    for i, frequency in enumerate([620, 830, 1100]):
        add(value, wood(.18, frequency, .05), i * .053, .5)
    return value

def rattle(variant):
    value = np.zeros(round(.35 * RATE))
    for i, when in enumerate([0, .028, .065, .11, .17, .23]):
        add(value, wood(.11, 490 + variant * 31 + i * 67, .018), when, .55 - i * .055)
    return value

def write(path, value, peak):
    # Short edge ramps and bounded peak avoid click transients and clipping.
    value = np.tanh(value * 1.1)
    ramp = min(round(.003 * RATE), len(value) // 2)
    value[:ramp] *= np.linspace(0, 1, ramp)
    value[-ramp:] *= np.linspace(1, 0, ramp)
    value *= peak / max(abs(value).max(), 1e-8)
    pcm = np.round(np.clip(value, -1, 1) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    return pcm.astype(float) / 32768

clips = []
for v in range(2):
    clips += [(f'window_open_{v+1:02}', window(True, v), .29), (f'window_close_{v+1:02}', window(False, v), .25)]
clips += [('transition_close_01', transition(False), .24), ('transition_open_01', transition(True), .24)]
for v in range(3):
    clips += [(f'jump_{v+1:02}', jump(v), .28), (f'land_{v+1:02}', foot(v, True), .43)]
for v in range(4):
    clips += [(f'footstep_{v+1:02}', foot(v), .28)]
clips += [('level_complete_01', completion(), .48), ('retry_01', retry(), .34), ('token_exit_01', exit_cue(), .32)]
for v in range(2):
    clips += [(f'token_hit_{v+1:02}', foot(v + 2, True) + wood(.21, 430 + v * 45, .037) * .32, .36),
              (f'dice_roll_{v+1:02}', rattle(v), .34)]

manifest = {'sampleRate': RATE, 'channels': 1, 'encoding': 'PCM16', 'source': 'Original procedural synthesis; no voices or external recordings.', 'clips': []}
audition = []
cursor = .4
audition.append(np.zeros(round(cursor * RATE)))
for name, raw, peak in clips:
    path = OUTPUT / (name + '.wav')
    pcm = write(path, raw, peak)
    measured = float(abs(pcm).max())
    manifest['clips'].append(dict(name=name, auditionStartSeconds=round(cursor, 3), durationSeconds=round(len(pcm) / RATE, 4), peakDbfs=round(20 * np.log10(max(measured, 1e-9)), 2), rmsDbfs=round(20 * np.log10(np.sqrt(np.mean(pcm ** 2)) + 1e-12), 2), clippedSamples=int((abs(pcm) >= .9999).sum()), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    audition += [pcm, np.zeros(round(.38 * RATE))]
    cursor += len(pcm) / RATE + .38

with wave.open(str(ROOT / 'woodland-sfx-audition.wav'), 'wb') as wav:
    wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(RATE)
    wav.writeframes(np.round(np.concatenate(audition) * 32767).astype('<i2').tobytes())
manifest['auditionDurationSeconds'] = round(cursor, 3)
manifest['clippedSamples'] = sum(c['clippedSamples'] for c in manifest['clips'])
(ROOT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
(ROOT / 'audition-timings.txt').write_text('\n'.join(f"{c['auditionStartSeconds']:6.2f}s  {c['name']}  ({c['durationSeconds']:.2f}s, {c['peakDbfs']:.1f} dBFS)" for c in manifest['clips']) + '\n')

if args.install:
    import shutil
    destination = PROJECT / 'Assets/_Project/Resources/Audio/Woodland'
    destination.mkdir(parents=True, exist_ok=True)
    for path in OUTPUT.glob('*.wav'):
        target = destination / path.name
        shutil.copyfile(path, target)
        meta = Path(str(target) + '.meta')
        if not meta.exists():
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'diceforge/original-sfx/' + path.name).hex
            meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: Original procedural woodland SFX v01
  assetBundleName:
  assetBundleVariant:
''')
print(json.dumps({'clips': len(clips), 'seconds': manifest['auditionDurationSeconds'], 'clippedSamples': manifest['clippedSamples'], 'bytes': sum(p.stat().st_size for p in OUTPUT.glob('*.wav'))}))
