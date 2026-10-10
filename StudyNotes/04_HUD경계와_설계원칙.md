# 04. HUD 경계(PlayerHudSource)와 설계 원칙 ⭐1순위

> 난이도 🟡 · 1.5시간 · 선행: 01, 02 (03은 대충 봤으면 OK) · [← 목차](README.md)
> 코드: [Hud/PlayerHudSource.cs](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs)

"PlayerHudSource가 이해가 안 된다"는 말이 가장 중요한 질문이라, 이 챕터는 아주 천천히 갑니다.

---

## 1. 먼저 문제부터: 이게 없으면 어떻게 되나

UI 담당 팀원이 스킬 아이콘의 쿨다운을 그리고 싶다. 필요한 정보는 이것저것 흩어져 있다:

```
스킬 아이콘 UI가 알고 싶은 것          실제로 있는 곳
──────────────────────────────    ──────────────────────────────
지금 Q에 무슨 스킬?                 SkillStateModule.GetSkill(slot)
쿨다운 남은 시간?                   SkillStateModule.GetCooldownRemaining(slot)
교체 잠금 남은 시간?                 SkillStateModule.GetLockRemaining(slot)
충전 몇 개?                        SkillStateModule.GetCharges(slot)
아이콘 테두리 색 (원소 색)?          SkillData.TryGetElement → ElementPalette.ColorOf
키 이름 "Q"?                       PlayerAgent.GetSkillKeyLabel(slot)
콤보 진행 상황?                     ElementComboChain
교체 창 열렸나?                     SkillSwapInteractor.Choosing
```

UI가 이걸 **전부 직접** 가져오면:

```
 UI 스크립트 8개 ──┬──▶ SkillStateModule
                  ├──▶ ElementComboChain
                  ├──▶ SkillSwapInteractor        선이 8 × 5 = 40개
                  ├──▶ PlayerAgent
                  └──▶ ElementPalette
```

- 스킬 모듈 함수 이름 하나 바꾸면 → UI 8개가 깨진다.
- "쿨다운이 0이어도 잠금 중이면 못 쓴다" 같은 **규칙**을 UI 8개가 각자 구현 → 하나는 틀린다.
- UI 담당은 로직 코드를 다 공부해야 한다.

---

## 2. 해결: 창구를 하나 만든다

### 비유: 은행 창구
은행 안에는 대출팀, 외환팀, 카드팀이 있다. 손님(UI)은 그 팀들을 몰라도 된다. **창구 직원(PlayerHudSource)** 에게 물어보면 창구가 알아서 각 팀에 확인하고 **정리된 답** 을 준다.
은행이 내부 팀을 재편해도, 창구 응대 방식만 같으면 손님은 아무것도 모른다.

```
 UI 스크립트들 ──────▶  PlayerHudSource  ──┬──▶ SkillStateModule
 (손님)                 (창구)              ├──▶ ElementComboChain
                                           ├──▶ SkillSwapInteractor
                                           ├──▶ CharacterSwitcher
                                           └──▶ ElementPalette
     UI는 창구 하나만 안다.            로직이 바뀌면 창구 안쪽만 고친다.
```

이걸 **파사드(Facade) 패턴**이라고 한다. Facade = 건물의 "정면". 복잡한 건물 내부를 정면 하나로 가린다.

---

## 3. PlayerHudSource가 하는 4가지 일

### (1) 묻는 말에 답하기 — 조회 함수

```csharp
SlotInfo info = PlayerHudSource.Instance.GetSlot(SkillSlotId.Weapon);
icon.sprite = info.Skill.Icon;
fill.fillAmount = info.CooldownFill;    // 1 = 방금 씀, 0 = 준비됨
border.color = info.Color;              // 원소 색
keyText.text = info.Key;                // "Q"
```

### (2) 규칙을 한 곳에서 계산해서 주기 — `GetSlot` ([PlayerHudSource.cs:118](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:118))

