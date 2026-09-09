using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using RobotWeapons;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    public string characterName;
    public WeaponData weaponData;
    public SkillData qSkillData;
    public SkillData eSkillData;
    public SkillData xSkillData;
    public PassiveData passiveData;
    public GameObject networkPrefab;
}