# 무기 시스템 (챔피언 기반)

## 구조

```
WeaponData (추상 베이스 - 공통 필드: 이름, 역할(WeaponType), 쿨다운 등)
 ├─ TankerWeaponData      → TankerWeapon        (근접+원거리, 모드 토글)
 ├─ LaserDealerData       → LaserDealerWeapon   (조준 토글, 동일 타겟 지속 시 데미지 램프업)
 ├─ GunDealerData         → GunDealerWeapon     (연사, 탄 튀김 스프레드, 조준)
 ├─ MeleeSubDealerData    → MeleeSubDealerWeapon(서브딜러 - 근접 전용)
 └─ HealerData            → HealerWeapon        (힐샷 + 힐/딜 겸용 수류탄)
```

`WeaponType`(Tanker/MainDealer/SubDealer/Healer)은 라인 배정용 "역할" 구분으로만 쓰이고,
실제 어떤 챔피언인지는 `WeaponData`의 **구체 타입 자체**로 결정됨. `WeaponFactory`가 타입
패턴매칭으로 알맞은 무기 클래스를 생성.

## 새 챔피언 추가하는 법

1. `Data/`에 `WeaponData`를 상속하는 새 데이터 클래스 작성 (`[CreateAssetMenu]` 필수)
2. `Implementations/`에 `WeaponBase`를 상속하는 새 무기 클래스 작성
3. `WeaponFactory.cs`의 switch에 케이스 한 줄 추가

인스펙터는 손댈 필요 없음 - SO 서브클래스라 유니티가 필드를 자동으로 그려줌.

## 챔피언별 판정 방식

- **탱커**: `TankerWeaponData.mode`(MeleeOnly/RangedOnly/Both)로 근접·원거리 중 뭘 쓸지
  인스펙터에서 토글. 근접은 궤적 판정(`WeaponHitbox`), 원거리는 투사체.
- **메인딜러 - 레이저**: `PrimaryAttack`으로 토글, 켜진 동안 `Tick()`에서 레이캐스트로 초당 피해.
  같은 타겟을 계속 맞추면 `rampUpRate`만큼 데미지 배율이 올라감(`maxRampMultiplier`까지).
  `SecondaryAction`은 조준 토글(`Laser_AimStart/End` 이벤트만 발행, 로직 영향 없음 - 카메라 FOV
  등 외부 시스템이 반응하도록). 빔 시각화는 `LaserBeamVisual`(LineRenderer)이 매 프레임
  `BeamEndPoint`를 읽어서 그림 - 라인 렌더러 채택 이유는 매 프레임 시작~타격점을 갱신해야
  하는 빔 특성상 파티클보다 훨씬 가볍고 다루기 쉬움.
- **메인딜러 - 건**: `PrimaryIsHeld = true`라 버튼을 누르고 있으면 `fireRate`에 따라 연사.
  쏠수록 `currentSpread`가 커졌다가(탄 튀김) 안 쏘면 서서히 회복. `SecondaryAction`(조준)은
  스프레드를 `aimSpreadMultiplier`만큼 줄여줌.
- **서브딜러**: 궤적 판정(방식 B) 근접 스윙 콤보.
- **힐러**: `SecondaryAction`(우클릭)은 공격이 아니라 **장전 모드 전환** - 힐샷 ↔ 힐/딜 수류탄
  을 스왑. `PrimaryAttack`(좌클릭)이 실제 발사이고, 현재 모드에 따라 힐샷(아군 즉시 회복) 또는
  수류탄(터지면 반경 안 대상을 `IHealable`/`IDamageable` 여부로 각각 회복·피해)이 나감.

## 테스트

`Tools > Robot Weapons > Test Scene Setup`에서 챔피언 선택 후 배치, Play 눌러서
좌클릭=Primary/우클릭=Secondary로 테스트. 애니메이터 없이도 판정 확인 가능하도록
`SampleWeaponOwner`에 디버그 타이밍 시뮬레이션이 들어있음 (실제 애니메이션 연결 시 꺼둘 것).

## 플레이어 담당자가 할 일

1. `IWeaponOwner` 구현 (`Sample/SampleWeaponOwner.cs` 참고)
2. `WeaponFactory.Create(weaponData, savedUpgrades)`로 무기 생성 후 `Equip()`
3. 입력 시 `PrimaryAttack()` / `SecondaryAction()` 호출, 매 프레임 `Tick()` 호출
4. Animation Event 연결: 근접류는 `Anim_SwingStart`/`Anim_SwingEnd`, 건 계열은 `Anim_ExecuteHit`
