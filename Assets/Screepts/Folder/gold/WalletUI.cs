using UnityEngine;
using TMPro;

public class WalletUI : MonoBehaviour
{
    [Header("References (Ссылки)")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [Tooltip("Формат вывода, {0} — количество золота")]
    [SerializeField] private string _format = "{0}";

    private int _shownGold = -1;

    private void Start()
    {
        if (_goldText != null) _goldText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Update()
    {
        if (_goldText == null || _shownGold == Wallet.TotalGold) return;

        _shownGold = Wallet.TotalGold;
        _goldText.text = string.Format(_format, _shownGold);
    }
}
