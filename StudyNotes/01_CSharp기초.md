# 01. C# 기초 — 우리 코드에 나오는 것만

> 난이도 🟢 · 1시간 · [← 목차](README.md)

우리 코드를 읽는 데 꼭 필요한 C# 문법 6가지. 전부 **실제 우리 코드 예시**로 설명합니다.

---

## 1. 델리게이트와 이벤트 — "구독 신청서"

### 비유
유튜브 구독. 채널(보내는 쪽)은 구독자가 누군지 몰라도 "새 영상 올림!" 하면 구독자 전원에게 알림이 간다.
구독자는 원할 때 구독하고 원할 때 취소한다.

```
     SkillStateModule (채널)
          │  "스킬 썼다!" (SkillUsed 이벤트 발행)
          ├──────────▶ ElementComboChain   (콤보 판정하자)
          ├──────────▶ PlayerHudSource     (UI에 알려주자)
          └──────────▶ SkillElementBridge  (JJH 원소 시스템에 알려주자)
   채널은 이 셋이 누군지 모른다. 그냥 "썼다"고 외칠 뿐.
```

### 문법

```csharp
// 1) 이벤트 선언 (보내는 쪽) — SkillStateModule.cs:65
public event Action<SkillUsedInfo> SkillUsed;
//            ^^^^^^^^^^^^^^^^^^^ "SkillUsedInfo 하나를 받는 함수"만 구독 가능

// 2) 발행 (보내는 쪽) — SkillStateModule.cs:321
SkillUsed?.Invoke(info);
//       ^^ 구독자가 0명이면 null이라서, ?. 로 "있을 때만 호출"

// 3) 구독 / 해제 (받는 쪽) — PlayerHudSource.cs:237, 268
_skills.SkillUsed += HandleSkillUsed;   // 구독
_skills.SkillUsed -= HandleSkillUsed;   // 해제 (꼭!)
```

| 용어 | 뜻 |
|---|---|
| **델리게이트** | "함수를 담는 변수". `Action<T>` = 반환값 없고 T 하나 받는 함수 타입. `Func<T, R>` = 반환값 R이 있는 함수 타입 |
| **event** 키워드 | 델리게이트에 "밖에서는 `+=` `-=`만 하고, 호출(Invoke)은 나만 한다"는 잠금을 건 것 |

### 왜 쓰나
- 보내는 쪽 코드를 **한 줄도 안 고치고** 받는 쪽을 늘릴 수 있다. 콤보 시스템, HUD, 원소 브리지를 추가할 때 `SkillStateModule`은 안 건드렸다.

### ⚠️ 함정: 구독 해제 안 하면?
오브젝트가 파괴돼도 채널이 그 함수를 계속 들고 있어서, 다음 발행 때 **파괴된 오브젝트의 함수가 불려 에러**(MissingReferenceException)가 나거나 메모리가 샌다.
→ 규칙: `OnEnable`에서 `+=` 했으면 `OnDisable`에서 `-=`. `Start`/`Bind`에서 했으면 `OnDestroy`에서. ([PlayerHudSource.cs:260](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:260))

---

## 2. 인터페이스 — "자격증"

### 비유
"운전면허 있는 사람 구함" 공고. 그 사람이 택시기사든 대학생이든 상관없다. **면허(인터페이스)** 만 있으면 운전(메서드)을 시킬 수 있다.

```csharp
// ISkillInputInterceptor.cs — "스킬 키를 잠깐 빌려 쓸 수 있는 자격"
public interface ISkillInputInterceptor
{
    bool Intercepts(SkillSlotId slot);        // 이 키 가로챌래?
    void OnInterceptedPress(SkillSlotId slot); // 가로챈 키가 눌렸어
}

// SkillSwapInteractor.cs:205 — 이 자격증을 땄다고 선언
public class SkillSwapInteractor : MonoBehaviour, ISkillInputInterceptor
```

`SkillStateModule`은 `List<ISkillInputInterceptor>`만 들고 있다. 그 안에 스킬 교체가 들어있는지, 나중에 만들 상점 UI가 들어있는지 **모른다**.
→ 나중에 "상점 열면 Q/E 막기"가 필요하면 상점이 같은 인터페이스만 구현하면 끝. `SkillStateModule` 수정 0줄.

