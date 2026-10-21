import bpy,json
from pathlib import Path
root=Path('C:/Backforge/Diceforge/DiceForge/docs/Validation/LivingForest/Beta-v02')
meta=json.loads((root/'MovieFrames/complete.json').read_text())
files=sorted((root/'MovieFrames').glob('*.png'))
assert len(files)==meta['frames']==288,(len(files),meta)
scene=bpy.context.scene;editor=scene.sequence_editor_create()
strip=editor.strips.new_image('Unity forest beta v02',filepath=str(files[0]),channel=1,frame_start=1,fit_method='FIT')
for file in files[1:]:strip.elements.append(file.name)
strip.frame_final_duration=len(files)
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.render.fps=meta['fps'];scene.frame_start=1;scene.frame_end=len(files);scene.render.use_sequencer=True
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG'
scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264';scene.render.ffmpeg.constant_rate_factor='HIGH';scene.render.ffmpeg.audio_codec='NONE'
scene.render.filepath=str(root/'living-forest-unity.mp4')
bpy.ops.render.render(animation=True)
print('ENCODED_FOREST_BETA',len(files),scene.render.filepath)
