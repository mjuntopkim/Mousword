# PlayerSkillController.cs 변경 검토안

클래스 내부에 아래 읽기 전용 속성만 추가합니다. 기존 필드, 입력, 마나 소비, 스킬 실행 및 쿨타임 계산은 변경하지 않습니다.

```csharp
public float DashCooldownRemaining => Mathf.Max(0f, currentCoolTime);
public float SpinCooldownRemaining => Mathf.Max(0f, spinCoolTime);
public float SwordWaveCooldownRemaining => Mathf.Max(0f, swordWaveCoolTime);
public float UltimateCooldownRemaining => Mathf.Max(0f, ultimateCoolTime);

public SkillData DashSkillData => dashSlashSkill;
public SkillData SpinSkillData => spinSkill;
public SkillData SwordWaveSkillData => swordWaveSkill;
public SkillData UltimateSkillData => ultimateSkill;
```

UI는 실제 남은 시간과 각 스킬 데이터의 coolTime을 읽습니다. 스킬 사용 실패 시 표시를 시작하지 않으며, 일시정지와 실제 타이머 변경도 반영합니다. 개별 스킬의 쿨타임 숫자는 UI 코드에 고정하지 않습니다.

신규 UI 컴포넌트는 Assets/Scripts/MainScreenUI/SkillCooldownUI.cs에 준비했습니다. 승인을 받은 뒤 연결 코드를 작성하고 Player_test4 씬의 네 아이콘에만 적용합니다.
