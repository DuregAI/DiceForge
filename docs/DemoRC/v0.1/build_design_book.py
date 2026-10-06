"""Build the local demo design book from editable Markdown and generated art.

Uses the bundled reportlab/Pillow/pypdf libraries. No network access or runtime
game changes. Run after the authoring chapters and selected art are complete.
"""
from pathlib import Path
import html
import json
import re
from io import BytesIO
from reportlab.platypus import (
    BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak,
    NextPageTemplate, Image, LongTable, TableStyle, KeepTogether,
)
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.enums import TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from PIL import Image as PILImage
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parent
PDFDIR = ROOT / 'output' / 'pdf'
PDFDIR.mkdir(parents=True, exist_ok=True)
PDF = PDFDIR / 'Glimblehop_Demo_Design_v0.1.pdf'
CHAPTERS = [
    ('00_OVERVIEW.md', 'Обзор демо'),
    ('01_GAME_DESIGN.md', 'Правила и баланс'),
    ('02_STORY_CHARACTERS.md', 'История и персонажи'),
    ('03_LEVEL_SCRIPT_STORYBOARDS.md', 'Сценарий и комиксы'),
    ('04_DIALOGUE_TUTORIAL.md', 'Диалоги и обучение'),
    ('05_ART_LOCATIONS.md', 'Арт и локации'),
    ('06_PRODUCTION_RC.md', 'Производство и RC'),
]
ART = [
    ('cast-01-heroes', 'Тиш, Лума и Бум', 'Три управляемых героя. Слева направо: Тиш, Лума, Бум. Полный рост, эмоции и реквизит.'),
    ('cast-02-family', 'Корень, Пип и Фика', 'Наставник и маленькие племянники. Корень с фонарём, Пип с компасом, Фика с рисунками.'),
    ('cast-03-village', 'Мира, Уна и Шал', 'Подружки невесты и проводник. Мира с венком, Уна со свирелью, Шал с веслом.'),
    ('cast-04-rivals', 'Клоч, Рыж и Жук', 'Сюжетный антагонист, дружеский соперник и спокойное препятствие. Оружия и урона в демо нет.'),
    ('locations-01-03', 'Локации первого знакомства', 'Слева направо: калитка у дома, ручей коротких шагов, мост двух друзей. Геометрия задаётся конфигом, рисунок показывает оформление.'),
    ('locations-04-06', 'Локации общей дороги', 'Слева направо: мастерская у корней, старая переправа, поляна общего света. Свет и декор меняют настроение общей восьмиклеточной тропы.'),
    ('story-S00-v2', 'S00 Подготовка и пропавший свет', 'Подготовка к свадьбе → Клоч уносит линзу → Тиш получает рисунок пути и отправляется. Бум присоединяется позже.'),
    ('story-S01', 'S01 След у ручья', 'Племянники у безопасной калитки → компас и рисунок пера → самостоятельный путь Тиша к ручью.'),
    ('story-S02', 'S02 Два друга', 'Тиш на дальнем берегу → Лума обследовала мост → равная пара выбирает дорогу вместе.'),
    ('story-S03', 'S03 Помощь Корня и Бума', 'Пустая оправа общего фонаря → готовый запасной фонарик → на тропу выходят трое друзей.'),
    ('story-S04-v2', 'S04 Переправа и вызов', 'Жук остаётся мирным лесным существом → Шал показывает переправу → Рыж предлагает соревнование.'),
    ('story-S05', 'S05 Кому нужен свет', 'Вместе важнее первого места → у Клоча пустая вторая чашка → запасной фонарь обменян на общую линзу.'),
    ('story-S06', 'S06 Общий стол', 'Общий свет восстановлен → свадьба Тиша и Лумы → у Клоча есть гости, Бум счастлив.'),
]

for family, file in [('Book', 'arial.ttf'), ('BookBold', 'arialbd.ttf'), ('BookItalic', 'ariali.ttf')]:
    pdfmetrics.registerFont(TTFont(family, str(Path('C:/Windows/Fonts') / file)))
pdfmetrics.registerFontFamily('Book', normal='Book', bold='BookBold', italic='BookItalic', boldItalic='BookBold')

