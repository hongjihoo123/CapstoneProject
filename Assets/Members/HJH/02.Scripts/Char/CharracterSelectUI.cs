using RobotWeapons;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName;
    [SerializeField] private WeaponData[] weaponOptions;

    public void OnSelect(int index)
    {
        CharacterSelectionContext.Select(weaponOptions[index]);
        SceneManager.LoadScene(gameSceneName);
    }
}