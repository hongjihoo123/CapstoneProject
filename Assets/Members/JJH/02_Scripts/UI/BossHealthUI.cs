using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Members.JJH._02_Scripts.Agents.Enemies;

namespace Members.JJH._02_Scripts.UI
{
    public class BossHealthUI : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] private AbstractEnemy boss;

        [Header("UI")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider delayedHealthSlider;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float delayedHealthDelay = 0.5f;
        [SerializeField] private float delayedHealthDuration = 0.8f;
        [SerializeField] private float fadeDelay = 3f;
        [SerializeField] private float fadeDuration = 1.5f;
        [SerializeField] private float healthTweenDuration = 0.2f;

        private Tween _healthTween;
        private Tween _fadeTween;
        private Tween _delayedHealthTween;
        private Tween _delayedHealthDelayTween;

        private bool _isDead;
        private float _delayedTargetHealth;

        private void Awake()
        {
            healthSlider.maxValue = boss.EnemyData.Health;
            healthSlider.value = boss.EnemyData.Health;
            bossNameText.text = boss.EnemyData.EnemyName;
            delayedHealthSlider.maxValue = boss.EnemyData.Health;
            delayedHealthSlider.value = boss.EnemyData.Health;
            canvasGroup.alpha = 1f;
        }

        private void Start()
        {
            boss.Health.OnDeathEvent?.AddListener(HandleOnDeath);
        }

        private void OnDestroy()
        {
            if (boss != null && boss.Health != null)
                boss.Health.OnDeathEvent?.RemoveListener(HandleOnDeath);

            _healthTween?.Kill();
            _delayedHealthTween?.Kill();
            _delayedHealthDelayTween?.Kill();
            _fadeTween?.Kill();
        }

        private void HandleOnDeath()
        {
            if (_isDead)
                return;

            _isDead = true;

            _healthTween?.Kill();
            _delayedHealthTween?.Kill();
            _delayedHealthDelayTween?.Kill();

            _healthTween = healthSlider.DOValue(0f, healthTweenDuration);

            _delayedHealthTween = delayedHealthSlider
                .DOValue(0f, healthTweenDuration)
                .SetDelay(delayedHealthDelay);

            _fadeTween = DOVirtual.DelayedCall(fadeDelay, () =>
            {
                _fadeTween = canvasGroup.DOFade(0f, fadeDuration)
                    .OnComplete(() => gameObject.SetActive(false));
            });
        }

        private void Update()
        {
            if (_isDead)
                return;

            if (boss == null || !boss.gameObject.activeInHierarchy)
            {
                HandleOnDeath();
                return;
            }

            float currentHealth = boss.Health.CurrentHealth;

            if (!Mathf.Approximately(healthSlider.value, currentHealth))
            {
                _healthTween?.Kill();
                _healthTween = healthSlider.DOValue(currentHealth, healthTweenDuration);

                if (currentHealth < delayedHealthSlider.value)
                {
                    _delayedTargetHealth = currentHealth;

                    if (_delayedHealthDelayTween == null || !_delayedHealthDelayTween.IsActive())
                    {
                        _delayedHealthDelayTween = DOVirtual.DelayedCall(delayedHealthDelay, () =>
                        {
                            _delayedHealthDelayTween = null;
                            UpdateDelayedHealth();
                        });
                    }
                }
                else
                {
                    _delayedHealthDelayTween?.Kill();
                    _delayedHealthDelayTween = null;

                    _delayedHealthTween?.Kill();
                    _delayedHealthTween = null;

                    delayedHealthSlider.value = currentHealth;
                }
            }
        }

        private void UpdateDelayedHealth()
        {
            _delayedHealthTween?.Kill();

            _delayedHealthTween = delayedHealthSlider
                .DOValue(_delayedTargetHealth, delayedHealthDuration)
                .OnComplete(() =>
                {
                    if (!Mathf.Approximately(delayedHealthSlider.value, _delayedTargetHealth))
                        UpdateDelayedHealth();
                });
        }
    }
}