"""Build the local character dossier album from authored JSON and imagegen sheets."""
from pathlib import Path
from html import escape
import json

ROOT = Path(__file__).resolve().parent
canon = json.loads((ROOT.parent / 'characters.json').read_text(encoding='utf-8'))
briefs = json.loads((ROOT / 'visual-briefs.json').read_text(encoding='utf-8'))
groups = {'hero':'Основной состав','family':'Племянники','village':'Деревня','road':'Обитатели тропы','companion':'Спутники','future':'Будущая встреча'}
notice = 'Внешность опирается на карту персонажей от 18 октября 2026. Развёрнутые биографии, мотивы и примеры общения — новая рабочая авторская версия. Будущие встречи не добавлены в действующий сценарий демо.'
art_notice = 'Концептуальные ракурсы и актёрские состояния. Это основа для следующего этапа моделирования; точные ортографические виды, скрытые сочленения и размеры уточняются отдельно.'
voice_labels = {'tempo':'Темп и ритм','lexicon':'Слова и построение фразы','humor':'Юмор','stress':'Под давлением','care':'Как проявляет заботу','avoid':'Чего избегать'}
motives = [('want','Чего хочет'),('need','Чему нужно научиться'),('fear','Чего боится'),('flaw','Что мешает'),('strength','На что опирается'),('contradiction','Внутреннее противоречие')]

def e(text):
    return escape(str(text), quote=True)

def paras(items):
    return ''.join(f'<p>{e(x)}</p>' for x in items)

def bullets(items):
    return '<ul>' + ''.join(f'<li>{e(x)}</li>' for x in items) + '</ul>'

def table(rows, headings):
    return '<div class="table-wrap"><table><thead><tr>' + ''.join(f'<th>{e(x)}</th>' for x in headings) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(f'<td>{e(x)}</td>' for x in row) + '</tr>' for row in rows) + '</tbody></table></div>'

def shell(title, body):
    return f'<!doctype html>\n<html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{e(title)}</title><link rel="stylesheet" href="album.css"></head><body><main>{body}</main></body></html>'

combined=[]
markdown=['# Glimblehop — подробные досье персонажей\n','18 октября 2026. 16 персонажей; отдельный визуальный лист и шесть эмоциональных состояний для каждого.\n',notice+'\n']
tiles=[]
for pos,c in enumerate(canon['characters']):
    cid=c['id']
    d=json.loads((ROOT/'data'/f'{cid}.json').read_text(encoding='utf-8'))
    assert len(d['history'])==3 and len(d['emotions'])==6 and len(d['lines'])==6
    assert (ROOT/'art'/f'{cid}.png').is_file()
    d.update({'id':cid,'nameRu':c['nameRu'],'nameEn':c['nameEn'],'appearance':c['appearance'],'palette':c['palette'],'sheet':f'art/{cid}.png','narrativeStatus':'working_author_expansion','runtimeChange':False,'productionApproved':False})
    image_labels=[x.strip() for x in briefs[cid]['emotions'].split(',')]
    assert len(image_labels)==6
    emotion_rows=[(label,*row) for label,row in zip(image_labels,d['emotions'])]
    shots=[('Общий план спереди','Силуэт, лицо, одежда, положение ступней; без потери главных признаков.'),('Боковой ракурс','Глубина фигуры, профиль лица, предметы на ремнях и посадка одежды.'),('Вид со спины','Причёска, крепления, рюкзак либо панцирь; признаки не зеркалятся.'),('Средний план в три четверти','Лицо вместе с характерным жестом и позой корпуса.'),('Крупные детали','Материал и узнаваемые предметы; особенности лица, уха, руки или панциря.'),('Шесть эмоциональных портретов','Взгляд, брови, рот и положение головы; у существ также усики, крылья и поза.')]
    d['shots']=shots
    combined.append(d)
    next_c=canon['characters'][(pos+1)%len(canon['characters'])]
    body=f'<nav><a href="index.html">← Все персонажи</a><a href="#{cid}-story">История</a><a href="#{cid}-voice">Общение</a><a href="#{cid}-emotion">Эмоции</a><a href="{cid}.md">Текст карточки</a></nav>'
    body+=f'<header><div class="eyebrow">GLIMBLEHOP · {e(groups[c["group"]])} · {pos+1:02d} / 16</div><h1>{e(c["nameRu"])} <span>/ {e(c["nameEn"])}</span></h1><p class="tagline">{e(d["logline"])}</p><p>{e(d["role"])}</p></header>'
    body+=f'<figure><a href="art/{cid}.png"><img class="sheet" src="art/{cid}.png" alt="{e(c["nameRu"])}: ракурсы, детали и шесть эмоций" width="1536" height="1024"></a><figcaption>Нажмите на лист, чтобы открыть изображение. {e(art_notice)}</figcaption></figure>'
    body+=f'<aside>{e(notice)}</aside><section><h2>Образ</h2><p>{e(c["appearance"])}.</p><p><strong>Палитра:</strong> {e(c["palette"])}.</p></section>'
    body+=f'<section id="{cid}-story"><h2>История</h2>{paras(d["history"])}</section>'
    body+='<section><h2>Мотивация и характер</h2><div class="motives">'+''.join(f'<article><h3>{label}</h3><p>{e(d[key])}</p></article>' for key,label in motives)+'</div></section>'
    body+='<section><h2>Движение истории</h2><ol>'+''.join(f'<li>{e(x)}</li>' for x in d['arc'])+'</ol><h3>Отношения</h3>'+bullets(d['relations'])+'</section>'
    body+=f'<section id="{cid}-voice"><h2>Характер общения</h2>'+''.join(f'<p><strong>{voice_labels[k]}.</strong> {e(v)}</p>' for k,v in d['voice'].items())+'<h3>Примеры в сцене</h3>'+table(d['lines'],['Ситуация','Реплика или действие'])+'</section>'
    body+=f'<section id="{cid}-emotion"><h2>Эмоции и пластика</h2>'+table(emotion_rows,['Подпись на листе','Состояние','Актёрская задача'])+f'<p><strong>Движение:</strong> {e(d["acting"])}</p></section>'
    body+='<section><h2>Планы и ракурсы</h2>'+table(shots,['План','Что показывает'])+f'<h3>Будущая история</h3><p>{e(d["future"])}</p><aside><strong>Сохранить при дальнейшей работе:</strong> {e(d["limits"])}</aside></section>'
    body+=f'<footer><a href="index.html">Все досье</a><a href="{next_c["id"]}.html">Далее: {e(next_c["nameRu"])} →</a></footer>'
    (ROOT/f'{cid}.html').write_text(shell(f'{c["nameRu"]} — досье Glimblehop',body),encoding='utf-8')
    md=f'# {c["nameRu"]} / {c["nameEn"]}\n\n{d["logline"]}\n\n{d["role"]}\n\n{notice}\n\n![Лист персонажа](art/{cid}.png)\n\n{art_notice}\n\n## Образ\n\n{c["appearance"]}.\n\nПалитра: {c["palette"]}.\n\n## История\n\n'+'\n\n'.join(d['history'])+'\n\n## Мотивация и характер\n\n'
    md+='\n\n'.join(f'**{label}.** {d[key]}' for key,label in motives)+'\n\n## Движение истории\n\n'+'\n'.join(f'{i+1}. {x}' for i,x in enumerate(d['arc']))+'\n\n## Отношения\n\n'+'\n'.join('- '+x for x in d['relations'])+'\n\n## Характер общения\n\n'
    md+='\n\n'.join(f'**{voice_labels[k]}.** {v}' for k,v in d['voice'].items())+'\n\n### Примеры в сцене\n\n'+'\n\n'.join(f'**{label}:** {line}' for label,line in d['lines'])+'\n\n## Эмоции и пластика\n\n'
    md+='\n\n'.join(f'**{label} / {emotion}:** {task}' for label,emotion,task in emotion_rows)+f'\n\n**Движение:** {d["acting"]}\n\n## Планы и ракурсы\n\n'+'\n\n'.join(f'**{a}:** {b}' for a,b in shots)+f'\n\n## Будущая история\n\n{d["future"]}\n\n## Ограничения канона\n\n{d["limits"]}\n'
    (ROOT/f'{cid}.md').write_text(md,encoding='utf-8')
    markdown.append('\n'.join('#'+line if line.startswith('#') else line for line in md.split('\n')))
    tiles.append(f'<a class="tile" href="{cid}.html" data-search="{e(c["nameRu"]+" "+c["nameEn"]+" "+groups[c["group"]])}"><img src="art/{cid}.png" alt="Лист {e(c["nameRu"])}" loading="lazy" width="1536" height="1024"><div><small>{e(groups[c["group"]])}</small><h2>{e(c["nameRu"])} <span>/ {e(c["nameEn"])}</span></h2><p>{e(d["logline"])}</p><b>Открыть досье →</b></div></a>')

