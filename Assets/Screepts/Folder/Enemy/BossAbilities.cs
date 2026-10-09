using System.Collections;
using UnityEngine;

namespace Tanks2D
{
    // Способности босса / мини-босса. Какие включены — задаёт уровень кампании (LevelDefinition.bossAbilities).
    //   Shield        — щит со своей полоской прочности. Пока щит цел, весь урон уходит в него;
    //                   когда полоска кончается, щит ломается и босс снова получает урон. Затем перезарядка щита.
    //   RamWall       — удар по стене тараном с повышенным уроном и откат назад.
    //   DestroyTowers — периодически выбирает башню-помощника; она мигает красным и разрушается.
    [RequireComponent(typeof(PigEnemy))]
    public class BossAbilities : MonoBehaviour
    {
        [Header("Щит")]
        [SerializeField] private GameObject _shieldVisual;
        [Tooltip("Полоска прочности щита. Пусто — создаётся автоматически над полоской здоровья")]
        [SerializeField] private EnemyHealthBar2D _shieldBar;
        [SerializeField] private float _firstShieldDelay = 2f;
        [Tooltip("Через сколько секунд после поломки щит включается снова")]
        [SerializeField] private float _shieldCooldown = 8f;
        [Tooltip("Прочность щита в долях от максимального здоровья босса")]
        [SerializeField, Range(0.05f, 2f)] private float _shieldHealthFraction = 0.35f;
        [SerializeField] private Color _shieldBarColor = new Color(0.35f, 0.65f, 1f);

        [Header("Таран и откат")]
        [SerializeField] private float _ramDamageMultiplier = 2f;
        [SerializeField] private float _retreatDistance = 4f;
        [SerializeField] private float _retreatSpeed = 4f;

        [Header("Разрушение башен")]
        [SerializeField] private float _towerStrikeFirstDelay = 4f;
        [SerializeField] private float _towerStrikeInterval = 9f;
        [Tooltip("Сколько секунд башня мигает перед разрушением")]
        [SerializeField] private float _towerStrikeWarning = 1.2f;

        private PigEnemy _enemy;
        private BossAbility _abilities = BossAbility.Shield;
        private int _level = 1;

        private bool _shieldActive;
        private int _shieldHP;
        private int _shieldMaxHP;
        private float _nextShieldTime;

        private bool _retreating;
        private float _nextTowerStrikeTime;
        private bool _strikingTower;
        private int _towersDestroyed;

        public int Level => _level;
        public BossAbility Abilities => _abilities;
        public bool ShieldActive => _shieldActive;
        public int ShieldHP => _shieldHP;
        public int ShieldMaxHP => _shieldMaxHP;
        public bool IsRetreating => _retreating;
        public int TowersDestroyed => _towersDestroyed;

        public bool HasShield => (_abilities & BossAbility.Shield) != 0;
        public bool HasRam => (_abilities & BossAbility.RamWall) != 0;
        public bool HasDestroyTowers => (_abilities & BossAbility.DestroyTowers) != 0;

        private void Awake()
        {
            _enemy = GetComponent<PigEnemy>();
            _enemy.WallAttacked += OnWallAttacked;
            EnsureShieldBar();
            SetShieldVisible(false);
        }

        private void OnDestroy()
        {
            if (_enemy != null) _enemy.WallAttacked -= OnWallAttacked;
        }

        public void Setup(BossAbility abilities, int level)
        {
            _abilities = abilities;
            _level = Mathf.Max(1, level);
            _enemy.AttackDamageMultiplier = HasRam ? _ramDamageMultiplier : 1f;
        }

        private void Start()
        {
            _nextShieldTime = Time.time + _firstShieldDelay;
            _nextTowerStrikeTime = Time.time + _towerStrikeFirstDelay;
        }

