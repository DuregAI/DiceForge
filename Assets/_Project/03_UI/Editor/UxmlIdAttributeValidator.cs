using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DiceForge.UI.Editor
{
    [InitializeOnLoad]
    internal static class UxmlIdAttributeValidator
    {
        private const string RootPath = "Assets";
        private static readonly Regex IdAttributeRegex = new("\\bid\\s*=\\s*\"", RegexOptions.Compiled);

        static UxmlIdAttributeValidator()
        {
            EditorApplication.delayCall += ValidateAllUxml;
        }

        [MenuItem("Tools/UI Toolkit/Validate UXML: forbid id attribute")]
        private static void ValidateAllUxml()
        {
            var guids = AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { RootPath });
            var invalidFiles = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".uxml"))
                .Where(File.Exists)
                .Where(ContainsIdAttribute)
                .ToList();

            if (invalidFiles.Count == 0)
            {
                Debug.Log("[UI Toolkit] UXML validation passed. No forbidden id attributes found.");
                return;
            }

            foreach (var file in invalidFiles)
            {
                Debug.LogError($"[UI Toolkit] Forbidden id attribute found in: {file}. Replace with name/class.");
            }
        }

        private static bool ContainsIdAttribute(string path)
        {
            var text = File.ReadAllText(path);
            return IdAttributeRegex.IsMatch(text);
        }
    }
}
