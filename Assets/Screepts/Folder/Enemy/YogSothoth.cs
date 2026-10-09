using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks2D
{
    // Йог-Сотот (мини-босс уровней 4+):
    //   1) быстро идёт к стене и неуязвим;
    //   2) останавливается в ~100 пикселях от стены, перед ним вырастают щупальца (уязвимые);
    //   3) атакует: сначала разрушает башни-помощники, а когда башен нет — бьёт стену с двойным уроном;
    //   4) пока живо хоть одно щупальце — неуязвим; все щупальца уничтожены — его можно убить.
    // Движением и атакой управляет этот компонент (PigEnemy только хранит здоровье и эффекты стихий).
    [RequireComponent(typeof(PigEnemy))]
    public class YogSothoth : MonoBehaviour
    {
        public enum State { Approaching, Holding, Vulnerable }

        [Header("Подход")]
        [Tooltip("На каком расстоянии от стены остановиться, в пикселях макета 1080x1920")]
        [SerializeField] private float _stopDistancePixels = 100f;
        [SerializeField] private float _referenceScreenHeight = 1920f;

        [Header("Щупальца")]
        [SerializeField] private GameObject _tentaclePrefab;
        [SerializeField, Min(1)] private int _tentacleCount = 3;
        [Tooltip("Расстояние между щупальцами по X")]
        [SerializeField] private float _tentacleSpacing = 1.1f;
        [Tooltip("Насколько ниже центра Йога (ближе к стене) появляются щупальца")]
        [SerializeField] private float _tentacleOffsetY = 0.9f;

        [Header("Атака")]
        [SerializeField] private float _attackInterval = 2f;
        [Tooltip("Сколько секунд башня мигает перед разрушением")]
        [SerializeField] private float _towerWarning = 1f;
        [SerializeField] private float _wallDamageMultiplier = 2f;

        private PigEnemy _enemy;
        private State _state = State.Approaching;
        private readonly List<PigEnemy> _tentacles = new List<PigEnemy>();
        private float _nextAttackTime;
        private bool _attackingTower;

        public State CurrentState => _state;
        public bool IsInvulnerable => _state != State.Vulnerable;
        public int TentaclesAlive
        {
            get
            {
                int count = 0;
                foreach (PigEnemy tentacle in _tentacles) if (tentacle != null && !tentacle.IsDying) count++;
                return count;
            }
        }
        public int TowersDestroyed { get; private set; }
        public int WallHits { get; private set; }
        public int LastWallDamage { get; private set; }

        // 100 пикселей макета в мировых единицах (высота поля 16 = 1920 пикселей)
        public float StopDistance
        {
            get
            {
                float fieldHeight = PlayField.Instance != null ? PlayField.Instance.Size.y : 16f;
                return _stopDistancePixels * fieldHeight / Mathf.Max(1f, _referenceScreenHeight);
            }
        }

        private void Awake()
        {
            _enemy = GetComponent<PigEnemy>();
            _enemy.MovementLocked = true;           // движением управляет Йог
            _enemy.DamageAbsorber = AbsorbAll;      // неуязвим до гибели щупалец
        }

        private void Update()
        {
            if (_enemy.IsDying)
            {
                RemoveTentacles();
                return;
            }

            if (GamePause.IsPaused || Wall.IsGameOver) return;

            Wall wall = _enemy.TargetWall != null ? _enemy.TargetWall : Wall.ActiveInstance;
            if (wall == null) return;

            switch (_state)
            {
                case State.Approaching:
                    Approach(wall);
                    break;

                case State.Holding:
                    if (TentaclesAlive == 0) BecomeVulnerable();
                    Attack(wall);
                    break;

                case State.Vulnerable:
                    Attack(wall);
                    break;
            }
        }

        private void Approach(Wall wall)
        {
            float stopY = wall.TopY + _enemy.BottomOffset + StopDistance;
            Vector3 position = transform.position;
            position.y = Mathf.Max(stopY, position.y - _enemy.CurrentSpeed * Time.deltaTime);
            transform.position = position;

            if (position.y <= stopY + 0.0001f) Arrive();
        }

        private void Arrive()
        {
            _state = State.Holding;
            _nextAttackTime = Time.time + _attackInterval * 0.5f;
            SpawnTentacles();
        }

        private void SpawnTentacles()
        {
            if (_tentaclePrefab == null)
            {
                BecomeVulnerable();
                return;
            }

            float firstX = -(_tentacleCount - 1) * _tentacleSpacing * 0.5f;
            for (int i = 0; i < _tentacleCount; i++)
            {
                var position = transform.position + new Vector3(firstX + i * _tentacleSpacing, -_tentacleOffsetY, 0f);
                GameObject go = Instantiate(_tentaclePrefab, position, Quaternion.identity);
                go.name = $"Tentacle_{i + 1}";

                PigEnemy tentacle = go.GetComponent<PigEnemy>();
                if (tentacle == null) continue;

                tentacle.Initialize(false, _enemy.Difficulty);
                tentacle.MovementLocked = true; // щупальца стоят на месте
                _tentacles.Add(tentacle);
            }
        }

        private void BecomeVulnerable()
        {
            _state = State.Vulnerable;
            _enemy.DamageAbsorber = null;
        }

        private void Attack(Wall wall)
        {
            if (_attackingTower || Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + _attackInterval;

            CharacterAnimations animations = GetComponent<CharacterAnimations>();
            if (animations != null) animations.Play(CharacterAnimation.Attack);

            // Сначала башни, потом стена
            TowerManager towers = TowerManager.Instance;
            HelperTower target = towers != null ? towers.GetRandomTower() : null;
            if (target != null)
            {
                StartCoroutine(DestroyTower(towers, target));
                return;
            }

            LastWallDamage = Mathf.RoundToInt(_enemy.AttackDamage * _wallDamageMultiplier);
            wall.TakeDamage(LastWallDamage);
            WallHits++;
        }

        private IEnumerator DestroyTower(TowerManager towers, HelperTower target)
        {
            _attackingTower = true;
            target.MarkForDestruction(_towerWarning);
            yield return new WaitForSeconds(_towerWarning);

            if (!_enemy.IsDying && target != null && towers != null)
            {
                towers.DestroyTower(target);
                TowersDestroyed++;
            }
            else if (target != null)
            {
                target.CancelDestruction();
            }

            _attackingTower = false;
        }

        // Пока Йог неуязвим, весь урон поглощается
        private int AbsorbAll(int damage) => 0;

        // Йог погиб — щупальца исчезают вместе с ним
        private void RemoveTentacles()
        {
            foreach (PigEnemy tentacle in _tentacles)
            {
                if (tentacle != null && !tentacle.IsDying) tentacle.Dismiss();
            }
            _tentacles.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Wall wall = Object.FindAnyObjectByType<Wall>();
            if (wall == null) return;

            Gizmos.color = new Color(0.6f, 0.3f, 1f);
            float y = wall.TopY + StopDistance;
            Gizmos.DrawLine(new Vector3(-5f, y, 0f), new Vector3(5f, y, 0f));
        }
    }
}
