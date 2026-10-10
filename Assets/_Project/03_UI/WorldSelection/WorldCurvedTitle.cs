using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    internal sealed class WorldCurvedTitle : VisualElement
    {
        private readonly List<Label> letters = new();
        private readonly Label measure = new();
        private string value;
        private float requestedSize = 48;

        internal WorldCurvedTitle()
        {
            name = "worldCurvedTitle";
            AddToClassList("world-curved-title");
            pickingMode = PickingMode.Ignore;
            measure.style.position = Position.Absolute;
            measure.style.visibility = Visibility.Hidden;
            measure.style.unityFontDefinition = FontDefinition.FromFont(Resources.Load<Font>("WorldSelection/GlimbleRoundBlack"));
            Add(measure);
            RegisterCallback<GeometryChangedEvent>(_ => Arrange());
        }

        internal void SetText(string text)
        {
            if (value == text) return;
            value = text;
            tooltip = text;
            foreach (var letter in letters) letter.RemoveFromHierarchy();
            letters.Clear();
            foreach (char character in text)
            {
                var label = new Label(character.ToString()) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("world-title-letter");
                Add(label); letters.Add(label);
            }
            Arrange();
        }

        internal void SetSize(float size)
        {
            requestedSize = size;
            Arrange();
        }

        private float Width(string text) =>
            measure.MeasureTextSize(text + "|", 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x -
            measure.MeasureTextSize("|", 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;

        private void Arrange()
        {
            float available = contentRect.width;
            if (panel == null || available < 1 || string.IsNullOrEmpty(value)) return;
            measure.style.fontSize = requestedSize;
            float total = Width(value);
            if (total <= 0) return;
            float fontSize = requestedSize * Mathf.Min(1, (available - 16) / total);
            measure.style.fontSize = fontSize;
            total = Width(value);
            float arch = Mathf.Min(16, fontSize * .34f);
            float left = (available - total) * .5f;
            float previous = 0;
            for (int index = 0; index < letters.Count; index++)
            {
                float end = Width(value.Substring(0, index + 1));
                float width = end - previous;
                float center = previous + width * .5f;
                float u = (center - total * .5f) / (total * .5f);
                var letter = letters[index];
                letter.style.fontSize = fontSize;
                letter.style.left = left + previous - 3;
                letter.style.top = arch * u * u;
                letter.style.width = width + 6;
                letter.style.height = fontSize * 1.5f;
                letter.style.rotate = new Rotate(Angle.Radians(Mathf.Atan(4 * arch * u / total)));
                previous = end;
            }
            style.height = fontSize * 1.42f + arch;
        }
    }
}
