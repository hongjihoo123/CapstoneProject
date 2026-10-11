# 02. Unity 기초 — 우리 코드에 나오는 것만

> 난이도 🟢 · 1시간 · [← 목차](README.md)

---

## 1. MonoBehaviour 생명주기 — "출근부터 퇴근까지"

### 비유
회사원 하루: 입사(Awake) → 출근(OnEnable) → 첫 업무 시작(Start) → 매일 일(Update) → 퇴근 정리(LateUpdate) → 휴가(OnDisable) → 퇴사(OnDestroy)

```
씬 로드
  │
  ▼
Awake()        ← 나 자신 준비. "내 컴포넌트 가져오기"           (모든 오브젝트의 Awake가 먼저 다 끝남)
  │
OnEnable()     ← 켜질 때마다. 이벤트 구독(+=) 하기 좋은 곳
  │
Start()        ← 첫 Update 직전 1번. "다른 오브젝트" 참조하기 좋은 곳 (다들 Awake 끝났으니까)
  │
┌─▶ Update()       ← 매 프레임. 입력, 게임 로직
│   LateUpdate()   ← 매 프레임, 모든 Update 뒤. 카메라·UI 그리기
└── (반복)
  │
OnDisable()    ← 꺼질 때마다. 이벤트 해제(-=)
  │
OnDestroy()    ← 파괴될 때 1번. Start/Bind에서 한 구독 해제
```

### 우리 코드에서
| 함수 | 예 | 왜 거기서? |
|---|---|---|
| Awake | `SkillSwapInteractor.Awake`: `player = GetComponentInParent<PlayerAgent>()` | 내 부모 찾기는 나 혼자 할 수 있음 |
| Start | `SkillSwapInteractor.Start`: `player.SkillFsm`을 가져와 `AddInputInterceptor(this)` | SkillFsm은 **PlayerAgent의 Awake에서** 만들어짐 → 내 Awake 시점엔 아직 없을 수도 |
| LateUpdate | `TopDownCameraFollow`, `AimCursorView`, `SkillPickup`(빌보드) | 플레이어가 이번 프레임에 다 움직인 **뒤에** 카메라/UI를 맞춰야 안 떨림 |
| OnDestroy | `PlayerHudSource.OnDestroy`: 모든 `-=` | 구독을 Bind(=Start 시점)에서 했으니까 |

### ⚠️ "Start 순서는 보장되지 않는다"
A의 Start가 B의 Start보다 먼저 불린다는 보장이 없다. 그래서 `PlayerHudSource`는 **Lazy Bind**를 쓴다:
"누가 나한테 값을 물어보는 순간, 아직 연결 안 됐으면 그때 연결한다" ([PlayerHudSource.cs:211](../Assets/Members/HJH/02.Scripts/Hud/PlayerHudSource.cs:211)). 자세한 건 04장.

---

## 2. ScriptableObject — "설정 파일"

### 비유
게임 캐릭터 **스탯 카드**. 카드 자체는 안 싸운다. 카드에 적힌 숫자를 보고 캐릭터가 싸운다.

- `MonoBehaviour` = 씬 안의 오브젝트에 붙는 **배우**.
- `ScriptableObject` = 프로젝트 폴더에 `.asset` 파일로 저장되는 **대본/설정 카드**.

우리 ScriptableObject: `SkillData`(스킬 하나의 수치), `WeaponKitData`(캐릭터 스킬 묶음), `PassiveData`, `ElementPalette`(원소 색), `FxLibrary`, `CharacterData`.

### 왜 쓰나
- 기획자가 **코드 없이** 인스펙터에서 쿨다운·데미지를 바꿀 수 있다.
- 같은 에셋을 여러 곳이 공유 (스킬 하나를 바닥에도, 슬롯에도).

### ⚠️ 가장 중요한 규칙: "에셋에 진행 상태를 넣지 마라"
ScriptableObject는 **모든 사용처가 같은 파일 하나를 공유**한다. 에디터에선 플레이를 멈춰도 바뀐 값이 남는다.
→ "이번 시전에서 몇 번 때렸나" 같은 값은 에셋이 아니라 **매 시전마다 새로 만드는 객체**에 둔다 (03장 Execution).

---

## 3. [SerializeField] — "인스펙터에 보여줘"

```csharp
[SerializeField] private float swapLockout = 1f;   // private인데 인스펙터에서 수정 가능
```

