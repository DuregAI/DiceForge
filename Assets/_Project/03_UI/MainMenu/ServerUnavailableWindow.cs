using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.MainMenu
{
    internal sealed class ServerUnavailableWindow : IDisposable
    {
        private GameObject windowObject;
        private PanelSettings panelSettings;
        private Button close;

        internal ServerUnavailableWindow(GameObject owner, int attempts)
        {
            panelSettings = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("WoodlandPanel"));
            panelSettings.sortingOrder = 1000;
            windowObject = new GameObject("Server unavailable window");
            windowObject.transform.SetParent(owner.transform, false);
            var document = windowObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("ServerUnavailableWindow");
            var root = document.rootVisualElement;
            var font = Resources.Load<Font>("Localization/Nunito");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            bool russian = PlayerPrefs.GetString("ui.language", "en") == "ru";
            root.Q<Label>("serverUnavailableTitle").text = russian ? "Сервер недоступен" : "Server unavailable";
            root.Q<Label>("serverUnavailableMessage").text = russian
                ? $"Не удалось подключиться после {attempts} попыток. Повторные подключения остановлены.\n\nПрогресс сохраняется на этом устройстве. Отправка сохранений и статистики на сервер недоступна до следующего запуска игры."
                : $"Could not connect after {attempts} attempts. Connection attempts have stopped.\n\nProgress is saved on this device. Uploading progress and statistics is unavailable until the next game launch.";
            close = root.Q<Button>("serverUnavailableClose");
            close.text = russian ? "Продолжить игру" : "Continue playing";
            close.clicked += Dispose;
            close.Focus();
        }

        public void Dispose()
        {
            if (close != null) close.clicked -= Dispose;
            close = null;
            if (windowObject != null)
            {
                windowObject.SetActive(false);
                UnityEngine.Object.Destroy(windowObject);
            }
            if (panelSettings != null) UnityEngine.Object.Destroy(panelSettings);
            windowObject = null;
            panelSettings = null;
        }
    }
}
