using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Враг идёт сверху вниз к стене, останавливается у её верхнего края и бьёт её.
// Поддерживает эффекты стихий: горение, замедление, отбрасывание.
public class PigEnemy : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHP = 30;

    [Header("Movement Settings")]
    [Tooltip("Скорость движения вниз, единиц в секунду")]
    [SerializeField] private float speed = 2f;
    [Tooltip("Сопротивление отбрасыванию: 0 — полное, 1 — неподвижен")]
    [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0f;
    [Tooltip("Скорость, с которой враг отлетает при отбрасывании")]
    [SerializeField] private float knockbackSpeed = 8f;

    [Header("Attack Settings (Настройки атаки стены)")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRate = 1.5f;
    [Tooltip("Зазор между низом врага и верхом стены, при котором враг начинает бить")]
    [SerializeField] private float attackDistance = 0.05f;

    [Header("Rewards")]
    [Tooltip("Опыт за убийство")]
    [SerializeField] private int xpReward = 1;

    [Header("UI & Visuals")]
    [SerializeField] private Tanks2D.VisualSlot visual;
    [SerializeField] private Tanks2D.EnemyHealthBar2D healthBar;
    [SerializeField] private GameObject damageTextPrefab;

    [Header("Hit Feedback")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.08f;
    [SerializeField] private float deathFadeDuration = 0.35f;
    [SerializeField] private Color slowTint = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private Color burnTint = new Color(1f, 0.55f, 0.2f);

    private int currentHP;
    private float _nextAttackTime;
    private Tanks2D.Wall _targetWall;
    private bool isDying;
    private bool _isBoss;
    private float _healthMultiplier = 1f;
    private Color _baseColor = Color.white;
    private float _flashUntil;

    // Эффекты стихий
    private float _slowFactor;
    private float _slowUntil;
    private float _burnDps;
    private float _burnUntil;
    private float _burnAccumulated;
    private Color _burnTextColor = new Color(1f, 0.55f, 0.2f);
    private float _knockbackRemaining;

    // Все живые враги на сцене (для прицеливания башен, отскоков пуль и лимита спавна)
    private static readonly List<PigEnemy> _alive = new List<PigEnemy>();
    public static IReadOnlyList<PigEnemy> Alive => _alive;
    public static int AliveCount => _alive.Count;

    // Для способностей босса
    public event Action<PigEnemy> WallAttacked;
    public float DamageTakenMultiplier { get; set; } = 1f;
    public float AttackDamageMultiplier { get; set; } = 1f;
    public bool MovementLocked { get; set; }

    public bool IsDying => isDying;
    public bool IsBoss => _isBoss;
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsSlowed => Time.time < _slowUntil;
    public bool IsBurning => Time.time < _burnUntil;
    public float CurrentSpeed => speed * (IsSlowed ? 1f - _slowFactor : 1f);

    private float HalfHeight => visual != null ? visual.Size.y * 0.5f : 0.5f;
    private Animator CurrentAnimator => visual != null ? visual.Animator : null;

    public static PigEnemy FindNearest(Vector2 position)
    {
        PigEnemy nearest = null;
        float bestDistance = float.MaxValue;

        foreach (PigEnemy enemy in _alive)
        {
            float distance = ((Vector2)enemy.transform.position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void OnEnable()
    {
        if (!isDying && !_alive.Contains(this)) _alive.Add(this);
    }

    private void OnDisable()
    {
        _alive.Remove(this);
    }

    public void SetTargetWall(Tanks2D.Wall wall)
    {
        _targetWall = wall;
    }

    public void Initialize(bool isBoss, float healthMultiplier = 1f)
    {
        _isBoss = isBoss;
        _healthMultiplier = Mathf.Max(0.1f, healthMultiplier);
    }

    private void Start()
    {
        maxHP = Mathf.Max(1, Mathf.RoundToInt(maxHP * _healthMultiplier));
        currentHP = maxHP;
        if (visual != null && visual.Renderer != null) _baseColor = visual.Renderer.color;
        if (healthBar != null) healthBar.SetNormalized(1f);
    }

    private void Update()
    {
        if (isDying || Tanks2D.GamePause.IsPaused || Tanks2D.Wall.IsGameOver) return;

        TickBurn();
        if (isDying) return;

        UpdateTint();

        if (_knockbackRemaining > 0f)
        {
            float step = Mathf.Min(_knockbackRemaining, knockbackSpeed * Time.deltaTime);
            transform.position += Vector3.up * step;
            _knockbackRemaining -= step;
            return;
        }

        if (MovementLocked) return;

        if (_targetWall == null)
        {
            MoveDown(float.NegativeInfinity);
            return;
        }

        float stopY = _targetWall.TopY + HalfHeight + attackDistance;

        if (transform.position.y > stopY)
        {
            MoveDown(stopY);
        }
        else if (Time.time >= _nextAttackTime)
        {
            Animator animator = CurrentAnimator;
            if (animator != null) animator.SetTrigger("Attack");

            _targetWall.TakeDamage(Mathf.RoundToInt(attackDamage * AttackDamageMultiplier));
            _nextAttackTime = Time.time + attackRate;
            WallAttacked?.Invoke(this);
        }
    }

    private void MoveDown(float stopY)
    {
        Vector3 position = transform.position;
        position.y = Mathf.Max(stopY, position.y - CurrentSpeed * Time.deltaTime);
        transform.position = position;
    }

    // ---------------------------------------------------------------- Урон

    public void ApplyDamage(int damage, Color? textColor = null, bool isCrit = false)
    {
        if (isDying) return;

        bool shielded = DamageTakenMultiplier < 1f;
        damage = Mathf.Max(0, Mathf.RoundToInt(damage * DamageTakenMultiplier));
        currentHP -= damage;

        if (healthBar != null) healthBar.SetNormalized((float)currentHP / maxHP);

        string text = isCrit ? $"{damage}!" : damage.ToString();
        Color? color = shielded ? new Color(0.6f, 0.75f, 1f) : textColor;
        SpawnFloatingText(damageTextPrefab, text, color, 0f);
        _flashUntil = Time.time + hitFlashDuration;

        if (currentHP <= 0) Die();
    }

    // ---------------------------------------------------------------- Эффекты стихий

    public void ApplyBurn(float damagePerSecond, float duration, Color textColor)
    {
        if (isDying) return;

        // Новое горение не слабее текущего, длительность обновляется
        _burnDps = IsBurning ? Mathf.Max(_burnDps, damagePerSecond) : damagePerSecond;
        _burnUntil = Time.time + duration;
        _burnTextColor = textColor;
    }

    public void ApplySlow(float factor, float duration)
    {
        if (isDying) return;

        _slowFactor = IsSlowed ? Mathf.Max(_slowFactor, factor) : factor;
        _slowUntil = Time.time + duration;
    }

    public void Knockback(float distance)
    {
        if (isDying) return;
        _knockbackRemaining += distance * (1f - knockbackResistance);
    }

    private void TickBurn()
    {
        if (!IsBurning)
        {
            _burnAccumulated = 0f;
            return;
        }

        _burnAccumulated += _burnDps * Time.deltaTime;
        if (_burnAccumulated < 1f) return;

        int damage = Mathf.FloorToInt(_burnAccumulated);
        _burnAccumulated -= damage;
        ApplyDamage(damage, _burnTextColor);
    }

    private void UpdateTint()
    {
        if (visual == null || visual.Renderer == null) return;

        Color color = _baseColor;
        if (IsSlowed) color = Color.Lerp(color, slowTint, 0.6f);
        if (IsBurning) color = Color.Lerp(color, burnTint, 0.5f);
        if (Time.time < _flashUntil) color = hitFlashColor;

        visual.Renderer.color = color;
    }

    // ---------------------------------------------------------------- Смерть

    private void Die()
    {
        if (isDying) return;
        isDying = true;
        _alive.Remove(this);

        // Сначала опыт (повышения уровня), потом спавнер (за босса — награда и лобби)
        Tanks2D.ExperienceSystem.AddExperience(xpReward);

        if (Tanks2D.EnemySpawner2D.Instance != null)
        {
            Tanks2D.EnemySpawner2D.Instance.RegisterEnemyDeath(_isBoss);
        }

        if (healthBar != null) healthBar.gameObject.SetActive(false);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        StartCoroutine(DeathRoutine());
    }

    private void SpawnFloatingText(GameObject prefab, string text, Color? color, float extraOffsetY)
    {
        if (prefab == null) return;

        Vector3 spawnPosition = transform.position + new Vector3(0f, HalfHeight + 0.2f + extraOffsetY, 0f);
        GameObject textGo = Instantiate(prefab, spawnPosition, Quaternion.identity);

        DamageText floatingText = textGo.GetComponent<DamageText>();
        if (floatingText != null) floatingText.Setup(text, color);
    }

    private IEnumerator DeathRoutine()
    {
        Animator animator = CurrentAnimator;

        if (animator != null)
        {
            // Финальный арт с анимацией смерти
            animator.SetTrigger("Die");
            yield return null;
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        }
        else
        {
            // Заглушка: сжимаемся и растворяемся
            Vector3 startScale = transform.localScale;
            SpriteRenderer spriteRenderer = visual != null ? visual.Renderer : null;
            float elapsed = 0f;

            while (elapsed < deathFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / deathFadeDuration;
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
                if (spriteRenderer != null)
                {
                    Color c = _baseColor;
                    c.a = 1f - t;
                    spriteRenderer.color = c;
                }
                yield return null;
            }
        }

        Destroy(gameObject);
    }
}