INK = colors.HexColor('#304438')
MUTED = colors.HexColor('#6B7468')
TEAL = colors.HexColor('#477D72')
CREAM = colors.HexColor('#FBF8F0')
STYLES = {
    'body': ParagraphStyle('body', fontName='Book', fontSize=9.8, leading=14.1, textColor=INK, spaceAfter=6, splitLongWords=True),
    'h1': ParagraphStyle('h1', fontName='BookBold', fontSize=23, leading=28, textColor=INK, spaceBefore=8, spaceAfter=16, keepWithNext=True),
    'h2': ParagraphStyle('h2', fontName='BookBold', fontSize=14.5, leading=19, textColor=TEAL, spaceBefore=15, spaceAfter=8, keepWithNext=True),
    'h3': ParagraphStyle('h3', fontName='BookBold', fontSize=11.5, leading=16, textColor=INK, spaceBefore=11, spaceAfter=6, keepWithNext=True),
    'h4': ParagraphStyle('h4', fontName='BookBold', fontSize=10, leading=14, textColor=INK, spaceBefore=8, spaceAfter=4, keepWithNext=True),
    'caption': ParagraphStyle('caption', fontName='Book', fontSize=9, leading=13, textColor=MUTED, spaceAfter=9),
    'cell': ParagraphStyle('cell', fontName='Book', fontSize=8, leading=11.2, textColor=INK, spaceAfter=0, splitLongWords=True),
    'cover': ParagraphStyle('cover', fontName='BookBold', fontSize=36, leading=42, textColor=INK, spaceAfter=14),
    'sub': ParagraphStyle('sub', fontName='Book', fontSize=18, leading=25, textColor=TEAL, spaceAfter=14),
    'bullet': ParagraphStyle('bullet', fontName='Book', fontSize=9.8, leading=14.1, textColor=INK, spaceAfter=5, leftIndent=12, firstLineIndent=-10),
    'quote': ParagraphStyle('quote', fontName='BookItalic', fontSize=9.8, leading=14.1, textColor=TEAL, spaceAfter=6, leftIndent=12),
    'code': ParagraphStyle('code', fontName='Book', fontSize=8, leading=11, textColor=MUTED, leftIndent=8, spaceAfter=3),
}

def clean(text):
    return text.replace('\u2011', '-').replace('\u2013', '-').replace('\u2014', '-').replace('\u00a0', ' ')

def inline(text, pdf=False):
    text = clean(text) if pdf else text
    text = html.escape(text)
    # Keep link labels readable in the PDF; HTML retains safe local links.
    if pdf:
        text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'\1', text)
    else:
        text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', lambda m: '<a href="' + m[2] + '">' + m[1] + '</a>', text)
    text = re.sub(r'`([^`]+)`', r'<font color="#477D72">\1</font>' if pdf else r'<code>\1</code>', text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*([^*]+)\*(?!\*)', r'<i>\1</i>', text)
    return text

def parse(text):
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        s = lines[i].strip()
        if not s:
            i += 1
            continue
        if s.startswith('```'):
            i += 1
            block = []
            while i < len(lines) and not lines[i].strip().startswith('```'):
                block.append(lines[i]); i += 1
            i += 1
            yield ('code', '\n'.join(block))
            continue
        if s.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].strip().startswith('|'):
                line = lines[i].strip()
                cells = [c.strip() for c in line.strip('|').split('|')]
                if not all(re.fullmatch(r'[:\- ]+', c or '-') for c in cells):
                    rows.append(cells)
                i += 1
            yield ('table', rows)
            continue
        m = re.match(r'^(#{1,4})\s+(.+)', s)
        if m:
            yield ('h' + str(len(m[1])), m[2]); i += 1
            continue
        if re.match(r'^[-*]\s+', s):
            yield ('bullet', re.sub(r'^[-*]\s+', '', s)); i += 1
            continue
        if re.match(r'^\d+\.\s+', s):
            yield ('bullet', s); i += 1
            continue
        if s.startswith('> '):
            yield ('quote', s[2:]); i += 1
            continue
        para = [s]; i += 1
        while i < len(lines):
            line = lines[i].strip()
            if not line or re.match(r'^(#{1,4} |[-*] |\d+\. |\||```|> )', line):
                break
            para.append(line); i += 1
        yield ('body', ' '.join(para))

PW, PH = A4
MARGIN = 19 * 2.83464567
TEXT_WIDTH = PW - 2 * MARGIN

