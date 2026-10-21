from pathlib import Path
import json,xml.etree.ElementTree as ET,shutil
ROOT=Path('C:/Backforge/Diceforge/DiceForge')
DOC=ROOT/'docs/Art/MapDesign/Production-v01'
report=ROOT/'docs/Validation/TishReady/living-forest-beta-v02.xml'
tree=ET.parse(report).getroot()
assert int(tree.get('failed','0'))==0,tree.attrib
passed=int(tree.get('passed','0'))
p=DOC/'Sources/create_review_gallery.py';s=p.read_text(encoding='utf-8')
s=s.replace("'revision':'LivingForest v01'","'revision':'LivingForest beta v02'")
s=s.replace("'prefabs':23","'prefabs':len(assets)")
s=s.replace("'graphicsPassed':16,'campaignPassed':1",f"'graphicsPassed':{passed-1},'campaignPassed':1")
stats=json.loads((ROOT/'docs/Validation/LivingForest/static-meshes.json').read_text())
s=s.replace("'staticDressing':{'sourceRenderers':357,'combinedRenderers':25,'protectedObjects':104}",
            "'staticDressing':json.loads((folder/'../../../Validation/LivingForest/static-meshes.json').read_text())")
s=s.replace('357 неподвижных Renderer объединены в 25 групп',f"{stats['sourceRenderers']} неподвижных Renderer объединены в {stats['combinedRenderers']} группы")
s=s.replace('LivingForest/living-forest-unity.mp4','LivingForest/Beta-v02/living-forest-unity.mp4')
s=s.replace('LivingForest/close-runtime.png','LivingForest/Beta-v02/close-runtime.png')
s=s.replace('LivingForest/Campaign-Final/','LivingForest/Beta-v02/Campaign/')
s=s.replace('<h1>Живой лес</h1>','<h1>Живой лес · beta v02</h1>')
s=s.replace('<b>23</b> prefab','<b>'+str(len(json.loads((DOC/'kit-manifest.json').read_text()))+3)+'</b> prefab')
s=s.replace('<b>17</b> пройденных проверок',f'<b>{passed}</b> пройденных проверок')
s=s.replace('16 проверок графики, камеры, кадрирования и персонажей;',f'{passed-1} проверок графики, камеры, кадрирования и персонажей;')
s=s.replace('лес, тёплый камень, прозрачный ручей и движущиеся детали.','лес, тёплый камень, бурная река с пеной и объёмные ветки переднего плана.')
s=s.replace('Дно с объёмными камешками, преломление, поглощение с глубиной, два уровня отражений, мягкая пена и каустики.',
 'Асимметричный подводный берег, единое русло с перекатами и водопадом. Пена движется по течению, огибает камни и расходится неровным шлейфом в озере.')
s=s.replace('Ветер в кронах, камыше и траве. Закреплённый край флага и подвес фонаря. Поддерживается уменьшенная анимация.',
 'Ветер в кронах и камыше, объёмные дубовые ветви у камеры. Флаг и подвес фонаря собраны на одной консоли. Поддерживается уменьшенная анимация.')
