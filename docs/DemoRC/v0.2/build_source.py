"""Reproducible bilingual demo revision; Unity imports narrative.json and levels.json."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
old = json.loads((ROOT / 'docs/DemoRC/v0.1/data/dialogues.json').read_text(encoding='utf-8-sig'))
eng = json.loads((ROOT / 'docs/DemoRC/v0.1/data/dialogues.en.json').read_text(encoding='utf-8-sig'))
names = {s['id']: s['name'] for s in eng['speakers']}
speakers = [dict(id=s['id'], ru=s['nameRu'], en=names[s['id']]) for s in old['speakers']]
speakers.append(dict(id='scratchy', ru='Царап', en='Scratchy'))
events = []

def event(level, suffix, speaker, ru, en, domain='', topic='', winner=''):
    kind = {'I':'level_intro', 'V':'level_success', 'F':'soft_failure', 'R':'return', 'T':'tutorial'}[suffix[0]]
    events.append(dict(id=f'L{level:02}_{suffix}', levelId=f'L{level}', speakerId=speaker,
        kind=kind, domainEvent=domain, helpTopic=topic, ru=ru, en=en, priority=80,
        firstFinisherId=winner))

titles = [('Шорох в кустах','A rustle in the bushes'),('По лесной тропе','Along the woodland trail'),
          ('Треснувшая плита','The cracked tile'),('Перегонки с Царапом','A race with Scratchy'),
          ('Кто быстрее, Лума?','A race with Jo'),('Огонёк за перевалом','A light beyond the ridge')]
heroes = [['tish'],['tish'],['tish'],['tish','scratchy'],['tish','luma'],['tish','luma','bum']]
intros = [
 [('tish','Ещё один узел — и указатель снова цел. Кто там шуршит?','One more knot and the sign is fixed. Who is rustling over there?'),
  ('scratchy','Тиш! Пс-с… Сюда, за кусты! Я кое-что видел!','Tish! Psst… Over here, behind the bushes! I saw something!')],
 [('scratchy','Теперь пройдём всю тропу. Я знаю короткие шаги и длинные!','Now for the whole trail. I know short steps and long ones!'),
  ('tish','Показывай дорогу. Только колокольчик не потеряй.','Lead the way. Just do not lose your bell.')],
 [('scratchy','Стой! Эта плита треснула. На неё лучше не наступать.','Wait! That tile is cracked. Better not step on it.'),
  ('tish','Зато следующая целая. Перепрыгну с соседней!','But the next one is sound. I will jump across from the tile beside it!')],
 [('scratchy','До флажка наперегонки? Колокольчик считает честно!','Race you to the flag? My bell keeps an honest score!'),
  ('tish','Договорились! Кто первый, тот выбирает следующую тропу.','Deal! The first one there picks the next trail.')],
 [('luma','Слышала ваш звон издалека. Тиш, теперь со мной наперегонки!','I heard that bell from far away. Tish, race me this time!'),
  ('tish','Посмотрим, кто первым доберётся до флажка!','Let us see who reaches the flag first!')],
 [('bum','Я принёс перекус. Куда мы все так спешим?','I brought snacks. Where is everyone rushing?'),
  ('scratchy','За перевалом мелькнула птица с янтарной линзой. Покажу!','A bird with an amber lens flew beyond the ridge. I will show you!')]
]
success = [
 [('scratchy','Вот ты где! Я Царап. Обычно меня сначала слышат.','There you are! I am Scratchy. People usually hear me first.')],
 [('tish','Вся тропа позади. И ни одного потерянного колокольчика.','The whole trail is behind us. And not a single bell lost.')],
 [('scratchy','Вот это прыжок! Треснувшая плита даже не шелохнулась.','What a jump! The cracked tile did not even stir.')],
 [], [],
 [('bum','Все здесь. Можно передохнуть — и посмотреть, куда летела птица.','Everyone is here. We can rest, then see where that bird went.')]
]
for i in range(1,7):
    for j, (who,ru,en) in enumerate(intros[i-1],1): event(i,f'I{j:02}',who,ru,en)
    for j, (who,ru,en) in enumerate(success[i-1],1): event(i,f'V{j:02}',who,ru,en)
    event(i,'F01','scratchy','Ничего, попробуем ещё раз. Я подожду!','No worries. Let us try again. I will wait!')
    event(i,'R01','scratchy','Продолжим с того места, где остановились.','Let us carry on from where we stopped.')
    event(i,'R02','scratchy','Новая попытка! Выбери шаг, затем друга.','A fresh start! Choose a step, then a friend.')
    goal_ru = 'Доведи Тиша до флажка.' if i<4 else ('Доведи обоих до флажка. Кто придёт первым?' if i<6 else 'Доведи всех троих до флажка. Друзья выходят по очереди.')
    goal_en = 'Bring Tish to the flag.' if i<4 else ('Bring both friends to the flag. Who will arrive first?' if i<6 else 'Bring all three friends to the flag. Each friend exits separately.')
    event(i,'T01','scratchy',goal_ru,goal_en,'step_offer_ready')
    event(i,'T02','scratchy','Теперь нажми друга на плите или его имя внизу.','Now tap a friend on the tile or their name below.','step_selected')
    event(i,'T03','scratchy','Получилось! Снова выбери шаг и продолжай.','That worked! Choose another step and keep going.','legal_move_applied')
    event(i,'T04','scratchy','На треснувшую плиту приземляться нельзя. Подойди к соседней и выбери шаг 2.' if i==3 else 'Это место занято. Выбери другой шаг или друга.',
          'You cannot land on the cracked tile. Get to the tile beside it and choose step 2.' if i==3 else 'That spot is occupied. Choose a different step or friend.','illegal_destination_previewed')
    event(i,'T05','scratchy','Один уже у флажка! Теперь доведи второго.' if i<6 else 'Один уже дошёл. Доведём остальных!',
          'One friend has arrived! Now bring the other one.' if i<6 else 'One friend has arrived. Bring the others!','hero_exited')
    event(i,'T06','scratchy','Сначала выбери число шага, затем друга. Подсветка покажет место посадки.','Choose a step value, then a friend. The highlight shows the landing spot.','help_requested','input_order')
    event(i,'T07','scratchy',goal_ru,goal_en,'help_requested','goal')
    event(i,'T08','scratchy','На треснувшую плиту нельзя встать, но через неё можно прыгнуть.' if i==3 else 'Шаг 2 переносит друга через одну плиту. Проверь, куда он приземлится.',
          'You cannot stand on the cracked tile, but you can jump over it.' if i==3 else 'Step 2 jumps over one tile. Check where your friend will land.','help_requested','step_choice')

for level, rival, rival_ru, rival_en in [(4,'scratchy','Царап','Scratchy'),(5,'luma','Лума','Jo')]:
    event(level,'V01','tish','Я первый! Но следующую тропу всё равно выбираем вместе.','I am first! But we are still choosing the next trail together.',winner='tish')
    event(level,'V02',rival,'Ладно, шустрый! В следующий раз посмотрим.','All right, speedy! We will see next time.',winner='tish')
    event(level,'V03',rival,'Дзинь! Царап у флажка первым!' if level==4 else 'Я первая! Знание тропы тоже считается.',
          'Ding! Scratchy reaches the flag first!' if level==4 else 'I am first! Knowing the trail counts too.',winner=rival)
    event(level,'V04','tish','С таким колокольчиком финиш точно не пропустишь.' if level==4 else 'Считается. Но реванш за мной!',
          'With that bell, nobody could miss your finish.' if level==4 else 'It does. But I want a rematch!',winner=rival)

# Shared input recovery lines keep stable IDs used by the controller.
old_en = {e['id']:e['en'] for e in eng['events']}
for e in old['events']:
    if e['id'].startswith('G_'):
        events.append(dict(id=e['id'],levelId=e.get('levelId',''),speakerId='scratchy',kind=e['kind'],
            domainEvent=e['trigger']['event'],ru=e['text']['ru'],en=old_en[e['id']],priority=e['priority']))

scenes_data = [
 [('tish','Указатель готов. Теперь бы найти верёвку…','The sign is fixed. Now, where is that string…'),('scratchy','Тиш… Эй, Тиш! За кустами!','Tish… Hey, Tish! Behind the bushes!')],
 [('scratchy','Я бежал за блеском над деревьями и зацепился за куст.','I chased a sparkle above the trees and got caught in a bush.'),('tish','Сначала выберемся на тропу. Потом расскажешь.','Let us get onto the trail first. Then tell me all about it.')],
 [('scratchy','Слышишь? Впереди ручей. Только плиты там старые.','Hear that? There is a stream ahead. The tiles there are old though.'),('tish','Тогда посмотрим под ноги.','Then we will watch our step.')],
 [('scratchy','Ты ловко прыгаешь. А быстро умеешь?','You jump well. Can you be quick too?'),('tish','До флажка? Давай!','To the flag? You are on!')],
 [('luma','Вот вы где! По вашему звону можно рисовать карту.','There you are! I could draw a map by following that bell.'),('scratchy','Лума, теперь твоя очередь!','Jo, it is your turn now!')],
 [('bum','Я вас догнал. И перекус почти не помял.','I caught up. And the snacks are almost uncrushed.'),('scratchy','Тогда дальше вместе. Я видел, куда полетела птица.','Then let us go together. I saw which way the bird flew.')],
 [('tish','За грибной долиной что-то блестит. Это линза?','Something is shining beyond Mushroom Hollow. Is that the lens?'),('luma','Отсюда не разглядеть. Наш путь только начинается.','It is too far to tell. Our journey is only beginning.')]
]
scenes=[]
for i,lines in enumerate(scenes_data):
    sid=f'S{i:02}'
    scenes.append(dict(id=sid,title='Лесная встреча',titleEn='A woodland encounter',frames=[dict(id=sid+'.1',title='Встреча',titleEn='Encounter',
        lines=[dict(id=sid+f'.1{chr(97+j)}',speakerId=w,ru=r,en=e) for j,(w,r,e) in enumerate(lines)])]))

catalog=dict(sourceVersion='0.2',events=events,speakers=speakers,scenes=scenes)
levels=[]
for i,(ru,en) in enumerate(titles,1):
    levels.append(dict(id=f'L{i}',titleRu=ru,title=en,heroIds=heroes[i-1],startCell=4 if i==1 else 0,
        friendlyRace=i in (4,5),hazard=3 if i==3 else 0,stepMode=1 if i==1 else (0 if i>=5 else 2)))

# Recorded legal routes are consumed by simulation, checkpoint and full UI tests.
for level in levels:
    def route(minimal):
        order=list(level['heroIds'])
        if not minimal and level['friendlyRace']: order.reverse()
        actions=[]; turn=1; remaining=[1,2]
        for hero in order:
            cell=level['startCell']
            while cell<8:
                if level['stepMode']==1: step=1
                elif level['stepMode']==0: step=max(remaining) if minimal else min(remaining)
                else: step=2 if minimal else 1
                if level['hazard']==3 and cell+step==4: step=3-step
                actions.append(dict(turn=turn,hero=hero,step=step,from_=cell))
                actions[-1]['from']=actions[-1].pop('from_')
                cell+=step
                if level['stepMode']==0:
                    remaining.remove(step)
                    if not remaining: turn+=1; remaining=[1,2]
                else: turn+=1
        return actions
    level['balance']=dict(manualReplay=route(False),minimalReplay=route(True))

def save(path,data): path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
save(OUT/'narrative.json',catalog)
save(OUT/'levels.json',dict(sourceVersion='0.2',levels=levels))
loc=ROOT/'Assets/_Project/03_UI/MainMenu/Resources/Localization/battle.json'
d=json.loads(loc.read_text(encoding='utf-8-sig'))
extra=[('Scratchy','Царап'),('Choose a friend.','Выбери друга.'),('The cracked tile cannot hold you. Jump over it.','Треснувшая плита не выдержит. Перепрыгни её.')]+[(en,ru) for ru,en in titles]
for en,ru in extra:
    found=next((e for e in d['entries'] if e['en']==en),None)
    if found: found['ru']=ru
    else: d['entries'].append(dict(en=en,ru=ru,zh=''))
loc.write_text('{"entries":[\n'+',\n'.join(json.dumps(e,ensure_ascii=False,separators=(',',':')) for e in d['entries'])+'\n]}\n',encoding='utf-8')
save(ROOT/'Assets/_Project/Resources/WorldSelection/WorldTeaser.json',dict(lines=[
 dict(id='WORLD_TEASER_01',speakerId='scratchy',ru='Дзинь… А вот тот огонёк я раньше не видел.',en='Ding… I have never seen that little light before.'),
 dict(id='WORLD_TEASER_02',speakerId='luma',ru='След ведёт в грибную долину. Линза всё ещё где-то впереди.',en='The trail leads into Mushroom Hollow. The lens is still somewhere ahead.'),
 dict(id='WORLD_TEASER_03',speakerId='tish',ru='Тогда продолжим вместе.',en='Then we will carry on together.')]))
print(f'Wrote {len(events)} events, {len(scenes)} scenes, {len(speakers)} speakers and six levels.')