def table_widths(rows, width):
    count = max(len(r) for r in rows)
    scores = []
    for col in range(count):
        vals = [len(re.sub(r'[*`]', '', row[col])) for row in rows if col < len(row)]
        scores.append(min(110, max(20, sum(vals) / max(1, len(vals)))) ** .5)
    return [width * x / sum(scores) for x in scores]

def pdf_blocks(text):
    result = []
    for kind, value in parse(text):
        if kind == 'table':
            n = max(len(r) for r in value)
            cells = [[Paragraph(inline(v, True), STYLES['cell']) for v in r + [''] * (n-len(r))] for r in value]
            table = LongTable(cells, colWidths=table_widths(value, TEXT_WIDTH), repeatRows=1, hAlign='LEFT')
            table.setStyle(TableStyle([
                ('BACKGROUND', (0,0), (-1,0), colors.HexColor('#E5ECE1')),
                ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, colors.HexColor('#F7F8F2')]),
                ('VALIGN', (0,0), (-1,-1), 'TOP'),
                ('LEFTPADDING', (0,0), (-1,-1), 6), ('RIGHTPADDING', (0,0), (-1,-1), 6),
                ('TOPPADDING', (0,0), (-1,-1), 7), ('BOTTOMPADDING', (0,0), (-1,-1), 7),
                ('LINEBELOW', (0,0), (-1,0), .5, colors.HexColor('#A4B7A1')),
            ]))
            result += [table, Spacer(1, 8)]
        elif kind == 'code':
            for line in value.splitlines():
                result.append(Paragraph(html.escape(clean(line)) or ' ', STYLES['code']))
        else:
            prefix = '- ' if kind == 'bullet' else ''
            result.append(Paragraph(prefix + inline(value, True), STYLES[kind]))
    return result

def fit_image(path, maxw, maxh):
    with PILImage.open(path) as im:
        w, h = im.size
        # Encode only the PDF's embedded stream; selected PNG originals stay intact.
        encoded = BytesIO()
        im.convert('RGB').save(encoded, format='JPEG', quality=91, subsampling=0)
        encoded.seek(0)
    scale = min(maxw/w, maxh/h)
    return Image(encoded, width=w*scale, height=h*scale, hAlign='CENTER')

