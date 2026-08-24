# 무기 시스템 (챔피언 기반)

## 구조

```
WeaponData (추상 베이스 - 공통 필드: 이름, 역할(WeaponType), 쿨다운 등)
 ├─ TankerWeaponData      → TankerWeapon        (근접+원거리, 모드 토글)
 ├─ LaserDealerData       → LaserDealerWeapon   (조준 토글, 동일 타겟 지속 시 데미지 램프업)
 ├─ GunDealerData         → GunDealerWeapon     (연사, 탄 튀김 스프레드, 조준)
 ├─ BowData               → BowWeapon           (메인딜러 - 기계식 활, 홀드 충전+릴리즈 발사)
 ├─ MeleeSawedOffData     → MeleeSawedOffWeapon (서브딜러 - 근접+소드오프)
 ├─ SniperSawedOffData    → SniperSawedOffWeapon (서브딜러 - 저격+소드오프)
 └─ HealerData            → HealerWeapon        (힐샷 + 힐/딜 겸용 수류탄)
```

`WeaponType`(Tanker/MainDealer/SubDealer/Healer)은 라인 배정용 "역할" 구분으로만 쓰이고,
실제 어떤 챔피언인지는 `WeaponData`의 **구체 타입 자체**로 결정됨. `WeaponFactory`가 타입
패턴매칭으로 알맞은 무기 클래스를 생성.

## 조준점 / 총구점 분리 (크로스헤어 수렴)

`IWeaponOwner`는 `AimOrigin`(카메라 중앙, 실제 판정용)과 `MuzzleOrigin`(무기 모델의 실제 총구,
시각효과 전용) 두 지점을 따로 제공함. 화면 구석에 무기가 보여도 판정은 크로스헤어 기준으로
나가고, 투사체/빔의 시각적 시작점만 무기 위치에서 그 판정 지점을 향하도록 회전시켜서
"크로스헤어에서 일직선으로 나가는 것처럼" 보이게 만드는 FPS 표준 트릭.
`AimUtility.GetConvergedMuzzleRotation()`이 이 계산을 담당하며, 건딜러/탱커 원거리모드/
힐샷이 이 방식을 씀. 레이저는 판정 자체가 레이캐스트라 `AimOrigin`에서 직접 쏘고, 빔 시각화만
`MuzzleOrigin`에서 시작해서 판정 지점까지 그림. 근접류(궤적 판정)와 수류탄(포물선)은 이 트릭이
필요 없어서 그대로 `AimOrigin`/`MuzzleOrigin` 각각의 forward만 사용.

## 약점 (Weakpoint)

- 유니티에서 직접: `Edit > Project Settings > Tags and Layers`에서 `Enemy`, `Weakpoint` 레이어
  두 개를 만들어야 함 (자동화 안 됨). 몹의 약점 부위 콜라이더만 `Weakpoint` 레이어로 두면 됨.
- `Projectile`/`WeaponHitbox`/`LaserDealerWeapon`이 맞은 콜라이더의 레이어를 체크해서
  `IWeaponOwner.ApplyDamageTo(target, amount, isWeakpoint)`로 전달.
- `HitFeedback.Play(isWeakpoint)`가 그 값에 따라 마커 색(`Normal Color`/`Weakpoint Color`)과
  사운드(`Normal Hit Sound`/`Weakpoint Hit Sound`)를 다르게 재생.
- `Test Scene Setup`으로 더미 배치 시 각 더미 위쪽에 작은 `Weakpoint` 자식 콜라이더가 자동으로
  붙어서 바로 테스트 가능 (해당 레이어가 프로젝트에 없으면 콘솔에 경고만 뜨고 무시됨).

## 타격감 (Hit Feedback)

- `HitFeedback` — 히트마커 UI 펄스 + 사운드. `SampleWeaponOwner.ApplyDamageTo()` 한 곳에서만
  호출하므로 총(발당 1회)이든 레이저(연속 틱)든 똑같이 커버됨. `minInterval`로 연속 타격 시
  과도한 반복을 막아 "두구두구" 리듬감을 만듦.
- `MuzzleFlash` — 총구 파티클/라이트 재생. 프리팹은 직접 갖고 계신 걸 `Muzzle Flash` 필드에
  연결하면 됨 (테스트 툴이 자동 생성은 안 함).
- `CinemachineImpulseSource` — 건은 발당, 에너지볼은 더 센 힘으로 화면 흔들림. **실제 게임
  카메라(가상 카메라)에 `CinemachineImpulseListener`를 붙여야 눈에 보임** - 테스트 씬엔
  진짜 카메라가 없어서 이 부분은 직접 세팅 필요.