```csharp
int charges = skills.GetCharges(slot);
float cooldown = skills.GetCooldownRemaining(slot);
float lockLeft = skills.GetLockRemaining(slot);

float remaining = 0f;
float duration = skills.GetCooldownDuration(slot);
if (charges <= 0)              // 충전이 남아 있으면 쿨다운 중이어도 쓸 수 있다 → 0으로 보여줌
    remaining = cooldown;
if (lockLeft > remaining)      // 교체 잠금이 더 길면 잠금을 보여줌
{
    remaining = lockLeft;
    duration = skills.GetLockDuration(slot);
}
```

"충전식 스킬은 충전이 남아 있으면 쿨다운을 안 보여준다", "교체 잠금이 쿨다운보다 길면 잠금을 보여준다" — 이 **규칙이 딱 한 곳**에 있다. UI는 `CooldownRemaining`만 그리면 끝.

### (3) 결과를 사진(값)으로 주기 — `SlotInfo` struct ([PlayerHudSource.cs:32](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:32))

```csharp
public readonly struct SlotInfo
{
    public readonly SkillSlotId Slot;
    public readonly SkillData Skill;
    public readonly string Key;
    public readonly float CooldownRemaining;
    ...
    public bool IsReady => Skill != null && CooldownRemaining <= 0f;
    public float CooldownFill => ...;   // UI가 바로 쓰기 좋은 모양으로 계산까지
}
```

- **struct + readonly** = 값 복사 + 수정 불가 → UI가 받은 걸 어떻게 해도 게임에 영향 0. "읽기 전용 사진".
- `GetSlot`을 호출할 때마다 그 순간의 사진을 새로 찍는다 (그래서 매 프레임 호출해도 됨, struct라 가비지도 거의 없음).
- 필요한 값이 하나의 꾸러미로 오니까, 나중에 값을 추가해도 함수 모양(시그니처)이 안 바뀐다.

### (4) 사건 전달하기 — 이벤트 재전달(relay)

```
 SkillStateModule.SkillUsed(SkillUsedInfo) ─▶ HandleSkillUsed ─▶ PlayerHudSource.SkillUsed(slot, data)
 ElementComboChain.ComboLanded(LandedCombo) ─▶ HandleComboLanded ─▶ PlayerHudSource.ComboLanded(recipe)
 ElementComboChain.InputAdded(bool)  ─┐
 ElementComboChain.StockChanged()    ─┼─▶ PlayerHudSource.ComboChanged()  ← 3개를 1개로 합침
 ElementComboChain.ChainBroken(int)  ─┘     (+ ComboBroken도 따로)
 SkillSwapInteractor.ChoosingChanged ─▶ PlayerHudSource.SwapChanged
```

```csharp
// PlayerHudSource.cs:297 — 받아서 그대로(또는 UI 친화적으로 바꿔서) 다시 발행
private void HandleSkillUsed(SkillUsedInfo info) => SkillUsed?.Invoke(info.Slot, info.Data);
private void HandleComboLanded(ElementComboChain.LandedCombo landed) => ComboLanded?.Invoke(landed.Combo);
```

**왜 그대로 넘기지 않고 다시 발행하나?**
- UI가 `ElementComboChain.LandedCombo` 같은 **로직 내부 타입**을 몰라도 되게.
- "입력 추가 / 스톡 변경 / 체인 끊김"은 UI 입장에선 전부 "콤보 칸 다시 그려"라서 `ComboChanged` 하나로 합침 → UI 코드가 단순해짐.
- UI는 로직 객체가 언제 생기는지, 몇 개인지 신경 쓸 필요 없이 **창구의 이벤트만 구독**.

---

## 4. 연결 방식 — 싱글턴 + 지연 연결 (Lazy Bind)

### 싱글턴 `Instance` ([PlayerHudSource.cs:76](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:76))

