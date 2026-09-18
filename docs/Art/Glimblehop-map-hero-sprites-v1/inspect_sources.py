"""Read-only source inspection; decode user-supplied video locally."""
from pathlib import Path
import sys, json
sys.path.insert(0, 'C:/Backforge/.tools/goblin-video')
import imageio_ffmpeg
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SOURCES = [Path('C:/Users/tehno/Downloads/grok-video-690af12e-755c-4416-bfff-77a95f16da44.mp4'),
           Path('C:/Users/tehno/Downloads/1224182 (1).mp4')]
for index, path in enumerate(SOURCES):
    reader = imageio_ffmpeg.read_frames(str(path), pix_fmt='rgb24')
    metadata = next(reader)
    print(path.name, json.dumps(metadata), flush=True)
    frames = [Image.frombytes('RGB', metadata['size'], data) for data in reader]
    sheet = Image.new('RGB', (240*6, 280*3), '#26332a')
    draw = ImageDraw.Draw(sheet)
    for i in range(18):
        frame_index = round(i*(len(frames)-1)/17)
        frame = frames[frame_index].copy()
        frame.thumbnail((236,250))
        x,y = i%6*240, i//6*280
        sheet.paste(frame, (x+(240-frame.width)//2,y+22))
        draw.text((x+8,y+5), f'{frame_index} / {frame_index/metadata["fps"]:.2f}s', fill='white')
    sheet.save(HERE/f'video-{index+1}-overview.jpg')
    (HERE/f'video-{index+1}-metadata.json').write_text(json.dumps(dict(source=str(path),count=len(frames),**metadata),indent=2))
