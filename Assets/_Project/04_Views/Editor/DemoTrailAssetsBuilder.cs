using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.GameModes;
using Diceforge.Presets;
using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    public static class DemoTrailAssetsBuilder
    {
        private const string Data = "Assets/_Project/05_Gameplay_Data/";
        private const string Art = Data + "Battle/DemoTrail/";

        [MenuItem("Diceforge/Demo RC/Configure levels 04-06")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Configure the demo in edit mode.");
            if (!AssetDatabase.IsValidFolder(Art.TrimEnd('/'))) AssetDatabase.CreateFolder(Data + "Battle", "DemoTrail");
            Material ochre = Material("Ochre", new Color(.8f, .52f, .16f));
            Material bark = Material("Bark", new Color(.32f, .19f, .09f));
            Material amber = Material("Amber", new Color(1, .66f, .22f));
            GameObject bum = Goblin("Bum", ochre, true);
            GameObject ryzh = Goblin("Ryzh", amber, false);
            GameObject beetle = Beetle(bark, amber);
            string[] titles = { "Workshop by the roots", "Old crossing", "Glade of shared light" };
            for (int index = 4; index <= 6; index++)
            {
                var rules = AssetDatabase.LoadAssetAtPath<RulesetPreset>($"{Data}Rulesets/Ruleset_Level_{index:D2}.asset");
                rules.gameMode = GameMode.SoloTrail;
                rules.soloTrailStepOfferMode = index == 4 ? SoloTrailStepOfferMode.Alternatives : SoloTrailStepOfferMode.Sequential;
                rules.soloTrailHazard = index == 5 ? SoloTrailHazard.Ryzh : SoloTrailHazard.Bark;
                rules.soloTrailHazardStartCell = index == 5 ? 3 : 4;
                rules.totalStonesPerPlayer = rules.maxUnitsPerSide = 3;
                rules.actionsPerTurn = index == 4 ? 1 : 2;
                rules.boardSize = 8;
                rules.homeSize = 2;
                rules.startCellA = 0;
                rules.moveDirA = 1;
                rules.dieMin = 1;
                rules.dieMax = 2;
                rules.maxTurns = 64;
                rules.allowHitSingleStone = rules.allowReroll = false;
                rules.blockIfOpponentAnyStone = true;
                rules.headRules.restrictHeadMoves = false;
                EditorUtility.SetDirty(rules);
                var bag = AssetDatabase.LoadAssetAtPath<DiceBagDefinition>($"{Data}DiceBags/DiceBag_Level_{index:D2}.asset");
                bag.drawMode = DiceBagDrawMode.Sequential;
                bag.outcomes = new List<DiceOutcomeDefinition> { new() { label = index == 4 ? "1 or 2" : "1 + 2", weight = 1, dice = new[] { 1, 2 } } };
                EditorUtility.SetDirty(bag);
                string path = $"{Data}GameModes/Demo_Level_{index:D2}.asset";
                var level = AssetDatabase.LoadAssetAtPath<DemoLevelDefinition>(path);
                if (level == null) { level = ScriptableObject.CreateInstance<DemoLevelDefinition>(); AssetDatabase.CreateAsset(level, path); }
                level.sourceVersion = "0.1";
                level.sourceId = $"docs/DemoRC/v0.1/data/levels.json#L{index}";
                level.levelId = "L" + index;
                level.title = titles[index - 4];
                level.heroIds = new[] { "tish", "luma", "bum" };
                level.bumPrefab = bum;
                level.hazardPrefab = index == 5 ? ryzh : beetle;
                level.Validate(RulesetConfig.FromPreset(rules));
                EditorUtility.SetDirty(level);
                var preset = AssetDatabase.LoadAssetAtPath<GameModePreset>($"{Data}GameModes/GM_Level_{index:D2}.asset");
                preset.demoLevel = level;
                EditorUtility.SetDirty(preset);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material Material(string name, Color color)
        {
            string path = Art + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>(Data + "Battle/WoodlandHero/HeroWood.mat");
            material = new Material(source) { name = name, color = color };
            material.SetTexture("_BaseMap", null);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Goblin(string name, Material accent, bool backpack)
        {
            string path = Art + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Data + "Battle/Diorama/Goblin_Red.prefab"));
            root.name = name;
            if (backpack)
            {
                Part(root, "SoftBackpack", PrimitiveType.Sphere, new Vector3(0, .5f, .25f), new Vector3(.48f, .5f, .25f), accent);
                Part(root, "OchreScarf", PrimitiveType.Sphere, new Vector3(0, .68f, -.2f), new Vector3(.47f, .17f, .15f), accent);
            }
            else Part(root, "OrangeTuft", PrimitiveType.Sphere, new Vector3(0, 1.06f, 0), new Vector3(.24f, .23f, .2f), accent);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Beetle(Material bark, Material amber)
        {
            string path = Art + "Bark.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var root = new GameObject("Bark");
            Part(root, "Shell", PrimitiveType.Sphere, new Vector3(0, .28f, 0), new Vector3(.7f, .48f, .65f), bark);
            Part(root, "Head", PrimitiveType.Sphere, new Vector3(0, .23f, -.3f), new Vector3(.38f, .3f, .28f), bark);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root, "AmberEye", PrimitiveType.Sphere, new Vector3(side * .12f, .3f, -.42f), Vector3.one * .09f, amber);
                for (int leg = -1; leg <= 1; leg++)
                    Part(root, "Leg", PrimitiveType.Capsule, new Vector3(side * .32f, .12f, leg * .2f), new Vector3(.08f, .12f, .08f), bark);
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
