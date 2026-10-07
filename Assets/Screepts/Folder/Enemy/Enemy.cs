using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Враг идёт сверху вниз к стене, останавливается у её верхнего края и бьёт её.
public class PigEnemy : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHP = 30;

    [Header("Movement Settings")]
    [Tooltip("Скорость движения вниз, единиц в секунду")]
    [SerializeField] private float speed = 2f;

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
    [SerializeField] private GameObject goldTextPrefab;

    [Header("Hit Feedback")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.08f;
    [SerializeField] private float deathFadeDuration = 0.35f;

    private int currentHP;
    private float _nextAttackTime;
    private Tanks2D.Wall _targetWall;
    private bool isDying;
    private int _goldValue;
    private bool _isBoss;
    private Color _baseColor = Color.white;
    private Coroutine _flashRoutine;

    // Все живые враги на сцене (для прицеливания башен, отскоков пуль и лимита спавна)
    private static readonly List<PigEnemy> _alive = new List<PigEnemy>();
    public static IReadOnlyList<PigEnemy> Alive => _alive;
    public static int AliveCount => _alive.Count;

    public bool IsDying => isDying;
    public bool IsBoss => _isBoss;

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

    private float HalfHeight => visual != null ? visual.Size.y * 0.5f : 0.5f;
    private Animator CurrentAnimator => visual != null ? visual.Animator : null;

    public void SetTargetWall(Tanks2D.Wall wall)
    {
        _targetWall = wall;
    }

    public void Initialize(int goldReward, bool isBoss)
    {
        _goldValue = goldReward;
        _isBoss = isBoss;
    }

    private void Start()
    {
        currentHP = maxHP;
        if (visual != null && visual.Renderer != null) _baseColor = visual.Renderer.color;
        if (healthBar != null) healthBar.SetNormalized(1f);
    }

    private void Update()
    {
        if (isDying || Tanks2D.GamePause.IsPaused || Tanks2D.Wall.IsGameOver) return;

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

            _targetWall.TakeDamage(attackDamage);
            _nextAttackTime = Time.time + attackRate;
        }
    }

    private void MoveDown(float stopY)
    {
        Vector3 position = transform.position;
        position.y = Mathf.Max(stopY, position.y - speed * Time.deltaTime);
        transform.position = position;
    }

    public void ApplyDamage(int damage)
    {
        if (isDying) return;

        currentHP -= damage;

        if (healthBar != null) healthBar.SetNormalized((float)currentHP / maxHP);

        SpawnFloatingText(damageTextPrefab, damage.ToString(), null, 0f);
        Flash();

        if (currentHP <= 0) Die();
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;
        _alive.Remove(this);

        if (Tanks2D.EnemySpawner2D.Instance != null)
        {
            Tanks2D.EnemySpawner2D.Instance.RegisterEnemyDeath(_isBoss);
        }

        Wallet.AddGold(_goldValue);
        Tanks2D.ExperienceSystem.AddExperience(xpReward);

        if (_goldValue > 0)
        {
            SpawnFloatingText(goldTextPrefab, $"+{_goldValue}", new Color(1f, 0.84f, 0f), 0.3f);
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

    private void Flash()
    {
        if (visual == null || visual.Renderer == null || isDying) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        visual.Renderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        if (!isDying) visual.Renderer.color = _baseColor;
        _flashRoutine = null;
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
