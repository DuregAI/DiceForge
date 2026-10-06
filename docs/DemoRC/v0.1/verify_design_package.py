"""Validate authoring files, render every PDF page, and package the deliverables."""
from pathlib import Path
import json
import re
import hashlib
from math import ceil
from PIL import Image, ImageDraw, ImageFont
import pypdfium2 as pdfium
from pypdf import PdfReader
from zipfile import ZipFile, ZIP_DEFLATED
import build_design_book as book

ROOT = Path(__file__).resolve().parent
CHECK = ROOT/'validation'
CHECK.mkdir(exist_ok=True)
data = json.loads((ROOT/'data/dialogues.json').read_text(encoding='utf-8'))
levels = json.loads((ROOT/'data/levels.json').read_text(encoding='utf-8'))
events = data['events']
speaker_ids = {s['id'] for s in data['speakers']}
assert len(events)==138 and len({e['id'] for e in events})==138
assert len(speaker_ids)==12 and len(levels['levels'])==6
assert all(e['speakerId'] in speaker_ids for e in events)
assert all(e['text']['ru'] for e in events)
assert len(re.findall(r'^### CHAR-',(ROOT/'02_STORY_CHARACTERS.md').read_text(encoding='utf-8'),re.M))==12
assert len(re.findall(r'^### S\d\d\.\d',(ROOT/'03_LEVEL_SCRIPT_STORYBOARDS.md').read_text(encoding='utf-8'),re.M))==21

for chapter,_ in book.CHAPTERS:
    text = (ROOT/chapter).read_text(encoding='utf-8')
    for label,link in re.findall(r'\[([^\]]+)\]\(([^)]+)\)',text):
        if not re.match(r'^[a-z]+:',link,re.I):
            assert (ROOT/link.split('#')[0]).exists(), (chapter,link)

reader = PdfReader(str(book.PDF))
texts = [p.extract_text() or '' for p in reader.pages]
assert all(len(t.strip())>40 for t in texts)
combined = '\n'.join(texts)
assert all(token in combined for token in ['L01_T01','L06_T12','S00.1','S06.3','CHAR-tish','CHAR-bark'])
pdf = pdfium.PdfDocument(str(book.PDF))
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
thumbs=[]
samples = {0,1,5,12,24,40,55,68,75,81,82,87,90,94}
for i in range(len(pdf)):
    page = pdf[i]
    bitmap = page.render(scale=.50)
    image = bitmap.to_pil().convert('RGB')
    image.thumbnail((220,285))
    tile = Image.new('RGB',(236,315),'#e9eadf')
    tile.paste(image,((236-image.width)//2,5))
    ImageDraw.Draw(tile).text((12,294),f'{i+1:02d}',font=font,fill='#304438')
    thumbs.append(tile)
    bitmap.close()
    if i in samples:
        full = page.render(scale=1.35)
        full.to_pil().save(CHECK/f'page-{i+1:03d}.png')
        full.close()
    page.close()
for start in range(0,len(thumbs),30):
    block=thumbs[start:start+30]
    sheet=Image.new('RGB',(5*236,ceil(len(block)/5)*315),'#e9eadf')
    for j,tile in enumerate(block):
        sheet.paste(tile,((j%5)*236,(j//5)*315))
    sheet.save(CHECK/f'contact-{start//30+1:02d}.png')
pdf.close()

report=json.loads((ROOT/'package_validation.json').read_text(encoding='utf-8'))
report.update({'uniqueDialogueIds':138,'knownSpeakerIds':12,'characterCards':12,'storyboardFrames':21,
    'localChapterLinksValid':True,'allPdfPagesRendered':len(reader.pages),
    'validationScope':'Authoring JSON, chapter links, PDF rendering and content coverage. Gameplay runtime not verified.',
    'pdfSha256':hashlib.sha256(book.PDF.read_bytes()).hexdigest()})
(ROOT/'package_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))

archive=ROOT/'Glimblehop_Demo_Design_v0.1.zip'
with ZipFile(archive,'w',ZIP_DEFLATED,compresslevel=6) as out:
    for file in sorted(ROOT.rglob('*')):
        relative=file.relative_to(ROOT)
        if file.is_file() and file!=archive and relative.parts[0] not in ['validation','__pycache__']:
            out.write(file,arcname=str(relative))
with ZipFile(archive) as check:
    assert check.testzip() is None
print(json.dumps({'zipBytes':archive.stat().st_size,'zipMembers':len(check.namelist())}))
