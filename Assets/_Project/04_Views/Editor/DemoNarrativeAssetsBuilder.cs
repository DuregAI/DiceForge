using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;
using Diceforge.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    public static class DemoNarrativeAssetsBuilder
    {
        [Serializable] private sealed class Source { public string schemaVersion; public SourceEvent[] events; public SourceSpeaker[] speakers; }
        [Serializable] private sealed class SourceSpeaker { public string id, nameRu; }
        [Serializable] private sealed class SourceEvent
        {
            public string id, levelId, speakerId, kind, speakerLabelRu;
            public SourceText text; public SourceTrigger trigger; public int priority;
        }
        [Serializable] private sealed class SourceText { public string ru, en; }
        [Serializable] private sealed class SourceTrigger { public string @event, when; }

        [MenuItem("Diceforge/Demo RC/Import story and dialogue")]
        public static void Import()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Import narrative outside Play Mode.");
            const string target = "Assets/_Project/Resources/DemoRC";
            const string source = "docs/DemoRC/v0.1";
            Directory.CreateDirectory(target);
            var data = JsonUtility.FromJson<Source>(File.ReadAllText(source + "/data/dialogues.json"));
            if (data.events.Length != 138 || data.events.Select(e => e.id).Distinct().Count() != 138)
                throw new InvalidOperationException("Expected 138 unique dialogue events.");
            var catalog = AssetDatabase.LoadAssetAtPath<DemoNarrativeCatalog>(target + "/Narrative.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DemoNarrativeCatalog>();
                AssetDatabase.CreateAsset(catalog, target + "/Narrative.asset");
            }
            catalog.events = data.events.Select(e => new DemoDialogueEvent
            {
                id = e.id, levelId = e.levelId, speakerId = e.speakerId, kind = e.kind,
                domainEvent = e.trigger.@event, helpTopic = Regex.Match(e.trigger.when ?? "", @"topic=([a-z_]+)").Groups[1].Value,
                ru = e.text.ru, en = e.text.en, priority = e.priority, speakerLabelRu = e.speakerLabelRu
            }).ToArray();
            string[] englishNames = { "Tish", "Luma", "Bum", "Grandpa Koren", "Pip", "Fika", "Mira", "Una", "Shal", "Kloch", "Ryzh", "Bark" };
            string[] ids = { "tish", "luma", "bum", "koren", "pip", "fika", "mira", "una", "shal", "kloch", "ryzh", "bark" };
            catalog.speakers = data.speakers.Select(s => new DemoSpeaker { id = s.id, ru = s.nameRu,
                en = englishNames[Array.IndexOf(ids, s.id)], neutralPortrait = catalog.speakers.FirstOrDefault(p => p.id == s.id)?.neutralPortrait }).ToArray();
            var scenes = new List<DemoStoryScene>();
            string markdown = File.ReadAllText(source + "/03_LEVEL_SCRIPT_STORYBOARDS.md");
            var blocks = Regex.Matches(markdown, @"### (S\d\d)\.(\d) — ([^\r\n]+)\r?\n([\s\S]*?)(?=\r?\n### |\r?\n## |\z)");
            for (int sceneIndex = 0; sceneIndex <= 6; sceneIndex++)
            {
                string id = "S" + sceneIndex.ToString("00");
                string artName = "story-" + id + (sceneIndex == 0 || sceneIndex == 4 ? "-v2" : "") + ".png";
                string artPath = target + "/" + id + ".png";
                File.Copy(source + "/art/" + artName, artPath, true);
                AssetDatabase.ImportAsset(artPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(artPath);
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Compressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false; importer.SaveAndReimport();
                var scene = new DemoStoryScene { id = id, title = Regex.Match(markdown, @"## \d+\. " + id + @" — «([^»]+)»").Groups[1].Value,
                    art = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath) };
                scene.frames = blocks.Cast<RegexMatch>().Where(m => m.Groups[1].Value == id).Select(m =>
                {
                    string textLine = m.Groups[4].Value.Split('\n').First(l => l.Contains("**Текст"));
                    var lines = Regex.Matches(textLine, @"`(S\d\d\.\d[a-z])`, ([^:]+): «([^»]+)»").Cast<RegexMatch>().Select(l => new DemoStoryLine
                    { id = l.Groups[1].Value, speakerId = data.speakers.First(s => s.nameRu == l.Groups[2].Value ||
                        (s.id == "koren" && l.Groups[2].Value == "Корень")).id, ru = l.Groups[3].Value }).ToArray();
                    if (lines.Length == 0) throw new InvalidOperationException("Missing story lines: " + m.Groups[1].Value + m.Groups[2].Value);
                    return new DemoStoryFrame { id = id + "." + m.Groups[2].Value, title = m.Groups[3].Value, lines = lines };
                }).ToArray();
                if (scene.frames.Length != 3 || scene.art == null) throw new InvalidOperationException("Incomplete story: " + id);
                scenes.Add(scene);
            }
            catalog.scenes = scenes.ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Demo RC] Imported 138 dialogue events and seven three-frame story scenes. Missing translations use the RU master.");
        }
    }
}
