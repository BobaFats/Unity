using UnityEditor;
using UnityEngine;

namespace Tanks2D.EditorTools
{
    // Инспектор анимаций: понятные названия ячеек и подсказка, когда игра их вызывает
    [CustomEditor(typeof(CharacterAnimations))]
    [CanEditMultipleObjects]
    public class CharacterAnimationsEditor : Editor
    {
        private static readonly (string field, CharacterAnimation state, string title)[] Slots =
        {
            ("_idle", CharacterAnimation.Idle, "Стоит (Idle)"),
            ("_move", CharacterAnimation.Move, "Идёт (Move)"),
            ("_attack", CharacterAnimation.Attack, "Атака (Attack)"),
            ("_hit", CharacterAnimation.Hit, "Получил урон (Hit)"),
            ("_death", CharacterAnimation.Death, "Смерть (Death)"),
            ("_special", CharacterAnimation.Special, "Особое (Special)")
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var animations = (CharacterAnimations)target;

            EditorGUILayout.HelpBox(
                "В каждую ячейку положите Animation Clip ИЛИ кадры-спрайты (Frames) с частотой кадров.\n" +
                "Пустые ячейки игра пропускает. Стоит/Идёт играют по кругу, остальные — один раз.\n" +
                "Кадры-спрайты меняют картинку объекта Visual (или первого спрайта модели). " +
                "Animation Clip проигрывается на объекте Model/Visual — записывайте его на этом объекте. " +
                "Позицию самого этого объекта клип не двигает (это root motion) — анимируйте кадры спрайта или дочерние части модели.",
                MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_visual"), new GUIContent("Внешний вид"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_speed"), new GUIContent("Общая скорость", "Множитель скорости для всех анимаций объекта"));
            EditorGUILayout.Space();

            foreach ((string field, CharacterAnimation state, string title) in Slots)
            {
                string when = Describe(animations.gameObject, state);
                SerializedProperty slot = serializedObject.FindProperty(field);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string mark = animations.Has(state) ? "●" : "○";
                    slot.isExpanded = EditorGUILayout.Foldout(slot.isExpanded, $"{mark} {title}", true, EditorStyles.foldoutHeader);
                    EditorGUILayout.LabelField(when, EditorStyles.miniLabel);

                    if (slot.isExpanded)
                    {
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("clip"), new GUIContent("Animation Clip"));
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("frames"), new GUIContent("Кадры (спрайты)"), true);
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("framesPerSecond"), new GUIContent("Кадров в секунду"));
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("speed"),
                            new GUIContent("Скорость этой анимации", "Умножается на общую скорость. 2 — вдвое быстрее, 0.5 — вдвое медленнее"));

                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Когда игра вызывает ячейку — зависит от того, на каком объекте стоит компонент
        private static string Describe(GameObject go, CharacterAnimation state)
        {
            bool enemy = go.GetComponent<PigEnemy>() != null;
            bool boss = go.GetComponent<BossAbilities>() != null;
            bool player = go.GetComponent<PlayerController2D>() != null;
            bool tower = go.GetComponent<HelperTower>() != null;
            bool wall = go.GetComponent<Wall>() != null;

            switch (state)
            {
                case CharacterAnimation.Idle:
                    return enemy ? "у стены между ударами" : "всё время по умолчанию";
                case CharacterAnimation.Move:
                    return enemy ? "пока идёт к стене (и при откате босса)" : "не используется этим объектом";
                case CharacterAnimation.Attack:
                    if (enemy) return "удар по стене";
                    if (player || tower) return "каждый выстрел";
                    return "не используется этим объектом";
                case CharacterAnimation.Hit:
                    if (enemy) return "попадание пули";
                    if (wall) return "удар врага по стене";
                    return "не используется этим объектом";
                case CharacterAnimation.Death:
                    if (enemy) return "смерть; объект исчезнет, когда анимация доиграет";
                    if (wall) return "стена разрушена (экран поражения)";
                    return "не используется этим объектом";
                case CharacterAnimation.Special:
                    if (boss) return "босс включает щит";
                    if (player) return "начало перезарядки";
                    return "не используется этим объектом";
                default:
                    return "";
            }
        }
    }
}
