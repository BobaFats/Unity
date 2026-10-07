using UnityEditor;
using UnityEngine;

namespace Tanks2D.EditorTools
{
    // Инспектор VisualSlot: что сейчас показано и как это заменить
    [CustomEditor(typeof(VisualSlot))]
    [CanEditMultipleObjects]
    public class VisualSlotEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var slot = (VisualSlot)target;

            EditorGUILayout.HelpBox(
                "Как поставить свою картинку или модель:\n" +
                "• только этому объекту — поля «Свой спрайт» / «Своя модель» ниже (или просто перетащите спрайт в SpriteRenderer объекта Visual);\n" +
                "• всем объектам этого типа (например, всем свиньям) — запись в каталоге визуала.\n" +
                "Подпись меняется полем «Свой текст подписи» или прямо в объекте Label.",
                MessageType.Info);

            if (!string.IsNullOrEmpty(slot.CurrentSource))
            {
                EditorGUILayout.LabelField("Сейчас показано", slot.CurrentSource, EditorStyles.boldLabel);
            }

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Применить"))
                {
                    foreach (Object o in targets)
                    {
                        var s = (VisualSlot)o;
                        Undo.RegisterFullObjectHierarchyUndo(s.gameObject, "Apply Visual");
                        s.Apply();
                        EditorUtility.SetDirty(s);
                    }
                }

                if (GUILayout.Button("Открыть каталог визуала")) VerticalProjectBuilder.SelectCatalog();
            }
        }
    }

    [CustomEditor(typeof(UIVisualSlot))]
    [CanEditMultipleObjects]
    public class UIVisualSlotEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Своя иконка — поле «Свой спрайт» (или перетащите спрайт в Image). Для всех таких иконок — каталог визуала.\n" +
                "Подпись — поле «Свой текст подписи» или объект Label.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Применить"))
                {
                    foreach (Object o in targets)
                    {
                        var s = (UIVisualSlot)o;
                        Undo.RegisterFullObjectHierarchyUndo(s.gameObject, "Apply Visual");
                        s.Apply();
                        EditorUtility.SetDirty(s);
                    }
                }

                if (GUILayout.Button("Открыть каталог визуала")) VerticalProjectBuilder.SelectCatalog();
            }
        }
    }
}
