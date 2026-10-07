using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Единая пауза: магазин, выбор улучшения за уровень, Game Over.
    // Каждый источник ставит/снимает свою паузу; игра идёт, только когда пауз нет.
    public static class GamePause
    {
        private static readonly HashSet<object> _owners = new HashSet<object>();

        public static bool IsPaused => _owners.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Clear();

        public static void Set(object owner, bool paused)
        {
            if (owner == null) return;

            if (paused) _owners.Add(owner);
            else _owners.Remove(owner);

            Time.timeScale = IsPaused ? 0f : 1f;
        }

        // Перед загрузкой сцены: снимаем все паузы
        public static void Clear()
        {
            _owners.Clear();
            Time.timeScale = 1f;
        }
    }
}
