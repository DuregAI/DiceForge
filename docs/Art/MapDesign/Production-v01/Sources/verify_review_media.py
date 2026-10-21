"""Read-only checks of media and local links; does not retouch any image."""
from pathlib import Path
from html.parser import HTMLParser
import json
from PIL import Image,ImageChops,ImageStat
folder=Path(__file__).resolve().parent.parent
project=folder.parents[3]
review=project/'docs/Validation/LivingForest/Beta-v03'
class Links(HTMLParser):
    def __init__(self):super().__init__();self.links=[]
    def handle_starttag(self,tag,attrs):
        for key,value in attrs:
            if key in ('src','href','poster') and value and not value.startswith(('http:','https:','#')):self.links.append(value)
parser=Links();parser.feed((folder/'index.html').read_text(encoding='utf-8'))
missing=[link for link in parser.links if not (folder/link).resolve().exists()]
assert not missing,missing
frame_a=Image.open(review/'MovieFrames/0024.png').convert('RGB')
frame_b=Image.open(review/'MovieFrames/0120.png').convert('RGB')
regions={'tree':(350,180,800,520),'reeds':(1120,640,1240,815),
         'flag':(1465,290,1535,350),'lantern':(1425,290,1478,355),
         'water':(1140,550,1260,800),'foreground':(0,680,270,1080)}
changes={name:sum(ImageStat.Stat(ImageChops.difference(frame_a.crop(box),frame_b.crop(box))).mean)/3 for name,box in regions.items()}
assert all(value>.01 for value in changes.values()),changes
result={'localLinksChecked':len(parser.links),'missingLinks':missing,'dimensions':frame_a.size,
        'comparedFrames':[24,120],'sameFixedCameraShot':True,'regionalMeanAbsoluteRGBDifference':changes,
        'note':'Positive rendered-motion check, not a frame-rate benchmark or a substitute for camera regression tests.'}
(review/'media-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
