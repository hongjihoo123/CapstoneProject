using DG.Tweening;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Members.JJH._02_Scripts.Map
{
    public class SceneTransitionManager : MonoBehaviour
    {
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float fadeDuration = 0.5f;

        private bool isTransitioning;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }

        private void OnEnable()
        {
            eventChannel.AddListener<ChangeSceneEvent>(OnChangeScene);
        }

        private void OnDisable()
        {
            eventChannel.RemoveListener<ChangeSceneEvent>(OnChangeScene);
        }

        private void Start()
        {
            fadeCanvasGroup.DOFade(0f, fadeDuration).OnComplete(() => fadeCanvasGroup.blocksRaycasts = false);
        }

        private void OnChangeScene(ChangeSceneEvent evt)
        {
            if (isTransitioning || string.IsNullOrWhiteSpace(evt.SceneName))
                return;

            StartCoroutine(TransitionScene(evt.SceneName));
        }

        private IEnumerator TransitionScene(string sceneName)
        {
            isTransitioning = true;
            fadeCanvasGroup.blocksRaycasts = true;

            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                fadeCanvasGroup.DOFade(0f, fadeDuration).OnComplete(() => fadeCanvasGroup.blocksRaycasts = false);
                isTransitioning = false;
                yield break;
            }

            yield return operation;

            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();

            fadeCanvasGroup.blocksRaycasts = false;
            isTransitioning = false;
        }
    }
}