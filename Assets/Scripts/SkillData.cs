using UnityEngine;

// 유니티 프로젝트 창에서 마우스 우클릭으로 스킬 파일을 만들 수 있게 된다.
[CreateAssetMenu(fileName = "SkillData", menuName = "Scriptable Objects/SkillData")]
public class SkillData : ScriptableObject
{
    public string skillName;       // 스킬 이름
    public float damageMultiplier; // 데미지 배율
    public float manaCost;         // 소모 마나 [cite: 7]
    public float coolTime;         // 쿨타임
    public GameObject effectPrefab;// 스킬 이펙트 프리팹
    
    public float dashForce = 20f;     // 대시 속도
    public float dashDuration = 0.2f; // 대시 지속 시간
}
