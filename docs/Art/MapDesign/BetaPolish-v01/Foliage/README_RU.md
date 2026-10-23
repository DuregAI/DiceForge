# Округлая листва для beta polish

Три самостоятельных варианта с крупными округлыми листьями, плавной чашеобразной
поверхностью и жилками существующей `FoliageStudy/LeafAlbedo.png`. Используются
непрозрачные контурные меши, без alpha-карт и без дополнительных текстур.

| Вариант | Назначение | Листьев LOD0 / LOD1 | Треугольников LOD0 / LOD1 | Размеры LOD0, м (ширина × глубина × высота) |
| --- | --- | --- | --- | --- |
| RoundleafShrub_A | Высокий плотный округлый куст | 81 / 60 | 3384 / 900 | 1.25 × 1.21 × 1.01 |
| RoundleafShrub_B | Низкий широкий асимметричный куст | 72 / 53 | 3008 / 796 | 1.58 × 1.36 × 0.71 |
| ShoreBroadleaf_A | Крупнолистные прибрежные веера | 63 / 47 | 2632 / 704 | 1.32 × 1.28 × 0.59 |

FBX находятся в `Assets/_Project/07_Art/BetaPolish/Foliage/`. Каждый содержит
два меша `<имя>_LOD0` и `<имя>_LOD1`; в каждом два слота: листья, затем кора.
Происхождение — у земли, масштаб — метры, FBX экспортирован Y-up / -Z-forward.

В Unity выполнить `Diceforge.View.Editor.BetaFoliageSetup.Build()` или меню
`Diceforge > Woodland > Import beta foliage kit`. Helper находится в
`Assets/_Project/07_Art/BetaPolish/Foliage/Editor/BetaFoliageSetup.cs`.
Он создаёт только собственные `Materials/RoundedLeaves.mat`, `ShoreLeaves.mat`,
`RoundedBark.mat` и три одноимённых prefab в новом каталоге. Сцены и доски
не изменяет. LODGroup использует пороги .06 / .006, без fade. Первый LOD
сохраняется для основных кустов, второй — для мелких и удалённых экземпляров.

Листья используют `Diceforge/Diorama/Soft Foliage`, `_AutoHeight=0`,
`_ToonStrength=.20`, `_Wrap=.12`, `_AmbientStrength=.8`, `_Sheen=.025`,
`_Translucency=.12`. R vertex color — плавный вес изгиба по высоте; G —
вариация пигмента каждого листа; B=.5, A=1. Числовые маски экспортируются
как **LINEAR**, без преобразования sRGB. Совместимы с существующим
`LivingWind.hlsl` и его `_ForestMotion` для reduced motion.

Исходник генератора, `.blend`, манифест и рендеры находятся в этом каталоге.
`BetaFoliage.blend` содержит обзорную постановку трёх отдельных моделей;
экспорт выполняется до их размещения на обзорной площадке.

Перегенерация из корня проекта (PowerShell):

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python docs/Art/MapDesign/BetaPolish-v01/Foliage/build_foliage_beta.py
```

Проверка готовых FBX:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python docs/Art/MapDesign/BetaPolish-v01/Foliage/validate_foliage_beta.py
```

`foliage-validation.json` подтверждает совпадение треугольников, наличие UV,
нормалей, двух material slots, сохранение диапазона ветровой маски и единичного
масштаба; вырожденных треугольников нет. Unity helper дополнительно проверяет
атрибуты и точное число треугольников после собственного импорта Unity.
Галереи LOD0/LOD1 визуально проверены в Blender Cycles. Финальную цветовую
подстройку следует оценивать в освещении игровой диорамы.