- 무기 모델 반동(킥백)은 플레이어 애니메이션이 담당하는 영역이라 이 시스템에서는 다루지 않음.

## 히트 이펙트 (몹마다 다르게)

- `IHitEffectSource` 인터페이스 — `HitEffectPrefab` 하나만 노출. 몹/오브젝트마다 이 인터페이스를
  구현하고 자기만의 이펙트 프리팹(피, 철파편 등)을 들고 있으면 됨. `TestDummy`가 임시로 스파크
  파티클을 들고 구현 예시로 되어있음.
- `Projectile`이 맞았을 때 `GetComponentInParent<IHitEffectSource>()`로 대상의 이펙트를 찾아서,
  `Collider.ClosestPoint()`로 근사 표면 방향을 구해 그 방향으로 스폰. 대상이 이 인터페이스를
  구현 안 했으면 `Projectile`의 `Default Hit Effect Prefab`(폴백)을 사용.
- 새 몹 타입 추가 시: `IHitEffectSource` 구현 + 인스펙터에 프리팹 하나 꽂기만 하면 끝.

## 탄약 / 재장전

건딜러·레이저는 기존 `CurrentResource`/`MaxResource`를 탄약(건은 발당 1씩 차감) / 배터리
(레이저는 초당 소모)로 사용함. 다 떨어지면 `PrimaryAttack`이 아예 안 먹힘. `R`키로
`Reload()` 호출 → `WeaponData.reloadDuration`만큼 대기 후 가득 참 (재장전 중엔 발사 불가).
`SampleWeaponOwner`의 `Ammo Text`(UI Text)가 매 프레임 `현재/최대` 또는 "재장전 중..."을 표시.
`Test Scene Setup`에서 건/레이저 배치 시 캔버스+텍스트(+레이저는 게이지 슬라이더까지) 자동 생성.

## 소드오프 펠릿 시각 표현

발로란트 쇼티 참고 - 한 발에 펠릿 20개(`pelletCount`). 판정은 즉시 레이캐스트로 확정하고,
`DumbBulletVisual`(판정 없는 순수 시각용 총알)을 총구에서 각 펠릿이 맞은 지점을 향해 따로
날려서 눈에 보이게 함. 거리 비례 데미지: 가까울수록 1배, 사거리(`shotgunRange`) 끝에 가까울수록
`damageFalloffAtMaxRange`(기본 0.7배)까지 선형 감소. 맞은 대상의 `IHitEffectSource` 프리팹
(없으면 `defaultHitEffectPrefab`)이 `hit.normal` 방향으로 스폰됨.

## 새 챔피언 추가하는 법

1. `Data/`에 `WeaponData`를 상속하는 새 데이터 클래스 작성 (`[CreateAssetMenu]` 필수)
2. `Implementations/`에 `WeaponBase`를 상속하는 새 무기 클래스 작성
3. `WeaponFactory.cs`의 switch에 케이스 한 줄 추가

인스펙터는 손댈 필요 없음 - SO 서브클래스라 유니티가 필드를 자동으로 그려줌.

## 챔피언별 판정 방식

- **탱커**: `TankerWeaponData.mode`(MeleeOnly/RangedOnly/Both)로 근접·원거리 중 뭘 쓸지
  인스펙터에서 토글. 근접은 궤적 판정(`WeaponHitbox`), 원거리는 투사체.
- **메인딜러 - 레이저**: `PrimaryIsHeld = true`라 좌클릭을 누르고 있는 동안만 발사(뗴면 즉시 정지,
  토글 아님). 켜진 동안 `Tick()` 대신 `PrimaryAttack()` 자체에서 매 프레임 레이캐스트 판정.
  같은 타겟을 계속 맞추면 `rampUpRate`만큼 데미지 배율 상승(`maxRampMultiplier`까지). 맞출
  때마다 게이지가 차고(`gaugeChargePerSecond`), 가득 차면 자동으로 에너지볼(투사체) 발사 후
  게이지 리셋 - 버튼 입력 없이 자동 발동. `SecondaryAction`은 조준 토글(이벤트만 발행).
- **메인딜러 - 건**: `PrimaryIsHeld = true`라 버튼을 누르고 있으면 `fireRate`에 따라 연사.
  쏠수록 `currentSpread`가 커졌다가(탄 튀김) 안 쏘면 서서히 회복. `SecondaryAction`(조준)은
  스프레드를 `aimSpreadMultiplier`만큼 줄여줌.
