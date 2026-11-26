using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    public class FinisherViewLink : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 씬에서 찾음")] private ElementComboChain chain;

        private readonly List<ElementStackView> _views = new();
        private readonly List<string> _names = new();

        private void Start()
        {
            if (chain == null)
                chain = FindAnyObjectByType<ElementComboChain>();
            if (chain == null)
            {
                Debug.LogWarning($"{name}: FinisherViewLink 가 ElementComboChain 을 못 찾음");
                enabled = false;
                return;
            }

            _views.AddRange(FindObjectsByType<ElementStackView>(FindObjectsSortMode.None));
            foreach (ElementStackView view in _views)
                view.UseExternalFinisher();

            chain.StockChanged += DrawStock;
            chain.FinisherArmed += OnArmed;
            chain.FinisherExpired += OnExpired;
            chain.FinisherReleased += OnReleased;
            DrawStock();
        }

        private void OnDestroy()
        {
            if (chain == null)
                return;
            chain.StockChanged -= DrawStock;
            chain.FinisherArmed -= OnArmed;
            chain.FinisherExpired -= OnExpired;
            chain.FinisherReleased -= OnReleased;
        }

        private void Update()
        {
            if (chain == null || !chain.IsArmed)
                return;
            foreach (ElementStackView view in _views)
                view.SetFinisherReady(true, chain.ArmedScale, chain.ArmedRemaining);
        }

        private void DrawStock()
        {
            _names.Clear();
            foreach (ElementComboBook.Recipe recipe in chain.Stock)
                _names.Add(recipe.Name);
            foreach (ElementStackView view in _views)
                view.SetFinisherStock(_names);
        }

        private void OnArmed(int stacks, float scale)
        {
            Banner("READY", "피니시 준비", $"다음 스킬 ×{scale:0.0#}", false);
            foreach (ElementStackView view in _views)
                view.SetFinisherReady(true, scale, chain.ArmedRemaining);
        }

        private void OnExpired()
        {
            Banner("MISS", "피니시 시간 끝", string.Empty, false);
            NotReady();
        }

        private void OnReleased(SkillSlotId slot, float scale)
        {
            Banner($"FINISH ×{scale:0.0#}", "피니시", "피해 강화", true);
            NotReady();
        }

        private void NotReady()
        {
            foreach (ElementStackView view in _views)
                view.SetFinisherReady(false, 1f, 0f);
        }

        private void Banner(string header, string title, string detail, bool flash)
        {
            foreach (ElementStackView view in _views)
                view.ShowFinisherBanner(header, title, detail, flash);
        }
    }
}
