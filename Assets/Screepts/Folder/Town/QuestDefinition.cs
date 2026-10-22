using UnityEngine;

namespace Tanks2D
{
    // Задание квестгивера. Пока только заготовка: система квестов будет строиться на этих ассетах.
    [CreateAssetMenu(fileName = "Quest", menuName = "Shooter/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        [Tooltip("Уникальный ключ задания. Пусто — имя ассета")]
        [SerializeField] private string _id;
        public string title = "Задание";
        [TextArea] public string description;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
    }
}
