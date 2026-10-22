using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 피니시 로직(ElementComboChain: 스택, C, 강화, 피니시 스킬)을 JJH 콤보 UI(ElementStackView)에 보여주는 연결.
    // 씬의 체인 옆에 하나 붙이면 끝. 켜져 있는 ElementStackView 를 전부 찾아서
    //   다이아 = 체인 스택, READY 글자 = 준비 중이면 배율 + 남은 시간, 배너 = 준비 / 피니시 / 놓침 / 피니시 스킬 저장
    // JJH 뷰는 연결되면 자기 스톡 계산과 피니시 키를 끔 (UseExternalFinisher)
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
            chain.FinisherStored += OnStored;
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
            chain.FinisherStored -= OnStored;
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

        private void OnStored(FinisherBook.Entry finisher) =>
            Banner("FINISH SKILL", finisher.name, "피니시 스킬 저장", false);

        private void OnArmed(int stacks, float scale)
        {
            int skills = chain.ArmedFinishers.Count;
            Banner("READY", "피니시 준비", skills > 0 ? $"다음 스킬 ×{scale:0.0#}  +  피니시 스킬 {skills}개" : $"다음 스킬 ×{scale:0.0#}", false);
            foreach (ElementStackView view in _views)
                view.SetFinisherReady(true, scale, chain.ArmedRemaining);
        }

        private void OnExpired()
        {
            Banner("MISS", "피니시 시간 끝", string.Empty, false);
            NotReady();
        }

        private void OnReleased(SkillSlotId slot, float scale, IReadOnlyList<FinisherBook.Entry> fired)
        {
            var names = new List<string>();
            foreach (FinisherBook.Entry finisher in fired)
                names.Add(finisher.name);
            Banner($"FINISH ×{scale:0.0#}", "피니시", names.Count > 0 ? string.Join("  +  ", names) : "피해 강화", true);
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