- `public`으로 열면 **아무 코드나** 바꿀 수 있게 된다. `[SerializeField] private`는 "인스펙터에선 바꾸되 코드에선 나만" → **캡슐화 유지**.
- Unity가 이 값을 씬/에셋 파일에 **저장(직렬화)** 한다.

### ⚠️ 함정: Unity가 직렬화하는 객체 안에서만 동작
`MonoBehaviour`, `ScriptableObject`, 또는 그 안에 들어간 `[Serializable]` struct/class에서만 의미가 있다.
코드에서 `new`로 만든 일반 클래스(`SkillStateBase` 같은)에 `[SerializeField]`를 붙여도 **인스펙터에 영원히 안 나온다** → 항상 null. (실제로 팀원 코드에서 이 실수가 있었음, 08장)

### 인스펙터에서 비워둬도 되는 이유
`PlayerHudSource`의 필드들은 툴팁에 "All optional"이라고 써 있다. 비어 있으면 코드가 `FindFirstObjectByType`으로 **스스로 찾는다**.
→ 씬에 드래그 연결이 없으면 **씬 파일이 덜 바뀌어서 병합 충돌이 줄어든다** (04장).

---

## 4. Resources 폴더 — "이름으로 꺼내 쓰는 창고"

```csharp
// ElementPalette.cs:15
public static ElementPalette Default =>
    _default != null ? _default : _default = Resources.Load<ElementPalette>("HJH/ElementPalette");
```

- 프로젝트 안 아무 `Resources` 폴더에 둔 에셋은 **경로 문자열로** 불러올 수 있다. 우리 팔레트: `Data/Resources/HJH/ElementPalette.asset`.
- 장점: 인스펙터 연결 0개. 어떤 씬에서든 같은 팔레트.
- 단점: Resources 안의 것은 **안 써도 빌드에 다 들어간다**, 경로를 문자열로 쓰니 오타는 실행해봐야 앎. → 우리처럼 "전역 설정 1~2개"에만 쓰고, 많은 에셋엔 Addressables를 쓴다.
- `_default`에 한 번 저장(캐싱)해서 매번 로드하지 않는다.

---

## 5. 프리팹 vs 씬 — "왜 씬은 같이 못 고치나"

| | 씬 (`.unity`) | 프리팹 (`.prefab`) |
|---|---|---|
| 뭔가 | 한 판의 무대 전체 | 재사용하는 오브젝트 묶음 |
| 파일 | 하나에 수천 줄 YAML | 작은 파일 하나 |
| 두 명이 동시에 수정 | **git 병합 거의 불가능** (ID·순서가 얽힘) | 서로 다른 프리팹이면 충돌 없음 |

**교훈:** 팀 작업에서는 "씬에는 프리팹만 놓고, 실제 구성은 프리팹 안에서". 내 HUD는 내 프리팹, 팀원 UI는 팀원 프리팹 → 씬 파일을 동시에 안 건드린다.
그리고 코드가 참조를 스스로 찾게 하면(위 3번) 씬에 저장되는 연결 정보 자체가 줄어든다.

---

## 6. Time.timeScale과 unscaled 시간

| | 의미 | 히트스톱(timeScale 0.05) 중 |
|---|---|---|
| `Time.deltaTime` / `Time.time` | 게임 시간 | 거의 멈춤 (5% 속도) |
| `Time.unscaledDeltaTime` / `unscaledTime` | 현실 시간 | 정상 속도 |

- 게임 로직(스킬, 콤보 타이머) = scaled → 히트스톱 때 같이 멈춰서 "묵직함".
- UI 연출 = unscaled → 히트스톱 중에도 배너가 부드럽게 들어옴. DOTween에선 `.SetUpdate(true)`가 같은 뜻 (07장).

---

## 7. Application.isFocused — "게임 창을 보고 있나?"

```csharp
// AimCursorView.cs:173
SetShown(mouse != null && alive && !overUi && Application.isFocused);
```

- 게임 창이 **활성(포커스) 상태**면 true. 알트탭으로 다른 창을 보면 false.
- 우리는 커스텀 조준 커서를 그리고 진짜 마우스 커서는 숨긴다. 그런데 알트탭했는데도 커서가 계속 숨어 있으면 사용자는 **다른 프로그램에서 마우스가 사라진 것처럼** 느낀다.
  → 포커스를 잃으면 조준 커서를 숨기고 OS 커서를 돌려준다.
- 비슷한 조건들: 죽었을 때(`alive`), UI 위에 마우스가 있을 때(`overUi`) → 그때는 조준 커서 대신 일반 커서.

