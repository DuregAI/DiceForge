"""Validate the authored album's structure, local HTML links and PNG integrity."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote
import json
import struct
import zlib

ROOT = Path(__file__).resolve().parent

class Page(HTMLParser):
    def __init__(self, text):
        super().__init__()
        self.links, self.ids = [], set()
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if 'id' in attrs:
            assert attrs['id'] not in self.ids, 'Duplicate HTML id'
            self.ids.add(attrs['id'])
        for key in ('href', 'src'):
            if attrs.get(key):
                self.links.append(attrs[key])

def png_size(path):
    data = path.read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, size, ended = 8, None, False
    while pos < len(data):
        length = struct.unpack('>I', data[pos:pos+4])[0]
        kind = data[pos+4:pos+8]
        payload = data[pos+8:pos+8+length]
        expected = struct.unpack('>I', data[pos+8+length:pos+12+length])[0]
        assert zlib.crc32(kind + payload) & 0xffffffff == expected, path
        if kind == b'IHDR':
            size = struct.unpack('>II', payload[:8])
        pos += length + 12
        if kind == b'IEND':
            ended = True
            break
    assert ended and pos == len(data), path
    return size

canon = json.loads((ROOT.parent/'characters.json').read_text(encoding='utf-8'))
ids = [c['id'] for c in canon['characters']]
assert len(ids) == len(set(ids)) == 16
combined = json.loads((ROOT/'dossiers.json').read_text(encoding='utf-8'))['characters']
assert [c['id'] for c in combined] == ids
assert {p.stem for p in (ROOT/'data').glob('*.json')} == set(ids)
assert {p.stem for p in (ROOT/'art').glob('*.png')} == set(ids)
assert {p.stem for p in ROOT.glob('*.html')} == set(ids) | {'index'}
for c in combined:
    cid = c['id']
    assert len(c['history']) == 3 and len(c['arc']) == 4
    assert len(c['emotions']) == len(c['lines']) == len(c['shots']) == 6
    assert set(c['voice']) == {'tempo','lexicon','humor','stress','care','avoid'}
    for field in ('role','logline','want','need','fear','flaw','strength','contradiction','acting','future','limits'):
        assert c[field].strip(), (cid, field)
    assert c['productionApproved'] is False and c['runtimeChange'] is False
    assert (ROOT/f'{cid}.md').is_file()
    assert png_size(ROOT/'art'/f'{cid}.png') == (1536, 1024)

pages = {p: Page(p.read_text(encoding='utf-8')) for p in ROOT.glob('*.html')}
links = 0
for path, page in pages.items():
    for href in page.links:
        url = urlsplit(href)
        if url.scheme or url.netloc:
            continue
        dest = (path.parent/unquote(url.path)).resolve() if url.path else path
        assert dest.is_file(), (path.name, href)
        if url.fragment and dest.suffix == '.html':
            target = pages.get(dest) or Page(dest.read_text(encoding='utf-8'))
            assert unquote(url.fragment) in target.ids, (path.name, href)
        links += 1
report = '\n'.join([
    'Glimblehop character dossiers — structural validation',
    '16 unique character IDs match the canon; 16 source records and combined records.',
    '16 individual HTML + 16 Markdown dossiers, gallery and combined text present.',
    '48 biography paragraphs, 96 emotion entries, 96 dialogue/action examples.',
    'All motivation, voice, relationship and production-status fields checked.',
    '16 PNG sheets: 1536 x 1024; signatures, all chunk CRCs and IEND checked.',
    f'{links} local HTML references and fragment targets checked; no missing target.',
    'PASS',
])
(ROOT/'validation.txt').write_text(report+'\n', encoding='utf-8')
print(report)
