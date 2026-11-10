using UnityEngine;

namespace Tanks2D
{
    // Метка «объект из пула»: помнит свой префаб
    [DisallowMultipleComponent]
    public class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; private set; }
        public bool IsReleased { get; private set; }

        public void Init(GameObject prefab) => Prefab = prefab;
        public void MarkSpawned() => IsReleased = false;
        public void MarkReleased() => IsReleased = true;
    }
}
