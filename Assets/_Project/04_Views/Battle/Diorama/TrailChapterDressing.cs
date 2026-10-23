using UnityEngine;

namespace Diceforge.View
{
    /// <summary>Authored cosmetic accents for the six demo visits to the woodland trail.</summary>
    public sealed class TrailChapterDressing : MonoBehaviour
    {
        public Renderer flag;
        public Material defaultFlag;
        public Material[] chapterFlags;
        public GameObject[] chapterDetails;

        public void Apply(string levelId)
        {
            int index = -1;
            if (!string.IsNullOrEmpty(levelId) && int.TryParse(levelId.TrimStart('L', 'l'), out int level)) index = level - 1;
            if (flag != null) flag.sharedMaterial = index >= 0 && index < chapterFlags.Length ? chapterFlags[index] : defaultFlag;
            for (int i = 0; i < chapterDetails.Length; i++)
                if (chapterDetails[i] != null) chapterDetails[i].SetActive(i == index);
        }
    }
}
