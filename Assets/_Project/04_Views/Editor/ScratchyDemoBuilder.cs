using System;
using System.IO;
using System.Linq;
using Diceforge.Core;
using Diceforge.Dialogue;
using Diceforge.GameModes;
using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    public static class ScratchyDemoBuilder
    {
        private const string Data = "Assets/_Project/05_Gameplay_Data/";
        private const string Resource = "Assets/_Project/Resources/DemoRC/";
        private const string Art = "Assets/_Project/07_Art/Characters/Scratchy/";
        [Serializable] private sealed class NarrativeSource
        {
            public string sourceVersion;
            public DemoDialogueEvent[] events;
            public DemoSpeaker[] speakers;
            public DemoStoryScene[] scenes;
        }
        [Serializable] private sealed class Levels { public string sourceVersion; public Level[] levels; }
        [Serializable] private sealed class Level
        {
            public string id, title, titleRu;
            public string[] heroIds;
            public int startCell, stepMode, hazard;
            public bool friendlyRace;
        }

        [MenuItem("Diceforge/Demo RC/Apply Scratchy demo revision")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing the demo.");
            AssetDatabase.Refresh();
            var narrative = AssetDatabase.LoadAssetAtPath<DemoNarrativeCatalog>(Resource + "Narrative.asset");
            var source = JsonUtility.FromJson<NarrativeSource>(File.ReadAllText("docs/DemoRC/v0.2/narrative.json"));
            if (source.sourceVersion != "0.2" || source.events.Select(e => e.id).Distinct().Count() != source.events.Length)
                throw new InvalidOperationException("Invalid or duplicate narrative IDs.");
            foreach (var s in source.speakers)
            {
                var previous = narrative.speakers.FirstOrDefault(p => p.id == s.id);
                s.neutralPortrait = previous?.neutralPortrait;
                s.dialogueProfile = previous?.dialogueProfile;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "scratchy-profile.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100; importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "scratchy-profile.png");
            var scratchy = source.speakers.First(s => s.id == "scratchy");
            scratchy.neutralPortrait = scratchy.dialogueProfile = sprite;
            narrative.events = source.events; narrative.scenes = source.scenes;
            narrative.speakers = source.speakers; narrative.sourceVersion = source.sourceVersion;
            EditorUtility.SetDirty(narrative);

            var characters = AssetDatabase.LoadAssetAtPath<DemoCharacterCatalog>(Resource + "Characters.asset");
            bool newCatalog = characters == null;
            if (newCatalog)
            {
                characters = ScriptableObject.CreateInstance<DemoCharacterCatalog>();
                AssetDatabase.CreateAsset(characters, Resource + "Characters.asset");
            }
            string[] ids = { "tish", "luma", "bum", "scratchy" };
            string[] paths = { "Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab",
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab",
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Bum.prefab", Art + "ScratchyStandee.prefab" };
            MakeStandee(sprite, paths[3]);
            var previousCharacters = characters.characters;
            characters.characters = ids.Select((id, index) =>
            {
                // A later approved Tripo import survives reimport of the narrative.
                var existing = Array.Find(previousCharacters, c => c.id == id);
                var speaker = source.speakers.First(s => s.id == id);
                var entry = existing ?? new DemoCharacterDefinition { id = id,
                    gameplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]),
                    artNotes = id == "scratchy" ? "Temporary 2D standee. Replace with reviewed rigged Tripo export."
                        : "Candidate; check identity against the canonical cast before production approval." };
                entry.nameRu = speaker.ru; entry.nameEn = speaker.en; entry.dialogueProfile = speaker.dialogueProfile;
                entry.identityColor = index == 0 ? new Color(.81f,.34f,.25f) : index == 1 ? new Color(.23f,.57f,.53f)
                    : index == 2 ? new Color(.78f,.54f,.22f) : new Color(.58f,.40f,.27f);
                if (entry.gameplayPrefab == null) throw new InvalidOperationException("Missing character prefab: " + id);
                return entry;
            }).ToArray();
            EditorUtility.SetDirty(characters);
            var cracked = MakeCrackedTile();
            var levels = JsonUtility.FromJson<Levels>(File.ReadAllText("docs/DemoRC/v0.2/levels.json"));
            for (int i = 0; i < levels.levels.Length; i++)
            {
                var item = levels.levels[i]; string suffix = (i + 1).ToString("00");
                var mode = AssetDatabase.LoadAssetAtPath<GameModePreset>(Data + "GameModes/GM_Level_" + suffix + ".asset");
                var level = mode.demoLevel; var rules = mode.rulesetPreset;
                level.sourceVersion = "0.2"; level.sourceId = "docs/DemoRC/v0.2/levels.json#" + item.id;
                level.title = item.title; level.heroIds = item.heroIds; level.characters = characters;
                level.friendlyRace = item.friendlyRace; level.hazardPrefab = item.hazard == 3 ? cracked : null;
                rules.totalStonesPerPlayer = rules.maxUnitsPerSide = item.heroIds.Length;
                rules.soloTrailStepOfferMode = (SoloTrailStepOfferMode)item.stepMode;
                rules.actionsPerTurn = item.stepMode == 0 ? 2 : 1;
                rules.soloTrailHazard = (SoloTrailHazard)item.hazard; rules.soloTrailHazardStartCell = 4;
                rules.maxTurns = 60;
                mode.setupPreset.unitPlacements.Clear();
                mode.setupPreset.unitPlacements.Add(new UnitPlacement { player = PlayerId.A, cellIndex = item.startCell, count = item.heroIds.Length });
                level.Validate(RulesetConfig.FromPreset(rules));
                EditorUtility.SetDirty(level); EditorUtility.SetDirty(rules); EditorUtility.SetDirty(mode.setupPreset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Scratchy demo] Six levels, thirteen speakers, race branches and central character catalog imported.");
        }

        private static void MakeStandee(Sprite sprite, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var root = new GameObject("Scratchy_Prototype");
            try
            {
                root.AddComponent<BoardLayoutTokenMover>();
                var model = new GameObject("Model"); model.transform.SetParent(root.transform, false);
                var renderer = model.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                float scale = 1.15f / sprite.bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = Vector3.up * .58f;
                model.AddComponent<CharacterStandee>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject MakeCrackedTile()
        {
            const string path = Art + "CrackedTile.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(.22f,.12f,.065f));
            AssetDatabase.CreateAsset(material, Art + "Crack.mat");
            var root = new GameObject("CrackedTile");
            try
            {
                // Raised jagged seam keeps the underlying board and its selection geometry intact.
                for (int i = 0; i < 2; i++)
                {
                    var line = new GameObject("Crack" + i).AddComponent<LineRenderer>();
                    line.transform.SetParent(root.transform, false); line.useWorldSpace = false;
                    line.sharedMaterial = material; line.widthMultiplier = .065f; line.positionCount = 4;
                    line.SetPositions(i == 0 ? new[] { new Vector3(-.38f,.07f,-.3f), new Vector3(-.08f,.07f,-.08f), new Vector3(-.19f,.07f,.14f), new Vector3(.34f,.07f,.34f) }
                        : new[] { new Vector3(-.09f,.075f,-.07f), new Vector3(.1f,.075f,-.18f), new Vector3(.22f,.075f,-.15f), new Vector3(.36f,.075f,-.3f) });
                }
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return prefab;
        }
    }
}