```
UI가 PlayerHudSource.Instance 를 부름
   │
   ├─ 이미 있음? → 그거 줌
   ├─ 없음 → 씬에서 PlayerAgent 찾기
   │          ├─ 없음 → null (플레이어 없는 씬)
   │          └─ 있음 → 거기 PlayerHudSource 붙어있나?
   │                    ├─ 있음 → 그거 줌
   │                    └─ 없음 → AddComponent로 붙여서 줌
```

→ **씬에서 아무것도 연결 안 해도 동작한다.** 팀원이 UI 프리팹을 아무 씬에 넣어도 `Instance`로 바로 찾는다.

### 지연 연결 `Bind()` ([PlayerHudSource.cs:213](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:213))

**문제:** 02장에서 배운 것처럼 Start 순서는 보장되지 않는다. UI의 Start가 PlayerHudSource의 Start보다 먼저 불려서 `GetSlot`을 호출하면? 아직 아무것도 연결 안 된 상태.

**해결:** "누가 값을 물어보는 순간, 아직 연결 안 됐으면 그때 연결한다."

```csharp
private SkillStateModule Skills
{
    get
    {
        Bind();          // ← 물어볼 때마다 "연결됐나?" 확인 (이미 됐으면 바로 return)
        return _skills;
    }
}

private void Bind()
{
    if (_bound) return;                                 // 한 번만
    if (player == null) player = ... 찾기;
    if (player == null || player.SkillFsm == null) return;   // 아직 준비 안 됨 → 다음에 다시 시도
    _skills = player.SkillFsm;
    comboChain ??= 찾기; swapInteractor ??= 찾기; ...   // 비어 있으면 찾음 (없어도 OK)
    _bound = true;
    ... 모든 이벤트 구독
}
```

비유: 택배 기사가 "문 앞에 두고 갈게요"가 아니라, **내가 문을 여는 순간** 택배를 받는 것. 언제 열든 받을 수 있다.

### 없는 기능은 조용히 무시
`comboChain`이 없는 씬(콤보 안 쓰는 테스트 씬)이면 → `HasCombo == false`, `ComboInputs`는 빈 배열, 콤보 이벤트는 그냥 안 터진다. **에러 없음**.
```csharp
public IReadOnlyList<...> ComboInputs => comboChain != null ? comboChain.Inputs : Array.Empty<...>();
```
→ UI는 "콤보가 있는 씬인가?"를 매번 확인할 필요 없이 빈 목록을 그리면 된다.

---

## 5. 의존 방향 — 화살표는 한쪽으로만

```
        ┌─────────┐       ┌─────────┐       ┌──────────────────────────────┐
        │   UI/   │ ────▶ │  Hud/   │ ────▶ │ 로직: FSM_Skill_Module,      │
        │ (화면)   │       │ (창구)   │       │ Element, SkillSwap           │
        └─────────┘       └─────────┘       └──────────────────────────────┘
   UI는 Hud를 안다      Hud는 로직을 안다        로직은 Hud도 UI도 모른다 ✅
```

확인 방법: 로직 폴더(`Char/`, `Element/`, `SkillSwap/`)에서 `using ...UI` / `using ...Hud`를 검색하면 **0개**. Hud에서 `using ...UI`도 0개.

