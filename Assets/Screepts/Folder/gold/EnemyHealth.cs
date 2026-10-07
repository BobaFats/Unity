using UnityEngine;

namespace Tanks2D
{
    public class EnemyHealth : MonoBehaviour
    {
        private int _goldValue;
        private bool _isDead = false;

        public void Initialize(int goldReward)
        {
            _goldValue = goldReward;
        }

        // Этот метод теперь будет вызываться СТРОГО при попадании пули
        public void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // Железобетонно начисляем золото за уничтожение
            Wallet.AddGold(_goldValue);

            // Удаляем танк с экрана
            Destroy(gameObject);
        }
    }
}