insert='''<h2>Лума: исправлены поверхности волос</h2><div class="proofs" style="grid-template-columns:1fr 1fr"><figure><img src="../../../Validation/LivingForest/Beta-v02/luma-before-2.png" alt="Лума до исправления нормалей"><figcaption>До: исходные внешние полигоны волос смотрели внутрь.</figcaption></figure><figure><img src="../../../Validation/LivingForest/Beta-v02/luma-after-2.png" alt="Лума после исправления нормалей"><figcaption>После: 1180 полигонов восстановлены. Тот же непрозрачный материал с отсечением обратных граней; риг, UV и 7 лицевых форм сохранены.</figcaption></figure></div>'''
if 'Лума: исправлены поверхности волос' not in s:s=s.replace('<h2>Проверено в игре</h2>',insert+'\n<h2>Проверено в игре</h2>')
p.write_text(s,encoding='utf-8')
exec(compile(s,str(p),'exec'),{'__file__':str(p)})
shutil.copy2('C:/Backforge/Tripo3D/work/build_living_forest_kit.py',DOC/'Sources/build_living_forest_kit.py')
shutil.copy2('C:/Backforge/Tripo3D/work/luma_v03/surface-repair.json',DOC/'luma-surface-repair.json')
readme=DOC/'README_RU.md';s=readme.read_text(encoding='utf-8')
s=s.replace('производственный комплект v01','производственный комплект beta v02').replace('23 модели и 23 отдельных','28 моделей и 28 отдельных')
s=s.replace('| Дополнительные варианты |', '| Новая река | RiverSurface_A, RiverRocks_A, AsymmetricShelf_A | Непрерывное русло, камни с пенными следами, асимметричный подводный берег |\n| Передний план | ForegroundBranch_A/B | Текстурированные части утверждённого дуба, адаптивное кадрирование |\n| Дополнительные варианты |')
start=s.index('Вода использует глубину сцены')
end=s.index('\nURP включает',start)
s=s[:start]+'''Вода использует глубину сцены и непрозрачный цвет для преломления и поглощения. `RiverSurface_A` — отдельное непрерывное русло от верховья через водопад до озера, с неодинаковой шириной и мелкими стоячими волнами. UV по длине заданы в метрах; цвет вершин задаёт перекаты (R), следы камней (G) и падающую струю (B). Пена и волны движутся вниз по руслу со скоростью 0.82 м/с по UV; это художественный эффект, а не симуляция жидкости. Разорванные скопления и пузырьки заменяют сплошные белые полосы. В озере шлейф пены расширяется и уходит в сторону.

`AsymmetricShelf_A` заменяет симметричную подводную полку; дополнительные неодинаковые отмели разбивают силуэт берега. Исправлено зеркальное положение дна ручья и камешков после FBX-экспорта. Старые лента, каскад и круговой всплеск скрыты, чтобы не накладывать две воды друг на друга.

Озеро и ручей имеют отдельные плоские отражения. Для неплоского русла высота зеркала явно задана 0.045 м; на падающем участке отражение верхнего плёса выключено. Камера отражения исключает воду, UI, портреты и ближние ветки (слой 29). Отражения: 512 px / 12 обновлений в секунду на desktop; 256 px / 6 на mobile. При движении камеры кадр обновляется сразу. Скрытая камера и RenderTexture освобождаются при отключении.

`LivingForestForeground` размещает настоящие дубовые ветви в нижних углах перед рендером основной камеры. Масштаб ограничен и шириной, и высотой экрана, поэтому ветки не закрывают центральную игровую область при смене ориентации и не меняют зум карты. Глубина резкости игры сохраняется выключенной.
''' +s[end:]
s=s.replace('357 неподвижных исходных Renderer',str(stats['sourceRenderers'])+' неподвижных исходных Renderer').replace('объединены в 25 групп',f"объединены в {stats['combinedRenderers']} группы")
s=s.replace('LivingForest/living-forest-unity.mp4','LivingForest/Beta-v02/living-forest-unity.mp4')
if '## Исправление волос Лумы' not in s:s+='''
## Исправление волос Лумы

Источник: `C:/Backforge/Tripo3D/work/luma_v03/Luma_v03.blend`, экспорт `Luma_v03.fbx`. В игре сохранён стабильный путь `Incoming/Tripo/Luma_v02/Luma_v02.fbx` и его `.meta`, поэтому ссылки prefab и клипы не меняются.

`extrude_face_region(use_keep_orig=True)` перевернул 1180 исходных внешних полигонов. Исправлено только их направление; материал остался Opaque / Cull Back. `Sources/repair_luma_surface.py` сохраняет UV, веса, 7 лицевых форм и все 7 клипов. В исходном `polish_luma_v02.py` добавлено восстановление исходных нормалей после extrusion, чтобы ошибка не возвращалась при пересборке. Наряд не менялся.

Проверка: четыре ракурса Unity до/после, девять лучей в макушку с отсечением обратных граней, игровой прогон и обзор в Play Mode. Текущий отчёт — `../../../Validation/TishReady/living-forest-beta-v02.xml`.
'''
readme.write_text(s,encoding='utf-8')
print('BETA_PACKAGED',passed)
