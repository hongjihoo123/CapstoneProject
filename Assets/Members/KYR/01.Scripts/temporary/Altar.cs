using System.Collections;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01.Scripts.temporary
{
    public class Altar : MonoBehaviour
    {
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private EventChannelSO moneyChannel;
        [SerializeField] private bool receiveGold;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private GameObject[] rewards;
        [SerializeField] private GameObject fkeyImage;
        [SerializeField, Min(0.1f)] private float detectRadius = 2.2f;
        [SerializeField, Min(0f)] private float sinkDistance = 3f;
        [SerializeField, Min(0.01f)] private float sinkDuration = 2.5f;

        private int _playersInside;
        private bool _used;

        private void Awake()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                range = gameObject.AddComponent<SphereCollider>();

            range.isTrigger = true;
            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);

            SetPrompt(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside++;
            SetPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside = Mathf.Max(0, _playersInside - 1);
            if (_playersInside == 0)
                SetPrompt(false);
        }

        private void Update()
        {
            if (_used || _playersInside <= 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Activate();
        }

        private void OnValidate()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                return;

            range.isTrigger = true;
            range.radius = detectRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.84f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, 0.9f, 0f), detectRadius);
        }

        private void Activate()
        {
            GameObject prefab = PickReward();
            if (prefab == null)
            {
                Debug.LogWarning($"{name}: 소환할 프리팹이 없습니다.", this);
                return;
            }

            _used = true;
            SetPrompt(false);
            GrantGold();
            Instantiate(prefab, transform.position, prefab.transform.rotation);

            foreach (Collider colorlider in GetComponentsInChildren<Collider>())
                GetComponent<Collider>().enabled = false;

            StartCoroutine(Sink());
        }

        private void GrantGold()
        {
            if (!receiveGold || price <= 0)
                return;

            if (moneyChannel == null)
            {
                Debug.LogWarning($"{name}: 돈 채널이 없습니다.", this);
                return;
            }

            moneyChannel.RaiseEvent(MoneyEvents.GainGoldEvent.Init(price));
        }

        private GameObject PickReward()
        {
            if (rewards == null || rewards.Length == 0)
                return null;

            int start = Random.Range(0, rewards.Length);
            for (int i = 0; i < rewards.Length; i++)
            {
                GameObject candidate = rewards[(start + i) % rewards.Length];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private IEnumerator Sink()
        {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * sinkDistance;
            float elapsed = 0f;

            while (elapsed < sinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sinkDuration));
                transform.position = Vector3.LerpUnclamped(start, end, t);
                yield return null;
            }

            transform.position = end;
        }

        private bool IsPlayer(Collider other) =>
            other != null && (playerLayer.value & (1 << other.gameObject.layer)) != 0;

        private void SetPrompt(bool visible)
        {
            if (fkeyImage != null)
                fkeyImage.SetActive(visible);
        }
    }
}