class BookDoc(BaseDocTemplate):
    def __init__(self, filename):
        super().__init__(str(filename), pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN,
            topMargin=MARGIN, bottomMargin=MARGIN, title='Glimblehop Диздок демо v0.1',
            author='Команда разработки Glimblehop')
        portrait = Frame(MARGIN, MARGIN, TEXT_WIDTH, PH-2*MARGIN, id='portrait', leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
        LW,LH = landscape(A4)
        landscape_frame = Frame(MARGIN, MARGIN, LW-2*MARGIN, LH-2*MARGIN, id='landscape', leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
        self.addPageTemplates([
            PageTemplate(id='Portrait', frames=[portrait], pagesize=A4, onPage=self.draw_page),
            PageTemplate(id='Landscape', frames=[landscape_frame], pagesize=landscape(A4), onPage=self.draw_page),
        ])
        self.heading_count = 0
        self.last_heading = 'Glimblehop'

    def beforeDocument(self):
        self.heading_count = 0
        self.last_heading = 'Glimblehop'

    def draw_page(self, canvas, doc):
        w,h = canvas._pagesize
        canvas.saveState()
        canvas.setFillColor(CREAM); canvas.rect(0,0,w,h,fill=1,stroke=0)
        canvas.setFillColor(MUTED); canvas.setFont('Book', 8)
        canvas.drawString(MARGIN, h-30, 'GLIMBLEHOP  /  Демо 0.1  /  6 октября 2026')
        canvas.drawRightString(w-MARGIN, 28, str(doc.page))
        canvas.drawString(MARGIN, 28, 'Документация и концепты для производства')
        canvas.restoreState()

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and flowable.style.name in ('h1', 'h2'):
            level = 0 if flowable.style.name == 'h1' else 1
            text = flowable.getPlainText()
            key = 'heading-' + str(self.heading_count)
            self.heading_count += 1
            self.canv.bookmarkPage(key)
            self.canv.addOutlineEntry(text, key, level=level, closed=True)
            if level == 0:
                self.notify('TOCEntry', (0, text, self.page, key))

def make_pdf():
    story = [Spacer(1, 25), Paragraph('GLIMBLEHOP', STYLES['cover']),
        Paragraph('Диздок демо<br/>на шесть уровней', STYLES['sub']),
        Paragraph('Уютная сказка, юмор и свадьба', STYLES['caption']),
        fit_image(ROOT/'art/cast-01-heroes.png', TEXT_WIDTH, 255), Spacer(1, 20),
        Paragraph('12 персонажей  ·  6 локаций  ·  138 игровых событий диалогов  ·  21 кадр истории', STYLES['body']),
        Paragraph('Версия 0.1. Рабочие имена и баланс.<br/>Пакет документации и арта для подготовки RC.', STYLES['caption']),
        PageBreak(), Paragraph('Содержание', STYLES['h1'])]
    toc = TableOfContents()
    toc.levelStyles = [ParagraphStyle('toc', fontName='Book', fontSize=11, leading=19, textColor=INK, spaceBefore=7)]
    story += [toc, PageBreak()]
    for index,(file, title) in enumerate(CHAPTERS):
        if index:
            story.append(PageBreak())
        story += pdf_blocks((ROOT/file).read_text(encoding='utf-8'))
    story += [NextPageTemplate('Landscape'), PageBreak()]
    lw,lh = landscape(A4)
    for index,(asset, title, caption) in enumerate(ART):
        if index:
            story.append(PageBreak())
        story += [Paragraph(title, STYLES['h1']),
            fit_image(ROOT/'art'/f'{asset}.png', lw-2*MARGIN, lh-2*MARGIN-92),
            Spacer(1, 10), Paragraph(caption, STYLES['caption']),
            Paragraph('Концептуальный ориентир. Локализуемые реплики, геометрия и производственные ассеты оформляются отдельно.', STYLES['caption'])]
    BookDoc(PDF).multiBuild(story)

def html_blocks(text, slug):
    result = []
    counter = 0
    for kind, value in parse(text):
        if kind == 'table':
            rows = []
            for index,row in enumerate(value):
                tag = 'th' if index == 0 else 'td'
                rows.append('<tr>' + ''.join(f'<{tag}>{inline(c)}</{tag}>' for c in row) + '</tr>')
            result.append('<div class="table-scroll"><table>' + ''.join(rows) + '</table></div>')
        elif kind == 'code':
            result.append('<pre>' + html.escape(value) + '</pre>')
        elif kind.startswith('h'):
            counter += 1
            result.append(f'<{kind} id="{slug}-{counter}">{inline(value)}</{kind}>')
        elif kind == 'bullet':
            result.append('<p class="bullet">• ' + inline(value) + '</p>')
        elif kind == 'quote':
            result.append('<blockquote>' + inline(value) + '</blockquote>')
        else:
            result.append('<p>' + inline(value) + '</p>')
    return '\n'.join(result)

def make_html_and_master():
    nav, content, master = [], [], ['# Glimblehop Диздок демо v0.1\n']
    for file,title in CHAPTERS:
        slug = file[:2]
        text = (ROOT/file).read_text(encoding='utf-8')
        nav.append(f'<a href="#chapter-{slug}">{html.escape(title)}</a>')
        content.append(f'<section id="chapter-{slug}">{html_blocks(text,slug)}</section>')
        master.append(text)
    nav.append('<a href="#art-atlas">Альбом концептов</a>')
    figures = []
    master.append('\n# Альбом концептов\n')
    for asset,title,caption in ART:
        figures.append(f'<figure><h2>{html.escape(title)}</h2><a href="art/{asset}.png"><img src="art/{asset}.png" alt="{html.escape(title)}" loading="lazy"></a><figcaption>{html.escape(caption)}</figcaption></figure>')
        master.append(f'\n## {title}\n\n![{title}](art/{asset}.png)\n\n{caption}\n')
    content.append('<section id="art-atlas"><h1>Альбом концептов</h1>' + ''.join(figures) + '</section>')
    css = '''*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#f7f4eb;color:#304438;font:17px/1.65 Georgia,"Times New Roman",serif}nav{position:fixed;left:0;top:0;bottom:0;width:235px;padding:32px 22px;background:#304438;color:#fbf8f0;overflow:auto;font:14px/1.6 Arial,sans-serif}nav strong{display:block;font-size:21px;letter-spacing:1px;margin-bottom:8px}nav small{display:block;color:#d5dfc9;margin-bottom:24px}nav a{display:block;color:#edf0e5;text-decoration:none;padding:8px 0;border-bottom:1px solid #ffffff20}main{margin-left:235px;max-width:1320px;padding:42px 58px}section{margin-bottom:68px;padding-bottom:35px;border-bottom:1px solid #d5dccf;scroll-margin-top:20px}h1,h2,h3,h4{font-family:Arial,sans-serif;line-height:1.25;color:#304438}h1{font-size:33px;margin:20px 0 30px}h2{font-size:25px;color:#477d72;margin:40px 0 16px}h3{font-size:20px;margin:30px 0 13px}p{max-width:1000px;margin:0 0 16px}a{color:#337f73}code{font:14px/1.5 Consolas,monospace;background:#e8ece0;padding:2px 4px;border-radius:4px}pre{white-space:pre-wrap;background:#eef0e7;padding:20px;font:14px/1.5 Consolas,monospace}.bullet{padding-left:18px;text-indent:-18px;margin:0 0 8px}.table-scroll{overflow:auto;margin:18px 0 24px}table{border-collapse:collapse;width:100%;font:14px/1.5 Arial,sans-serif}th,td{padding:12px;vertical-align:top;border-bottom:1px solid #d8dfd2;min-width:85px}th{background:#e0e8d9;text-align:left}tbody tr:nth-child(even){background:#ffffff70}figure{margin:25px 0 45px}img{display:block;width:100%;height:auto;border-radius:12px;border:1px solid #d9dbcc;background:#fffdf6}figcaption{font:15px/1.5 Arial,sans-serif;color:#677465;padding:14px 4px}blockquote{border-left:3px solid #7f9a6a;padding-left:18px}button{background:#e5ad56;color:#304438;border:0;border-radius:6px;padding:10px 14px;font-weight:bold;cursor:pointer}header{margin-bottom:40px}.note{font:14px/1.6 Arial,sans-serif;color:#687164}@media(max-width:850px){nav{position:relative;width:100%;height:auto}nav a{display:inline-block;margin-right:20px}main{margin:0;padding:24px}h1{font-size:28px}}@media print{nav,button{display:none}main{margin:0;padding:0;max-width:none}body{background:white;font-size:11pt}section{break-before:page}figure,table{break-inside:avoid}a{color:inherit;text-decoration:none}}'''
    document = '<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Glimblehop Диздок демо 0.1</title><style>'+css+'</style></head><body><nav><strong>GLIMBLEHOP</strong><small>Демо 0.1 · 6 октября 2026</small>'+''.join(nav)+'<br><a href="output/pdf/Glimblehop_Demo_Design_v0.1.pdf">Открыть PDF</a><a href="DESIGN_DOCUMENT.md">Редактируемый текст</a><a href="data/levels.json">Данные уровней</a><a href="data/dialogues.json">Данные диалогов</a></nav><main><header><p class="note">Уютная сказка, юмор и свадьба · Документация и концепты перед подготовкой RC</p><button onclick="window.print()">Печатать</button></header>'+''.join(content)+'</main></body></html>'
    (ROOT/'Glimblehop_Demo_Design_v0.1.html').write_text(document,encoding='utf-8')
    (ROOT/'DESIGN_DOCUMENT.md').write_text('\n\n'.join(master),encoding='utf-8')

def validate():
    missing = [str(ROOT/'art'/f'{name}.png') for name,_,_ in ART if not (ROOT/'art'/f'{name}.png').is_file()]
    if missing:
        raise ValueError('Missing selected art: ' + ', '.join(missing))
    dialogues = json.loads((ROOT/'data/dialogues.json').read_text(encoding='utf-8'))
    levels = json.loads((ROOT/'data/levels.json').read_text(encoding='utf-8'))
    events = dialogues.get('events', dialogues.get('lines', []))
    result = {'selectedConcepts':len(ART),'chapters':len(CHAPTERS), 'dialogueEvents':len(events), 'levelCount':len(levels['levels'])}
    return result

if __name__ == '__main__':
    facts = validate()
    make_html_and_master()
    make_pdf()
    reader = PdfReader(str(PDF))
    facts.update({'pdfPages':len(reader.pages),'pdfBytes':PDF.stat().st_size,'pdfTextCharacters':sum(len(p.extract_text() or '') for p in reader.pages)})
    (ROOT/'package_validation.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(facts,ensure_ascii=False))