### 인터페이스 vs 추상 클래스

| | 인터페이스 (`ISkillContext`) | 추상 클래스 (`SkillData`) |
|---|---|---|
| 들어있는 것 | "할 수 있는 일 목록"만 | 공통 필드·코드 + 빈칸(abstract) |
| 여러 개 상속 | ⭕ (`: MonoBehaviour, ISkillInputInterceptor`) | ❌ 하나만 |
| 우리 예 | `ISkillContext`, `ISkillInputInterceptor`, `IPassive` | `SkillData`, `PassiveData`, `SkillStateBase` |
| 언제 | "이 능력이 있니?"만 중요할 때 | 자식들이 공통 코드를 공유할 때 |

---

## 3. struct vs class — "복사본 vs 주소"

### 비유
- **class** = 구글 문서 **링크** 공유. 친구가 고치면 내 화면에도 바뀜 (같은 걸 가리킴).
- **struct** = 문서를 **복사해서** 줌. 친구가 고쳐도 내 건 그대로.

```csharp
SlotInfo a = hud.GetSlot(SkillSlotId.Weapon);
SlotInfo b = a;          // struct → b는 a의 "복사본"
```

### 우리 코드의 struct들

| struct | 위치 | 왜 struct? |
|---|---|---|
| `SlotInfo` | [PlayerHudSource.cs:32](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:32) | UI에 "지금 이 순간의 값"을 넘기는 **스냅샷**. UI가 이걸 고쳐도 스킬에 영향 0 |
| `SkillCooldownState` | [SkillCooldownState.cs](../Assets/Members/HJH/02.Scripts/Char/FSM_Skill_Module/SkillCooldownState.cs) | 쿨다운을 바닥에 놓을 때 찍어두는 **사진** |
| `SkillUsedInfo` | SkillUsedInfo.cs | 이벤트로 넘기는 작은 꾸러미 |
| `Recipe`, `ComboInput` | ElementComboBook / ElementComboChain | 콤보 하나, 입력 하나 |

**`readonly struct`** = 만든 뒤 값을 못 바꾼다. "스냅샷은 바뀌면 안 된다"를 컴파일러가 강제.

**struct가 좋은 경우:** 작고(필드 몇 개), 값 자체가 의미이고, 자주 만들고 버릴 때. class는 힙에 할당돼 가비지 컬렉션(GC) 부담이 있는데 struct는 보통 그게 없다.
**struct가 나쁜 경우:** 크거나, 여럿이 같은 걸 공유·수정해야 할 때.

---

## 4. static — "모두가 하나를 같이 쓴다"

### 비유
반 학생 30명(인스턴스)이 각자 필통은 따로 있지만, **교실 칠판(static)** 은 하나다.

```csharp
// HitFeel.cs — 화면 흔들림 값은 게임에 하나뿐
public static float ShakeAmplitude { get; private set; }
public static void Play(float hitStop, float shake) { ... }

// 어디서든 객체 없이 바로 호출
HitFeel.Play(0.04f, 0.25f);
```

### 싱글턴 패턴
"이 클래스의 객체는 딱 하나만, 어디서든 `Instance`로 꺼내 쓴다."

```csharp
// PlayerHudSource.cs:76 — 없으면 찾고, 그래도 없으면 만든다 (lazy)
public static PlayerHudSource Instance
{
    get
    {
        if (_instance != null) return _instance;
        PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
        ...
        _instance = agent.GetComponent<PlayerHudSource>() ?? agent.gameObject.AddComponent<PlayerHudSource>();
        return _instance;
    }
}
```

| 장점 | 단점 (면접에서 꼭 같이 말하기) |
|---|---|
| 인스펙터 연결 없이 어디서든 접근 | 누가 언제 바꾸는지 추적 어려움 (전역 변수와 비슷) |
| 객체가 하나임이 보장 | 테스트할 때 가짜로 바꾸기 어려움 |
| | 플레이를 다시 시작해도 static 값이 남을 수 있음 → 02장 "RuntimeInitializeOnLoadMethod" |