### 그래서 파일을 옮겼다
| 파일 | 전 → 후 | 이유 |
|---|---|---|
| `ElementPalette` | UI/ → **Element/** | 바닥 스킬 구슬(`SkillPickup`, 로직)도 원소 색이 필요. 로직이 UI 폴더를 참조하면 화살표가 거꾸로 됨. 색은 "원소의 성질"이니 Element로. |
| `ComboKeyMap` | UI/ → **Hud/** | "원소 → 누를 키" 변환은 창구가 UI에 주는 정보(`KeyFor(element)`). 창구가 UI 폴더를 참조하면 안 되니까 Hud로. |

그리고 `ElementPalette.Default`는 `Resources`에서 스스로 로드 → 인스펙터 연결 필요 없음.

### 왜 방향이 중요한가 (3가지)
1. **바뀌는 정도가 다르다.** UI는 디자인 피드백으로 매주 바뀐다. 로직은 덜 바뀐다. 자주 바뀌는 쪽이 덜 바뀌는 쪽에 의존해야 → UI를 갈아엎어도 로직은 안전.
   (실제로 콤보 UI를 "악보" → "격투게임 콤보"로 통째로 바꿨을 때 판정 코드는 안 바뀌었다.)
2. **순환 참조 방지.** A가 B를 알고 B가 A를 알면, 하나 고칠 때 둘 다 봐야 한다. 나중에 어셈블리(asmdef)로 나누려 하면 컴파일 자체가 안 된다.
3. **병합 충돌이 줄어든다.** 팀원(UI 담당)은 `UI/`와 자기 프리팹만, 나는 로직과 `Hud/`만 고친다. 서로의 파일을 건드릴 일이 거의 없음.

### 씬 파일과 병합
- 씬(`.unity`)은 git 병합이 사실상 불가능 → 둘이 같은 씬을 고치면 한 명 작업이 날아간다.
- 그래서: **UI는 프리팹으로**, **참조는 코드가 스스로 찾게**(`Instance`, `Bind`의 자동 탐색, `ElementPalette.Default`) → 씬에 저장되는 연결 정보 자체를 줄인다.
- `ComboFeedbackUI`도 "씬 연결은 이 컴포넌트 하나뿐"을 목표로 만듦 (07장).

### 솔직한 현재 상태
아직 `SkillSlotView`, `SkillBarView`, `ElementComboHud` 등 일부 UI는 `SkillStateModule`/`ElementComboChain`을 직접 참조한다. 창구는 최근(bf81167)에 생겼고, `ComboFeedbackUI`처럼 **새로 만드는 UI부터 창구만 쓰는 것**이 규칙. 기존 UI는 점진적으로 옮기면 된다.
(면접에서 "다 완벽하다"보다 "이렇게 옮기는 중이고 이유는 이렇다"가 훨씬 신뢰를 준다.)

---

## 6. SOLID — 우리 코드로 하나씩

| 원칙 | 한 줄 뜻 | 우리 코드 예 |
|---|---|---|
| **S** 단일 책임 | 클래스 하나는 바뀌는 이유가 하나 | `ElementComboBook`(판정 규칙) / `ElementComboChain`(입력 기록·타이머) / `ElementComboHud`(그리기)를 나눔. 판정 규칙이 바뀌어도 그리기 코드는 안 바뀜 |
| **O** 개방-폐쇄 | 기능 추가는 새 코드로, 기존 코드는 수정 X | 새 스킬 = `SkillData` 상속 클래스 하나 추가. `SkillStateModule`은 안 고침. 스킬 교체 기능 = `ISkillInputInterceptor` 구현 추가, 스킬 모듈은 가로채기 구멍만 제공 |
| **L** 리스코프 치환 | 자식은 부모 자리에 넣어도 문제없어야 | 어떤 `SkillData`든 `Begin(context)`만 부르면 동작. `EmptySkillData`(빈 스킬)도 슬롯에 그대로 들어감 |
| **I** 인터페이스 분리 | 큰 인터페이스 하나보다 작은 여러 개 | `ISkillContext` = `ISkillMover` + `ISkillCombatant` + `ISkillEffects` + ... |
| **D** 의존성 역전 | 구체 클래스 말고 추상(인터페이스)에 의존 | 스킬은 `PlayerAgent`가 아니라 `ISkillContext`에 의존. 스킬 모듈은 `SkillSwapInteractor`가 아니라 `ISkillInputInterceptor`에 의존 |

### 추가로 쓴 패턴
| 패턴 | 어디 | 한 줄 |
|---|---|---|
| 파사드 | `PlayerHudSource`, `Fx` | 복잡한 내부를 창구 하나로 |
| 옵저버(이벤트) | `SkillUsed`, `ComboLanded`... | 보내는 쪽이 받는 쪽을 모름 |
| 어댑터 | `PlayerSkillContext` | 인터페이스 ↔ 실제 플레이어 코드 변환 |
| 상태 | `SkillStateModule` + `GenericSkillState` | 한 번에 하나의 스킬 상태 |
| 싱글턴 | `PlayerHudSource.Instance`, `HitFeel` | 하나뿐인 전역 접근점 (남용 주의) |
| 데코레이터 비슷한 것 | `InterceptedInput` | 원래 입력을 감싸서 일부 키만 "안 눌림"으로 보이게 (05장) |

---

## 🎤 면접에서 이렇게 물으면

**Q. PlayerHudSource가 뭔가요? 왜 만들었나요?**
> UI가 게임 로직을 직접 참조하지 않도록 만든 파사드입니다. UI가 필요한 스킬·콤보·교체 정보를 이 클래스 하나에서 조회하고, 로직의 이벤트도 이 클래스가 받아서 UI용 이벤트로 다시 발행합니다.
> 효과는 세 가지입니다. 로직을 고쳐도 이 클래스 안쪽만 고치면 UI가 깨지지 않고, "충전이 남으면 쿨다운을 안 보여준다" 같은 표시 규칙이 한 곳에만 있고, UI 담당 팀원은 로직 코드를 몰라도 됩니다.

**Q. 싱글턴을 썼는데 문제는 없나요?**
> 전역 접근이라 추적이 어려워질 수 있어서 쓰임새를 HUD 조회로 한정했습니다. 플레이어당 하나인 게 자연스러운 객체였고,
> 씬 연결 없이 UI 프리팹을 어디든 넣을 수 있게 하는 게 목적이었습니다. 도메인 리로드를 끈 환경을 대비해 static 참조를 플레이 시작 때 초기화하는 코드도 넣었습니다.

**Q. 초기화 순서 문제는 어떻게 해결했나요?**
> Unity는 Start끼리 순서를 보장하지 않아서, 창구가 처음 값을 요청받는 순간 연결하는 지연 연결을 썼습니다. 플레이어의 스킬 모듈이 아직 없으면 연결을 미루고 다음 요청 때 다시 시도합니다.

**Q. 의존 방향을 왜 신경 썼나요?**
> UI는 자주 바뀌고 로직은 덜 바뀌어서, UI가 로직을 알고 로직은 UI를 모르게 했습니다. 실제로 콤보 UI를 완전히 새로 만들 때 판정 코드는 바뀌지 않았습니다.
> 또 팀원과 파일이 겹치지 않아 병합 충돌이 줄었고, 원소 팔레트를 UI 폴더에서 원소 폴더로 옮긴 것도 로직이 UI를 참조하지 않게 하려는 것이었습니다.

**Q. SOLID 중 하나를 프로젝트 예로 설명해 주세요.**
> 개방-폐쇄 원칙으로, 스킬 교체 기능을 추가할 때 스킬 모듈에는 "키 가로채기" 인터페이스 자리만 두고, 교체 기능은 그 인터페이스를 구현한 별도 클래스로 만들었습니다. 나중에 상점 UI가 키를 막아야 해도 같은 인터페이스만 구현하면 됩니다.

---

## ✅ 스스로 확인 질문

1. UI가 `SkillStateModule.GetCooldownRemaining`을 직접 쓰면, 충전 2개짜리 대시에서 어떤 잘못된 표시가 나올 수 있나? (힌트: GetSlot의 `if (charges <= 0)`)
2. `ComboChanged` 하나로 합친 원본 이벤트 3개는 뭐고, 왜 합쳤나?
3. UI의 Start가 PlayerHudSource의 Start보다 먼저 불려도 괜찮은 이유는?
4. `ElementPalette`를 UI 폴더에 두면 어떤 화살표가 거꾸로 되나?
5. 콤보가 없는 씬에서 `PlayerHudSource.ComboInputs`는 무엇을 돌려주나? 그게 왜 좋은가?