---

## 8. [RuntimeInitializeOnLoadMethod] + 도메인 리로드

### 문제 상황
static 변수는 "프로그램이 켜져 있는 동안" 유지된다. Unity 에디터에서 Play를 눌렀다 멈췄다 다시 누르면?

```
Unity 설정: Edit > Project Settings > Editor > Enter Play Mode Settings
 ├─ Reload Domain 켜짐 (기본) → Play 누를 때마다 C# 전체를 새로 로드 → static 전부 초기화 ✅ 대신 Play 진입이 느림
 └─ Reload Domain 꺼짐       → Play가 빨라짐 ⚡ 대신 static이 이전 플레이 값을 그대로 들고 있음 ❌
```

예: `PlayerHudSource._instance`가 지난 플레이의 **이미 파괴된 객체**를 들고 있으면 → `Instance`가 망가진 걸 돌려줌.

### 해결: 플레이 시작 직전에 직접 비우기

```csharp
// PlayerHudSource.cs:321
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics() => _instance = null;
```

- `SubsystemRegistration` = Play 시작 과정 중 **가장 이른 시점**(어떤 Awake보다도 먼저). 여기서 static을 초기화.
- 우리 프로젝트에 이게 **9곳**: `PlayerHudSource`, `HitFeel`, `FxLibrary`, `FxPrefabInstance`, `FxRunner`, `PunchFx`, `CursorService`, `UiFocusService`, `AttackAreaBus`.

### 우리 프로젝트는?
`ProjectSettings/EditorSettings.asset`에 `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0` → "옵션은 켰지만 아무 리로드도 끄지 않음" = **도메인 리로드 켜짐**으로 추정. 그래서 지금은 static이 매번 초기화된다.
→ 그래도 9곳의 리셋은 **안전장치로 유지 권장**. 누가 Play 속도 때문에 리로드를 끄는 순간 바로 필요해진다.
→ ⚠️ JJH의 `EventChannelSO`는 ScriptableObject 안의 리스너 딕셔너리(`_events`, `_lookup`)를 갖고 있다. 리로드를 끄면 **지난 플레이의 구독자(파괴된 객체)** 가 남아 이벤트 때 에러가 날 수 있다. 리로드를 끈다면 이것부터 처리해야 함.

---

## 🎤 면접에서 이렇게 물으면

**Q. Awake와 Start의 차이는?**
> Awake는 오브젝트가 로드되면 바로, Start는 첫 프레임 직전에 불립니다. 모든 Awake가 끝난 뒤 Start들이 불리므로,
> 저는 "내 컴포넌트 준비"는 Awake, "다른 오브젝트가 Awake에서 만든 것을 가져오기"는 Start에 둡니다.
> 예를 들어 스킬 교체 스크립트는 PlayerAgent가 Awake에서 만드는 스킬 모듈을 Start에서 가져옵니다.
> 그래도 Start끼리 순서는 보장되지 않아서, HUD 창구는 처음 값을 요청받을 때 연결하는 지연 연결(lazy bind)로 순서 문제를 없앴습니다.

**Q. ScriptableObject를 쓸 때 주의할 점은?**
> 모든 사용처가 같은 에셋을 공유하기 때문에 런타임 상태를 넣으면 안 됩니다. 저는 스킬 수치만 에셋에 두고, 시전 중 상태는 시전할 때마다 새로 만드는 실행 객체에 뒀습니다.

**Q. static 변수 쓸 때 Unity에서 주의할 점은?**
> Enter Play Mode 설정에서 도메인 리로드를 끄면 static이 플레이 사이에 초기화되지 않습니다.
> 그래서 RuntimeInitializeOnLoadMethod(SubsystemRegistration)로 플레이 시작 시 static을 비우는 함수를 두었습니다.

---

## ✅ 스스로 확인 질문

1. `SkillSwapInteractor`가 `SkillFsm`을 Awake가 아니라 Start에서 가져오는 이유는?
2. 카메라 추적을 Update가 아니라 LateUpdate에서 하는 이유는?
3. 일반 C# 클래스에 `[SerializeField]`를 붙이면 왜 의미가 없나?
4. 히트스톱 중에 콤보 배너가 멈추지 않게 하려면 어떤 시간을 써야 하나?
5. 도메인 리로드를 끄면 `PlayerHudSource._instance`에 무슨 일이 생기나? 그걸 막는 코드는 어디 있나?