index='<header><div class="eyebrow">GLIMBLEHOP · 18 ОКТЯБРЯ 2026</div><h1>Персонажи в деталях</h1><p class="tagline">16 досье · 16 визуальных листов · 96 эмоциональных портретов</p><p>Ракурсы и детали внешности, история, желания и страхи, отношения, голос и примеры поведения в сценах.</p></header>'
index+=f'<aside>{e(notice)} Имена Кусь, Пятнышко и Рута остаются рабочими.</aside><nav><a href="../index.html">Общая карта</a><a href="ALL_CHARACTERS.md">Весь текст одним файлом</a><a href="dossiers.json">Структурированные данные</a><a href="REVIEW.md">Проверка и ограничения</a></nav>'
index+='<label class="search">Найти персонажа<input id="search" type="search" placeholder="Имя или группа" autocomplete="off"></label><p id="count" aria-live="polite">Показано 16 из 16</p><div class="grid">'+''.join(tiles)+'</div><p id="empty" hidden>Совпадений нет. Попробуйте другое имя.</p><footer>Визуальные листы созданы встроенным imagegen по закреплённым референсам. <a href="generation-prompts.json">Точные запросы</a></footer>'
index+='''<script>const search=document.querySelector('#search');const tiles=[...document.querySelectorAll('.tile')];search.addEventListener('input',()=>{const q=search.value.trim().toLocaleLowerCase('ru');let n=0;for(const t of tiles){t.hidden=!t.dataset.search.toLocaleLowerCase('ru').includes(q);if(!t.hidden)n++;}document.querySelector('#count').textContent=`Показано ${n} из 16`;document.querySelector('#empty').hidden=n!==0;});</script>'''
(ROOT/'index.html').write_text(shell('Glimblehop — подробные досье персонажей',index),encoding='utf-8')
(ROOT/'ALL_CHARACTERS.md').write_text('\n\n---\n\n'.join(markdown),encoding='utf-8')
(ROOT/'dossiers.json').write_text(json.dumps({'version':1,'updated':'2026-10-18','status':'working_author_expansion','characters':combined},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Built {len(combined)} dossiers, index, combined text and JSON.')