        private void Update()
        {
            if (_enemy.IsDying)
            {
                if (_shieldActive) BreakShield();
                return;
            }

            if (GamePause.IsPaused) return;

            if (HasShield && !_shieldActive && Time.time >= _nextShieldTime) ActivateShield();

            if (HasDestroyTowers && !_strikingTower && Time.time >= _nextTowerStrikeTime && IsOnScreen())
            {
                StartCoroutine(StrikeTower());
            }
        }

        // ---------------------------------------------------------------- Щит

        private void ActivateShield()
        {
            _shieldMaxHP = Mathf.Max(1, Mathf.RoundToInt(_enemy.MaxHP * _shieldHealthFraction));
            _shieldHP = _shieldMaxHP;
            _shieldActive = true;
            _enemy.DamageAbsorber = AbsorbDamage;
            SetShieldVisible(true);
            UpdateShieldBar();

            CharacterAnimations animations = GetComponent<CharacterAnimations>();
            if (animations != null) animations.Play(CharacterAnimation.Special);
        }

        // Весь урон идёт в щит; остаток после поломки — по боссу
        private int AbsorbDamage(int damage)
        {
            if (!_shieldActive) return damage;

            int absorbed = Mathf.Min(damage, _shieldHP);
            _shieldHP -= absorbed;
            UpdateShieldBar();

            if (_shieldHP <= 0) BreakShield();
            return damage - absorbed;
        }

        private void BreakShield()
        {
            _shieldActive = false;
            _shieldHP = 0;
            if (_enemy != null) _enemy.DamageAbsorber = null;
            SetShieldVisible(false);
            _nextShieldTime = Time.time + _shieldCooldown;
        }

        private void SetShieldVisible(bool visible)
        {
            if (_shieldVisual != null) _shieldVisual.SetActive(visible);
            if (_shieldBar != null) _shieldBar.gameObject.SetActive(visible);
        }

        private void UpdateShieldBar()
        {
            if (_shieldBar != null) _shieldBar.SetNormalized(_shieldMaxHP > 0 ? (float)_shieldHP / _shieldMaxHP : 0f);
        }

        // Полоска щита над полоской здоровья
        private void EnsureShieldBar()
        {
            if (_shieldBar != null) return;

            EnemyHealthBar2D healthBar = GetComponentInChildren<EnemyHealthBar2D>(true);
            Vector3 position = healthBar != null ? healthBar.transform.localPosition + new Vector3(0f, 0.2f, 0f) : new Vector3(0f, 1.6f, 0f);

            var go = new GameObject("ShieldBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            _shieldBar = go.AddComponent<EnemyHealthBar2D>();
            _shieldBar.Build(PlaceholderSprites.Get(PlaceholderShape.Rectangle), healthBar != null ? healthBar.Size : new Vector2(2.4f, 0.14f), 44);
            _shieldBar.SetFillColor(_shieldBarColor);
        }

        // ---------------------------------------------------------------- Таран

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

        // ---------------------------------------------------------------- Разрушение башен

        private IEnumerator StrikeTower()
        {
            _strikingTower = true;

            TowerManager towers = TowerManager.Instance;
            HelperTower target = towers != null ? towers.GetRandomTower() : null;

            if (target == null)
            {
                _nextTowerStrikeTime = Time.time + 2f;
                _strikingTower = false;
                yield break;
            }

            // Предупреждение: башня мигает красным, потом рушится
            target.MarkForDestruction(_towerStrikeWarning);
            yield return new WaitForSeconds(_towerStrikeWarning);

            if (!_enemy.IsDying && target != null && towers != null)
            {
                CharacterAnimations animations = GetComponent<CharacterAnimations>();
                if (animations != null) animations.Play(CharacterAnimation.Attack);

                towers.DestroyTower(target);
                _towersDestroyed++;
            }
            else if (target != null)
            {
                target.CancelDestruction();
            }

            _nextTowerStrikeTime = Time.time + _towerStrikeInterval;
            _strikingTower = false;
        }

        // Босс уже вышел на экран (не бьёт из-за его края)
        private bool IsOnScreen()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return true;
            return transform.position.y < CameraFitter.GetVisibleWorldRect(cam).yMax;
        }
    }
}
