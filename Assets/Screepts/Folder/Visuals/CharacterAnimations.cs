using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Tanks2D
{
    // Состояния, которые вызывает код игры
    public enum CharacterAnimation
    {
        Idle,    // стоит (герой, башня, стена; враг у стены между ударами)
        Move,    // идёт (враг)
        Attack,  // выстрел героя/башни, удар врага по стене
        Hit,     // получил урон
        Death,   // смерть (последний кадр остаётся на экране)
        Special  // особое: щит босса, перезарядка героя
    }

    [Serializable]
    public class AnimationSlot
    {
        [Tooltip("Готовый Animation Clip. Важнее набора кадров")]
        public AnimationClip clip;

        [Tooltip("Или просто кадры-спрайты по порядку")]
        public Sprite[] frames = Array.Empty<Sprite>();

        [Min(0.1f)] public float framesPerSecond = 12f;

        public bool HasContent => clip != null || (frames != null && frames.Length > 0);

        public float Duration
        {
            get
            {
                if (clip != null) return clip.length;
                if (frames != null && frames.Length > 0) return frames.Length / Mathf.Max(0.1f, framesPerSecond);
                return 0f;
            }
        }
    }

    // Анимации персонажа: перетащите в ячейки Animation Clip или кадры-спрайты.
    // Код игры сам вызывает нужную ячейку (см. CharacterAnimation). Пустые ячейки просто пропускаются.
    // Idle и Move играют по кругу, остальные — один раз, после чего возвращается Idle/Move.
    [DisallowMultipleComponent]
    public class CharacterAnimations : MonoBehaviour
    {
        [Tooltip("Слот внешнего вида. Пусто — берётся с этого же объекта")]
        [SerializeField] private VisualSlot _visual;
        [Tooltip("Общая скорость анимаций")]
        [SerializeField, Min(0.01f)] private float _speed = 1f;

        [Header("Анимации")]
        [SerializeField] private AnimationSlot _idle = new AnimationSlot();
        [SerializeField] private AnimationSlot _move = new AnimationSlot();
        [SerializeField] private AnimationSlot _attack = new AnimationSlot();
        [SerializeField] private AnimationSlot _hit = new AnimationSlot();
        [SerializeField] private AnimationSlot _death = new AnimationSlot();
        [SerializeField] private AnimationSlot _special = new AnimationSlot();

        private CharacterAnimation _base = CharacterAnimation.Idle;
        private CharacterAnimation _current = CharacterAnimation.Idle;
        private AnimationSlot _currentSlot;
        private bool _oneShot;
        private bool _dead;
        private float _time;

        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private AnimationClip _graphClip;

        public CharacterAnimation Current => _current;
        public bool IsPlayingOneShot => _oneShot;

        private void Awake()
        {
            if (_visual == null) _visual = GetComponent<VisualSlot>();
        }

        private void Start()
        {
            // Разовую анимацию, запущенную до Start (в кадре появления объекта), не перебиваем
            if (!_oneShot && !_dead) StartSlot(_base, false);
        }

        public AnimationSlot GetSlot(CharacterAnimation animation)
        {
            switch (animation)
            {
                case CharacterAnimation.Move: return _move;
                case CharacterAnimation.Attack: return _attack;
                case CharacterAnimation.Hit: return _hit;
                case CharacterAnimation.Death: return _death;
                case CharacterAnimation.Special: return _special;
                default: return _idle;
            }
        }

        public bool Has(CharacterAnimation animation) => GetSlot(animation).HasContent;

        // Фоновое (зацикленное) состояние: Idle или Move
        public void SetBase(CharacterAnimation animation)
        {
            if (_dead || _base == animation) return;
            _base = animation;
            if (!_oneShot) StartSlot(_base, false);
        }

        // Разовая анимация. Возвращает её длительность (0 — ячейка пустая, ничего не играет)
        public float Play(CharacterAnimation animation)
        {
            if (_dead) return 0f;

            AnimationSlot slot = GetSlot(animation);
            if (!slot.HasContent) return 0f;

            if (animation == CharacterAnimation.Death) _dead = true;
            StartSlot(animation, true);
            return slot.Duration / _speed;
        }

        private void StartSlot(CharacterAnimation animation, bool oneShot)
        {
            AnimationSlot slot = GetSlot(animation);
            _current = animation;
            _oneShot = oneShot && slot.HasContent;
            _time = 0f;

            if (!slot.HasContent)
            {
                _currentSlot = null;
                return;
            }

            _currentSlot = slot;
            if (slot.clip != null) PrepareClip(slot.clip);
            Sample();
        }

        private void Update()
        {
            if (_currentSlot == null) return;

            // На паузе стоят все анимации, кроме смерти (например, разрушение стены на экране Game Over)
            bool paused = GamePause.IsPaused;
            if (paused && _current != CharacterAnimation.Death) return;

            _time += (paused ? Time.unscaledDeltaTime : Time.deltaTime) * _speed;

            if (_oneShot && _time >= _currentSlot.Duration)
            {
                if (_dead)
                {
                    // Смерть: замираем на последнем кадре
                    _time = _currentSlot.Duration;
                    Sample();
                    _currentSlot = null;
                    return;
                }

                StartSlot(_base, false);
                return;
            }

            Sample();
        }

        private void Sample()
        {
            AnimationSlot slot = _currentSlot;
            if (slot == null) return;

            float duration = Mathf.Max(0.0001f, slot.Duration);
            float time = _oneShot ? Mathf.Min(_time, duration) : Mathf.Repeat(_time, duration);

            if (slot.clip != null)
            {
                if (!_graph.IsValid()) return;
                _clipPlayable.SetTime(Mathf.Min(time, slot.clip.length - 0.0001f));
                _graph.Evaluate();
                return;
            }

            SpriteRenderer target = _visual != null ? _visual.Renderer : null;
            if (target == null) return;

            int index = Mathf.Clamp(Mathf.FloorToInt(time * slot.framesPerSecond), 0, slot.frames.Length - 1);
            if (slot.frames[index] != null) target.sprite = slot.frames[index];
        }

        // Animation Clip играет на объекте с картинкой (Model или Visual) через PlayableGraph — Animator Controller не нужен
        private void PrepareClip(AnimationClip clip)
        {
            if (_graph.IsValid() && _graphClip == clip) return;
            DestroyGraph();

            Transform root = _visual != null ? _visual.ArtRoot : transform;
            if (root == null) return;

            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;

            _graph = PlayableGraph.Create($"{name}_Animations");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            output.SetSourcePlayable(_clipPlayable);
            _graph.Play();
            _graphClip = clip;
        }

        private void DestroyGraph()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _graphClip = null;
        }

        private void OnDestroy()
        {
            DestroyGraph();
        }
    }
}