- **메인딜러 - 활**: 홀드 충전형. `IWeapon`에 새로 추가된 `OnPrimaryPressed`/`OnPrimaryHeld`/
  `OnPrimaryReleased` 훅을 씀 (기존 PrimaryAttack 방식과 별개, 다른 무기는 전부 no-op).
  누르면 충전 시작, 떼면 발사 - 충전 비율(`ChargeRatio`)에 비례해서 데미지/발사속도(사거리)가
  `Lerp`로 늘어남. 발사 최소 간격 1초(`minFireInterval`). 충전 비율은 `Bow Charge Slider` UI로
  표시됨. `ArrowProjectile`은 다른 무기들의 즉발/무중력 투사체와 다르게 **실제 Rigidbody 중력**을
  받아서 포물선을 그림. 맞으면 그 대상(바닥이든 캐릭터든)에 **자식으로 붙어서 계속 박혀있음**
  (움직이는 대상이면 같이 따라감, 대상이 파괴되면 화살도 같이 정리됨). 풀차지 상태
  (`IsFullyDrawn`)일 때 무기 모델이 퍼린노이즈로 미세하게 떨림. 무기 모델은 총과 구분되게
  세로로 긴 원기둥(대충 활 몸통 형태)으로 생성됨.
- **서브딜러 - 근접+소드오프**: 마우스 휠로 칼↔소드오프 샷건 전환. 좌클릭이 현재 모드 발사
  (칼=궤적 판정 스윙, 샷건=펠릿 레이캐스트 다발). 샷건은 2발 탄창, R로 장전. 칼/샷건 로직은
  `SawedOffShotgunModule`(공용 모듈)을 조합해서 씀 - 저격+소드오프도 이 모듈 재사용.
- **서브딜러 - 저격+소드오프**: 마우스 휠로 저격총↔소드오프 샷건 전환. 좌클릭=현재 모드 발사,
  우클릭=줌(저격 모드에서만 동작 - 줌 안 하면 스프레드 큼, 줌하면 정확). 저격/샷건 각각 별도
  탄창 + R로 각자 장전. 발로란트 마샬 참고 수치(오퍼레이터보다 연사 빠름/줌 배율 낮음/스코프인
  빠름). 줌 중엔 카메라 FOV가 좁아지고 퍼린 노이즈로 살짝 흔들림(스코프 숨쉬기 효과), 화면엔
  `Scope Overlay`(placeholder Image, 실제 스코프 텍스처로 교체 권장)가 뜨고 무기 모델이 화면
  중앙(ADS 위치)으로 순간 이동함(토글식, 보간 없음). 발사 시점의 총구~조준점 맞은 지점을
  `TracerVisual`(LineRenderer)이 짧게(기본 0.05초) 반짝여서 보여줌. 쏘면 자동으로 줌이 풀리고
  `postFireZoomLockDuration`(기본 0.3초) 동안 재발사/재줌이 잠깐 막힘.
- **힐러**: `SecondaryAction`(우클릭)은 공격이 아니라 **장전 모드 전환** - 힐샷 ↔ 힐/딜 수류탄
  을 스왑. `PrimaryAttack`(좌클릭)이 실제 발사이고, 현재 모드에 따라 힐샷(아군 즉시 회복) 또는
  수류탄(터지면 반경 안 대상을 `IHealable`/`IDamageable` 여부로 각각 회복·피해)이 나감.
  힐을 받으면(`IHealable.Heal` 호출 시) `HealScreenEffect`(URP Volume 기반)가 화면 비네트를
  초록빛으로 펄스시켜 회복 피드백을 줌 - 로컬 플레이어 카메라 전용 연출이라 `SampleWeaponOwner`
  가 `IHealable`을 구현해서 자기 자신의 `Heal()` 안에서 트리거함.

## 테스트

`Tools > Robot Weapons > Test Scene Setup`에서 챔피언 선택 후 배치, Play 눌러서
좌클릭=Primary/우클릭=Secondary로 테스트. 애니메이터 없이도 판정 확인 가능하도록
`SampleWeaponOwner`에 디버그 타이밍 시뮬레이션이 들어있음 (실제 애니메이션 연결 시 꺼둘 것).

## 플레이어 담당자가 할 일

1. `IWeaponOwner` 구현 (`Sample/SampleWeaponOwner.cs` 참고)
2. `WeaponFactory.Create(weaponData, savedUpgrades)`로 무기 생성 후 `Equip()`
3. 입력 시 `PrimaryAttack()` / `SecondaryAction()` 호출, 매 프레임 `Tick()` 호출
4. Animation Event 연결: 근접류는 `Anim_SwingStart`/`Anim_SwingEnd`, 건 계열은 `Anim_ExecuteHit`