우리는 "플레이어 HUD 창구는 진짜로 하나"라서 썼다. 남발하지 않는 게 원칙.

---

## 5. 자주 나오는 짧은 문법

| 문법 | 뜻 | 우리 코드 예 |
|---|---|---|
| `a?.B()` | a가 null이면 아무것도 안 함 | `_passive?.Tick(deltaTime);` |
| `a ?? b` | a가 null이면 b | `GetComponentInParent<PlayerAgent>() ?? FindFirstObjectByType<PlayerAgent>()` |
| `a ??= new X()` | a가 null일 때만 만들어 넣기 | `_block ??= new MaterialPropertyBlock();` (SkillPickup) |
| `SkillSlotId?` | null도 담을 수 있는 enum ("조준 중인 슬롯 없음") | `private SkillSlotId? _aimingSlot;` |
| `out` 파라미터 | 함수가 값을 여러 개 돌려줄 때 | `TryGetElement(out ElementType element)` → 성공 여부 + 원소 |
| `Try...` 이름 규칙 | 실패할 수 있으면 bool 반환 + out | `TryGetNextCombo`, `TryFindComboAtEnd` |
| `=>` (식 본문) | 한 줄짜리 함수/프로퍼티 | `public bool IsEmpty => Skill == null;` |
| `is` 패턴 | 타입 확인 + 변수 선언 한 번에 | `if (Machine.Current is GenericSkillState running)` |
| `IReadOnlyList<T>` | "읽기만 해" 리스트 | `ComboInputs` — UI가 콤보 입력을 지우지 못하게 |

---

## 6. 중첩 private 클래스 — "숨겨둔 도우미"

```csharp
// SkillStateModule.cs:407
private sealed class InterceptedInput : ISkillInputSource { ... }
```

`SkillStateModule` 안에서만 쓰는 도우미라서 밖에서 안 보이게 숨겼다. `sealed` = 더 이상 상속 금지.
스킬 데이터의 `Execution` 클래스들도 같은 방식(03장).

---

## 🎤 면접에서 이렇게 물으면

**Q. 이벤트를 왜 썼나요? 그냥 함수를 직접 부르면 안 되나요?**
> 직접 부르면 스킬 모듈이 콤보·HUD·원소 시스템을 전부 알아야 해서, 기능을 추가할 때마다 스킬 모듈을 고쳐야 합니다.
> 이벤트로 바꾸니 스킬 모듈은 "썼다"고만 알리고, 필요한 시스템이 알아서 구독합니다. 실제로 콤보와 HUD를 추가할 때 스킬 모듈은 수정하지 않았습니다.
> 대신 구독 해제를 빠뜨리면 파괴된 객체가 호출되는 문제가 있어서, 구독한 생명주기 함수의 짝(OnEnable↔OnDisable, Start↔OnDestroy)에서 반드시 해제했습니다.

**Q. struct와 class 차이?**
> class는 참조라 여럿이 같은 객체를 가리키고, struct는 값이라 대입하면 복사됩니다.
> 저는 UI에 넘기는 스킬 슬롯 정보(SlotInfo)를 readonly struct로 만들었는데, UI가 받은 값을 바꿔도 게임 로직에 영향이 없는 "그 순간의 사진"이어야 했기 때문입니다.

---

## ✅ 스스로 확인 질문

1. `SkillUsed?.Invoke(info)`에서 `?.`를 빼면 언제 에러가 나나?
2. `event` 키워드를 빼고 그냥 `public Action<SkillUsedInfo> SkillUsed;`로 하면 밖에서 무슨 나쁜 짓을 할 수 있게 되나?
3. `SlotInfo`가 class였다면, UI가 `info.CooldownRemaining`을 바꿀 수 있을 때 어떤 일이 생길 수 있나? (힌트: 지금은 readonly라 못 바꿈)
4. 싱글턴의 단점 2개를 말해보기.
5. `TryGetElement`는 왜 원소를 그냥 return하지 않고 `out`을 쓰나?
