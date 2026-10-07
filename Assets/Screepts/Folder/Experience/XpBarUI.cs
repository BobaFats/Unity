using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    public class XpBarUI : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _text;

        private void Update()
        {
            ExperienceSystem experience = ExperienceSystem.Instance;
            if (experience == null) return;

            if (_slider != null) _slider.value = experience.Progress;
            if (_text != null) _text.text = $"Ур. {GameStats.Level}   {GameStats.Experience} / {experience.XpToNextLevel} XP";
        }
    }
}
