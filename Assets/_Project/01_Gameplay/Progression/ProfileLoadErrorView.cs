using UnityEngine;

namespace Diceforge.Progression
{
    public sealed class ProfileLoadErrorView : MonoBehaviour
    {
        internal static void Show()
        {
            var host = new GameObject("Profile load error");
            DontDestroyOnLoad(host);
            host.AddComponent<ProfileLoadErrorView>();
        }

        private void OnGUI()
        {
            GUI.ModalWindow(GetEntityId().GetHashCode(), new Rect((Screen.width - 520) / 2, (Screen.height - 180) / 2, 520, 180),
                _ => GUI.Label(new Rect(20, 40, 480, 120), ProfileService.LoadError + "\nФайлы сохранения оставлены без изменений."),
                "Ошибка загрузки");
        }
    }
}
