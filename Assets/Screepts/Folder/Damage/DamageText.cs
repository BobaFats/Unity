using UnityEngine;
using TMPro;

// Всплывающая цифра (урон, золото). Масштаб и размер шрифта задаются в префабе.
public class DamageText : MonoBehaviour
{
    [Header("Movement & Fade")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private Vector3 randomOffset = new Vector3(0.3f, 0f, 0f);

    private TMP_Text _text;
    private Color _startColor;
    private float _lifetime;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        if (_text != null) _startColor = _text.color;
    }

    public void Setup(int amount, Color? customColor = null)
    {
        Setup(amount.ToString(), customColor);
    }

    // Если цвет не передан — остаётся цвет из префаба
    public void Setup(string value, Color? customColor = null)
    {
        if (_text == null) return;

        _text.text = value;
        if (customColor.HasValue) _text.color = customColor.Value;
        _startColor = _text.color;
    }

    private void Start()
    {
        transform.rotation = Quaternion.identity;
        transform.position += new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(-randomOffset.y, randomOffset.y),
            0f);

        Destroy(gameObject, fadeDuration);
    }

    private void Update()
    {
        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime, Space.World);

        _lifetime += Time.deltaTime;
        float alpha = Mathf.Lerp(1f, 0f, _lifetime / fadeDuration);

        if (_text != null) _text.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);
    }
}
