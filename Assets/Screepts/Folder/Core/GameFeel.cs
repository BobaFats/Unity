using UnityEngine;

namespace Tanks2D
{
    // Ощущение от попаданий: тряска камеры, осколки при попадании и смерти, вибрация телефона.
    // Вызывается из игровых скриптов: GameFeel.Shake(...), GameFeel.Burst(...), GameFeel.Vibrate().
    // Силу эффектов можно крутить в константах ниже или выключить флагами (позже — в настройках).
    public static class GameFeel
    {
        public static bool ShakeEnabled = true;
        public static bool ParticlesEnabled = true;
        public static bool VibrationEnabled = true;

        // Готовые силы тряски: (сила в мировых единицах, длительность)
        public static readonly (float strength, float duration) ShakeWallHit = (0.04f, 0.1f);
        public static readonly (float strength, float duration) ShakeExplosion = (0.05f, 0.1f);
        public static readonly (float strength, float duration) ShakeTowerDestroyed = (0.2f, 0.3f);
        public static readonly (float strength, float duration) ShakeBossSpawn = (0.25f, 0.45f);
        public static readonly (float strength, float duration) ShakeBossDeath = (0.4f, 0.6f);

        // Удар по стене вибрирует, если снял не меньше этой доли прочности
        public const float VibrateWallShare = 0.05f;

        private const float VibrationCooldown = 0.6f;
        private static float _nextVibration;

        // Для проверок
        public static int ShakeCount { get; private set; }
        public static int BurstCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ShakeCount = 0;
            BurstCount = 0;
            _nextVibration = 0f;
        }

        public static void Shake((float strength, float duration) preset, float scale = 1f)
        {
            Shake(preset.strength * scale, preset.duration);
        }

        public static void Shake(float strength, float duration)
        {
            if (!ShakeEnabled || strength <= 0f) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            CameraShake shake = camera.GetComponent<CameraShake>();
            if (shake == null) shake = camera.gameObject.AddComponent<CameraShake>();
            shake.Add(strength, duration);
            ShakeCount++;
        }

        // Осколки: count штук разлетаются из точки и гаснут
        public static void Burst(Vector3 position, Color color, int count, float speed = 3f)
        {
            if (!ParticlesEnabled || count <= 0) return;
            HitParticles.Emit(position, color, count, speed);
            BurstCount++;
        }

        // Короткий толчок вибрации на телефоне (не чаще раза в VibrationCooldown секунд)
        public static void Vibrate()
        {
            if (!VibrationEnabled || Time.unscaledTime < _nextVibration) return;
            _nextVibration = Time.unscaledTime + VibrationCooldown;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
