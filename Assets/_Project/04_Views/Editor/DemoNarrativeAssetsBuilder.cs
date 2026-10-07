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
        [Serializable] private sealed class EnglishSource
        {
            public string language, sourceVersion;
            public EnglishSpeaker[] speakers;
            public EnglishEvent[] events;
            public EnglishTitle[] scenes, frames;
            public EnglishLine[] storyLines;
        }
        [Serializable] private sealed class EnglishSpeaker { public string id, name; }
        [Serializable] private sealed class EnglishEvent { public string id, en, speakerLabel; }
        [Serializable] private sealed class EnglishTitle { public string id, title; }
        [Serializable] private sealed class EnglishLine { public string id, en; }

        [MenuItem("Diceforge/Demo RC/Import story and dialogue")]
        public static void Import()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Import narrative outside Play Mode.");
            const string target = "Assets/_Project/Resources/DemoRC";
            const string source = "docs/DemoRC/v0.1";
            var data = JsonUtility.FromJson<Source>(File.ReadAllText(source + "/data/dialogues.json"));
            if (data.events.Length != 138 || data.events.Select(e => e.id).Distinct().Count() != 138)
                throw new InvalidOperationException("Expected 138 unique dialogue events.");
            var english = JsonUtility.FromJson<EnglishSource>(File.ReadAllText(source + "/data/dialogues.en.json"));
            if (english.language != "en" || english.sourceVersion != data.schemaVersion)
                throw new InvalidOperationException("English narrative language or source version does not match the Russian master.");
            var eventTranslations = IndexById(english.events, e => e.id, "English dialogue events");
            var speakerTranslations = IndexById(english.speakers, s => s.id, "English speakers");
            ValidateIds(data.events.Select(e => e.id), eventTranslations.Keys, "dialogue events");
            ValidateIds(data.speakers.Select(s => s.id), speakerTranslations.Keys, "speakers");
            var events = data.events.Select(e => new DemoDialogueEvent
            {
                id = e.id, levelId = e.levelId, speakerId = e.speakerId, kind = e.kind,
                domainEvent = e.trigger.@event, helpTopic = Regex.Match(e.trigger.when ?? "", @"topic=([a-z_]+)").Groups[1].Value,
                ru = RequireText(e.text.ru, e.id + " RU"), en = RequireText(eventTranslations[e.id].en, e.id + " EN"),
                priority = e.priority, speakerLabelRu = e.speakerLabelRu,
                speakerLabelEn = string.IsNullOrWhiteSpace(e.speakerLabelRu) ? string.Empty :
                    RequireText(eventTranslations[e.id].speakerLabel, e.id + " EN speaker label")
            }).ToArray();
            foreach (var speaker in english.speakers) RequireText(speaker.name, speaker.id + " EN name");
            var scenes = ReadScenes(File.ReadAllText(source + "/03_LEVEL_SCRIPT_STORYBOARDS.md"), data, english);
            Directory.CreateDirectory(target);
            var catalog = AssetDatabase.LoadAssetAtPath<DemoNarrativeCatalog>(target + "/Narrative.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DemoNarrativeCatalog>();
                AssetDatabase.CreateAsset(catalog, target + "/Narrative.asset");
            }
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
                scenes[sceneIndex].art = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
                if (scenes[sceneIndex].art == null) throw new InvalidOperationException("Missing story art: " + id);
            }
            catalog.sourceVersion = data.schemaVersion;
            catalog.events = events;
            catalog.speakers = data.speakers.Select(s => new DemoSpeaker
            {
                id = s.id, ru = s.nameRu, en = speakerTranslations[s.id].name,
                neutralPortrait = catalog.speakers?.FirstOrDefault(p => p.id == s.id)?.neutralPortrait,
                dialogueProfile = catalog.speakers?.FirstOrDefault(p => p.id == s.id)?.dialogueProfile
            }).ToArray();
            catalog.scenes = scenes;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Demo RC] Imported complete RU/EN narrative: 138 dialogue events, 35 story lines, 21 frames, seven scenes and 12 speakers.");
        }

        private static DemoStoryScene[] ReadScenes(string markdown, Source data, EnglishSource english)
        {
            var sceneTranslations = IndexById(english.scenes, s => s.id, "English scene titles");
            var frameTranslations = IndexById(english.frames, f => f.id, "English frame titles");
            var lineTranslations = IndexById(english.storyLines, l => l.id, "English story lines");
            var blocks = Regex.Matches(markdown, @"### (S\d\d)\.(\d) — ([^\r\n]+)\r?\n([\s\S]*?)(?=\r?\n### |\r?\n## |\z)");
            var scenes = new List<DemoStoryScene>();
            for (int sceneIndex = 0; sceneIndex <= 6; sceneIndex++)
            {
                string id = "S" + sceneIndex.ToString("00");
                if (!sceneTranslations.TryGetValue(id, out var translatedScene))
                    throw new InvalidOperationException("Missing English scene: " + id);
                var scene = new DemoStoryScene
                {
                    id = id,
                    title = RequireText(Regex.Match(markdown, @"## \d+\. " + id + @" — «([^»]+)»").Groups[1].Value, id + " RU title"),
                    titleEn = RequireText(translatedScene.title, id + " EN title")
                };
                scene.frames = blocks.Cast<RegexMatch>().Where(m => m.Groups[1].Value == id).Select(m =>
                {
                    string frameId = id + "." + m.Groups[2].Value;
                    if (!frameTranslations.TryGetValue(frameId, out var translatedFrame))
                        throw new InvalidOperationException("Missing English frame: " + frameId);
                    string textLine = m.Groups[4].Value.Split('\n').First(l => l.Contains("**Текст"));
                    var lines = Regex.Matches(textLine, @"`(S\d\d\.\d[a-z])`, ([^:]+): «([^»]+)»").Cast<RegexMatch>().Select(l =>
                    {
                        string lineId = l.Groups[1].Value;
                        if (!lineTranslations.TryGetValue(lineId, out var translatedLine))
                            throw new InvalidOperationException("Missing English story line: " + lineId);
                        return new DemoStoryLine
                        {
                            id = lineId,
                            speakerId = data.speakers.First(s => s.nameRu == l.Groups[2].Value ||
                                (s.id == "koren" && l.Groups[2].Value == "Корень")).id,
                            ru = RequireText(l.Groups[3].Value, lineId + " RU"),
                            en = RequireText(translatedLine.en, lineId + " EN")
                        };
                    }).ToArray();
                    if (lines.Length == 0) throw new InvalidOperationException("Missing story lines: " + frameId);
                    return new DemoStoryFrame
                    {
                        id = frameId, title = m.Groups[3].Value,
                        titleEn = RequireText(translatedFrame.title, frameId + " EN title"), lines = lines
                    };
                }).ToArray();
                if (scene.frames.Length != 3) throw new InvalidOperationException("Expected three frames: " + id);
                scenes.Add(scene);
            }
            ValidateIds(scenes.Select(s => s.id), sceneTranslations.Keys, "story scenes");
            ValidateIds(scenes.SelectMany(s => s.frames).Select(f => f.id), frameTranslations.Keys, "story frames");
            var lineIds = scenes.SelectMany(s => s.frames).SelectMany(f => f.lines).Select(l => l.id).ToArray();
            if (lineIds.Length != 35) throw new InvalidOperationException("Expected 35 story lines.");
            ValidateIds(lineIds, lineTranslations.Keys, "story lines");
            return scenes.ToArray();
        }

        private static Dictionary<string, T> IndexById<T>(IEnumerable<T> values, Func<T, string> id, string context) where T : class
        {
            if (values == null) throw new InvalidOperationException("Missing " + context + ".");
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value == null) throw new InvalidOperationException("Null entry in " + context + ".");
                string key = RequireText(id(value), context + " ID");
                if (result.ContainsKey(key)) throw new InvalidOperationException("Duplicate " + context + " ID: " + key);
                result.Add(key, value);
            }
            return result;
        }

        private static void ValidateIds(IEnumerable<string> sourceIds, IEnumerable<string> translatedIds, string context)
        {
            var expected = new HashSet<string>(sourceIds, StringComparer.Ordinal);
            if (!expected.SetEquals(translatedIds))
                throw new InvalidOperationException("English IDs do not match the Russian master: " + context + ".");
        }

        private static string RequireText(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing text: " + context);
            return value;
        }
    }
}
