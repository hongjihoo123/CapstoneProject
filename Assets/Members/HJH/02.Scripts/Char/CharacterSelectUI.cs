using System;
using Members.KYR._01_Scripts;
using UnityEngine;

public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private CharacterDataSO[] characterOptions;

    public event Action<CharacterDataSO> OnSelected;

    public void OnSelect(int index)
    {
        if (characterOptions == null || index < 0 || index >= characterOptions.Length)
        {
            Debug.LogWarning($"{name}: characterOptions[{index}] 가 없습니다.");
            return;
        }

        CharacterDataSO selected = characterOptions[index];
        CharacterSelectionContext.Select(selected);

        PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
        if (player != null)
            player.ApplySelectedCharacter();

        OnSelected?.Invoke(selected);
    }
}
