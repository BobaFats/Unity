using System.Collections;
using UnityEngine;

namespace Tanks2D
{
    // Способности босса по уровню (уровень босса = номер миссии, способности накапливаются):
    //   ур.1 — периодически включает щит, почти не получая урона;
    //   ур.2 — бьёт стену усиленным тараном и откатывается назад, затем идёт снова.
    [RequireComponent(typeof(PigEnemy))]
    public class BossAbilities : MonoBehaviour
    {
        [Header("Ур. 1 — Щит")]
        [SerializeField] private GameObject _shieldVisual;
        [SerializeField] private float _firstShieldDelay = 2f;
        [SerializeField] private float _shieldInterval = 6f;
        [SerializeField] private float _shieldDuration = 3f;
        [Tooltip("Доля урона, проходящая через щит")]
        [SerializeField, Range(0f, 1f)] private float _shieldDamageMultiplier = 0.15f;

        [Header("Ур. 2 — Таран и откат")]
        [SerializeField] private float _ramDamageMultiplier = 2f;
        [SerializeField] private float _retreatDistance = 4f;
        [SerializeField] private float _retreatSpeed = 4f;

        private PigEnemy _enemy;
        private int _level = 1;
        private float _nextShieldTime;
        private float _shieldEndTime;
        private bool _shieldActive;
        private bool _retreating;

        public int Level => _level;
        public bool ShieldActive => _shieldActive;
        public bool IsRetreating => _retreating;
        public bool HasShield => _level >= 1;
        public bool HasRam => _level >= 2;

        private void Awake()
        {
            _enemy = GetComponent<PigEnemy>();
            _enemy.WallAttacked += OnWallAttacked;
            SetShield(false);
        }

        private void OnDestroy()
        {
            if (_enemy != null) _enemy.WallAttacked -= OnWallAttacked;
        }

        public void Setup(int level)
        {
            _level = Mathf.Max(1, level);
            _enemy.AttackDamageMultiplier = HasRam ? _ramDamageMultiplier : 1f;
        }

        private void Start()
        {
            _nextShieldTime = Time.time + _firstShieldDelay;
        }

        private void Update()
        {
            if (_enemy.IsDying)
            {
                SetShield(false);
                return;
            }

            if (GamePause.IsPaused || !HasShield) return;

            if (!_shieldActive && Time.time >= _nextShieldTime)
            {
                SetShield(true);
                _shieldEndTime = Time.time + _shieldDuration;
            }
            else if (_shieldActive && Time.time >= _shieldEndTime)
            {
                SetShield(false);
                _nextShieldTime = Time.time + _shieldInterval;
            }
        }

        private void SetShield(bool active)
        {
            _shieldActive = active;
            if (_enemy != null) _enemy.DamageTakenMultiplier = active ? _shieldDamageMultiplier : 1f;
            if (_shieldVisual != null) _shieldVisual.SetActive(active);
        }

        private void OnWallAttacked(PigEnemy enemy)
        {
            if (HasRam && !_retreating && !_enemy.IsDying) StartCoroutine(Retreat());
        }

        private IEnumerator Retreat()
        {
            _retreating = true;
            _enemy.MovementLocked = true;

            float targetY = transform.position.y + _retreatDistance;
            while (transform.position.y < targetY && !_enemy.IsDying)
            {
                transform.position += Vector3.up * Mathf.Min(_retreatSpeed * Time.deltaTime, targetY - transform.position.y);
                yield return null;
            }

            _enemy.MovementLocked = false;
            _retreating = false;
        }
    }
}
