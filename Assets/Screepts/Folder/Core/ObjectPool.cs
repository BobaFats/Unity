using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks2D
{
    // Объект из пула: вызывается перед тем, как он снова появится, и когда уходит обратно в пул
    public interface IPoolable
    {
        // Объект ещё выключен, позиция уже выставлена: сбросить состояние прошлой жизни
        void OnSpawnedFromPool();
        void OnReturnedToPool();
    }

    // Пул объектов: пули, всплывающие цифры, обычные враги не создаются и не удаляются заново,
    // а выключаются и переиспользуются. На телефоне это убирает подтормаживания от сборщика мусора.
    // Объекты живут в сцене: при смене сцены пул очищается сам.
    public static class ObjectPool
    {
        private static readonly Dictionary<GameObject, Stack<GameObject>> _free = new Dictionary<GameObject, Stack<GameObject>>();
        private static readonly List<IPoolable> _buffer = new List<IPoolable>();

        // Для проверок: сколько объектов создано и сколько раз объект взят из пула повторно
        public static int CreatedCount { get; private set; }
        public static int ReusedCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _free.Clear();
            CreatedCount = 0;
            ReusedCount = 0;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // Объекты выгруженной сцены уже уничтожены — пустые ссылки не храним
        private static void OnSceneUnloaded(Scene scene) => _free.Clear();

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            if (_free.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    if (go == null) continue; // уничтожен кем-то снаружи

                    if (parent != null) go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(position, rotation);
                    go.GetComponent<PooledObject>().MarkSpawned();

                    go.GetComponentsInChildren(true, _buffer);
                    foreach (IPoolable poolable in _buffer) poolable.OnSpawnedFromPool();

                    go.SetActive(true);
                    ReusedCount++;
                    return go;
                }
            }

            GameObject created = Object.Instantiate(prefab, position, rotation, parent);
            created.AddComponent<PooledObject>().Init(prefab);
            CreatedCount++;
            return created;
        }

        // Вернуть объект в пул. Объект не из пула просто уничтожается
        public static void Release(GameObject go)
        {
            if (go == null) return;

            PooledObject pooled = go.GetComponent<PooledObject>();
            if (pooled == null)
            {
                Object.Destroy(go);
                return;
            }
            if (pooled.IsReleased) return;

            pooled.MarkReleased();
            go.GetComponentsInChildren(true, _buffer);
            foreach (IPoolable poolable in _buffer) poolable.OnReturnedToPool();
            go.SetActive(false);

            if (!_free.TryGetValue(pooled.Prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _free[pooled.Prefab] = stack;
            }
            stack.Push(go);
        }

        public static int FreeCount(GameObject prefab)
        {
            return prefab != null && _free.TryGetValue(prefab, out Stack<GameObject> stack) ? stack.Count : 0;
        }
    }
}
