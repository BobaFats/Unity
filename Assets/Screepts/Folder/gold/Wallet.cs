using UnityEngine;

// Глобальный кошелёк. Больше не компонент сцены: золото не обнуляется при загрузке новой сцены,
// сброс только явный (новая игра / рестарт).
public static class Wallet
{
    public static int TotalGold { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => ResetWallet();

    public static void AddGold(int amount)
    {
        if (amount <= 0) return;
        TotalGold += amount;
        Debug.Log($"[Wallet] Золото добавлено: +{amount}. Всего в кошельке: {TotalGold}");
    }

    public static bool TrySpendGold(int amount)
    {
        if (amount <= 0) return false;

        if (TotalGold >= amount)
        {
            TotalGold -= amount;
            Debug.Log($"[Wallet] Золото списано: -{amount}. Осталось: {TotalGold}");
            return true;
        }

        Debug.Log($"[Wallet] Недостаточно золота! Нужно: {amount}, есть: {TotalGold}");
        return false;
    }

    public static void ResetWallet()
    {
        TotalGold = 0;
    }
}
