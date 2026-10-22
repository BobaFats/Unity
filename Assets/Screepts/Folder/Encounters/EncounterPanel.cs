using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks2D
{
    // Окно встречи в стиле Hand of Fate:
    //   1) карта события с текстом и вариантами;
    //   2) у рискованного варианта — карты удачи рубашкой вверх, игрок тянет одну;
    //   3) итог с описанием последствий.
    // Игра стоит на паузе, пока окно открыто.
    public class EncounterPanel : MonoBehaviour
    {
        public enum Stage { Closed, Choosing, Chance, Result }

        [SerializeField] private GameObject _root;

        [Header("Карта события")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Image _art;
        [SerializeField] private Transform _choicesContainer;
        [SerializeField] private Button _choiceTemplate;

        [Header("Карты удачи")]
        [SerializeField] private GameObject _chanceRoot;
        [SerializeField] private Transform _chanceContainer;
        [SerializeField] private Button _chanceCardTemplate;
        [SerializeField] private Color _cardBackColor = new Color(0.3f, 0.2f, 0.45f);

        [Header("Итог")]
        [SerializeField] private GameObject _resultRoot;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _continueButton;

        private readonly List<Button> _spawned = new List<Button>();
        private readonly List<ChanceCard> _chanceDeck = new List<ChanceCard>();
        private EncounterDefinition _encounter;
        private EncounterChoice _choice;
        private Action _onDone;

        public Stage CurrentStage { get; private set; } = Stage.Closed;
        public EncounterDefinition Current => _encounter;
        public bool IsOpen => CurrentStage != Stage.Closed;
        public int ChanceCardCount => _chanceDeck.Count;
        public ChanceCard LastDrawn { get; private set; }

        private void Awake()
        {
            if (_choiceTemplate != null) _choiceTemplate.gameObject.SetActive(false);
            if (_chanceCardTemplate != null) _chanceCardTemplate.gameObject.SetActive(false);
            if (_continueButton != null) _continueButton.onClick.AddListener(Continue);
            SetVisible(false);
        }

        public void Show(EncounterDefinition encounter, Action onDone)
        {
            if (encounter == null)
            {
                onDone?.Invoke();
                return;
            }

            _encounter = encounter;
            _onDone = onDone;
            _choice = null;
            LastDrawn = null;
            GamePause.Set(this, true);
            SetVisible(true);
            ShowChoices();
        }

        // ---------------------------------------------------------------- 1. Варианты

        private void ShowChoices()
        {
            CurrentStage = Stage.Choosing;
            ClearSpawned();
            SetSection(choices: true, chance: false, result: false);

            if (_title != null) _title.text = _encounter.title;
            if (_text != null) _text.text = _encounter.text;
            if (_art != null)
            {
                _art.sprite = _encounter.art != null ? _encounter.art : PlaceholderSprites.Get(PlaceholderShape.Rectangle);
                _art.color = _encounter.art != null ? Color.white : _encounter.placeholderColor;
                _art.preserveAspect = _encounter.art != null;
            }

            for (int i = 0; i < _encounter.choices.Count; i++)
            {
                EncounterChoice choice = _encounter.choices[i];
                Button button = Spawn(_choiceTemplate, _choicesContainer);

                string cost = choice.horseshoeCost > 0 ? $"  <size=75%>({choice.horseshoeCost} подков)</size>" : "";
                string risk = choice.isGamble ? "  <size=75%><color=#FFD23D>[риск]</color></size>" : "";
                SetLabel(button, choice.label + cost + risk);

                button.interactable = GameStats.Horseshoes >= choice.horseshoeCost;
                int index = i;
                button.onClick.AddListener(() => Choose(index));
            }
        }

        public void Choose(int index)
        {
            if (CurrentStage != Stage.Choosing || index < 0 || index >= _encounter.choices.Count) return;

            _choice = _encounter.choices[index];
            if (GameStats.Horseshoes < _choice.horseshoeCost) return;
            GameStats.Horseshoes -= _choice.horseshoeCost;

            if (_choice.isGamble && _choice.chanceCards.Count > 0) ShowChance();
            else
            {
                EncounterEffects.Apply(_choice.effects);
                ShowResult(_choice.resultText, _choice.effects);
            }
        }

        // ---------------------------------------------------------------- 2. Карты удачи

        private void ShowChance()
        {
            CurrentStage = Stage.Chance;
            ClearSpawned();
            SetSection(choices: false, chance: true, result: false);

            _chanceDeck.Clear();
            foreach (ChanceCard card in _choice.chanceCards)
            {
                for (int i = 0; i < card.count; i++) _chanceDeck.Add(card);
            }

            for (int i = _chanceDeck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_chanceDeck[i], _chanceDeck[j]) = (_chanceDeck[j], _chanceDeck[i]);
            }

            for (int i = 0; i < _chanceDeck.Count; i++)
            {
                Button card = Spawn(_chanceCardTemplate, _chanceContainer);
                SetLabel(card, "?");
                Image image = card.GetComponent<Image>();
                if (image != null) image.color = _cardBackColor;

                int index = i;
                card.onClick.AddListener(() => PickChance(index));
            }
        }

        public void PickChance(int index)
        {
            if (CurrentStage != Stage.Chance || index < 0 || index >= _chanceDeck.Count) return;

            LastDrawn = _chanceDeck[index];
            EncounterEffects.Apply(LastDrawn.effects);

            string header = LastDrawn.result switch
            {
                ChanceResult.HugeSuccess => "<color=#5BE06A>Большой успех!</color>",
                ChanceResult.Success => "<color=#A6E06A>Успех</color>",
                ChanceResult.Failure => "<color=#FF9A50>Неудача</color>",
                _ => "<color=#FF5050>Большая неудача!</color>"
            };
            ShowResult($"{header}\n{LastDrawn.text}", LastDrawn.effects);
        }

        // ---------------------------------------------------------------- 3. Итог

        private void ShowResult(string text, List<EncounterEffect> effects)
        {
            CurrentStage = Stage.Result;
            ClearSpawned();
            SetSection(choices: false, chance: false, result: true);

            string summary = EncounterEffects.Describe(effects);
            if (_resultText != null) _resultText.text = string.IsNullOrEmpty(summary) ? text : $"{text}\n\n<size=80%>{summary}</size>";
        }

        public void Continue()
        {
            if (CurrentStage != Stage.Result) return;

            CurrentStage = Stage.Closed;
            SetVisible(false);
            GamePause.Set(this, false);

            Action callback = _onDone;
            _onDone = null;
            callback?.Invoke();
        }

        // ---------------------------------------------------------------- Helpers

        private void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        private void SetSection(bool choices, bool chance, bool result)
        {
            if (_choicesContainer != null) _choicesContainer.gameObject.SetActive(choices);
            if (_chanceRoot != null) _chanceRoot.SetActive(chance);
            if (_resultRoot != null) _resultRoot.SetActive(result);
        }

        private Button Spawn(Button template, Transform parent)
        {
            Button button = Instantiate(template, parent != null ? parent : template.transform.parent);
            button.gameObject.SetActive(true);
            _spawned.Add(button);
            return button;
        }

        private static void SetLabel(Button button, string text)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }

        private void ClearSpawned()
        {
            foreach (Button button in _spawned) if (button != null) Destroy(button.gameObject);
            _spawned.Clear();
        }

        private void OnDestroy()
        {
            GamePause.Set(this, false);
        }
    }
}
