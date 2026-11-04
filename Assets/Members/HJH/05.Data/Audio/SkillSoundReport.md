# 스킬 효과음 배치 결과 (2026-11-03)

시점 = 기준 순간 + 초. **Anim** = 몸 동작 클립 이벤트 `PlaySkillSound(이벤트)` (클립 안 시각. Cast_* 클립은 게임에서 손 뻗기 0.15s 전부터 틀어서, 실제로는 시전 0.15s 뒤에 울림) · **Fx** = 그 이펙트 칸이 터진 뒤 · **시전** = 스킬 쓴 뒤 · **충돌** = 범위 공격이 닿은 뒤

파일 위치: `Assets/Audio/SFX/Skills/` · ⚠ = 타이밍 추정이 애매했던 소리


### Earth_Boulder

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A heavy boulder hurled forward: deep stone grinding scrape into a low rumbling whoosh. (0.8s) | `Earth_Boulder_Release.wav` | Cast_Forward 손 끝 속도 급감 0.80s (CastMotions release 0.80과 일치) |
| 시전 +0.05s | A large rock rolling and tumbling fast across the ground, gravel crunching, about one second. (1s, 0.9s 유지) | `Earth_Boulder_Roll.wav` | ⚠ 비행시간 = 거리/11 (최대 0.9s) — 커서 거리마다 달라서 길이 고정이 애매 |
| hitFx +0s | A boulder slamming into a body: heavy stone thud followed by crumbling rock debris. (0.7s) | `Earth_Boulder_Hit.wav` | FX_Earth_PbrHit burst 0s |

### Earth_EarthSplitter

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A forceful earth magic thrust: short stone crack and low ground thump. (0.6s) | `Earth_EarthSplitter_Release.wav` | Cast_Forward 0.80s |
| impactFx +0s | A line of jagged stone spikes erupting from the ground in rapid sequence, rock ripping and cracking, rumbling tail. (1.2s) | `Earth_EarthSplitter_Eruption.wav` | 첫 impactFx(burst 0s)에서 한 번만 (minInt 0.5) |

### Earth_Earthquake

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.367s | A heavy foot stomp on stone ground: deep thump with a sharp earth crack. (0.7s) | `Earth_Earthquake_Stomp.wav` | Cast_Ground 엉덩이 하강 멈춤(착지) 1.37s |
| zoneFx +0s | A continuous earthquake: deep ground rumble, rocks rattling and cracking, steady shaking for three seconds then fading out. (3s, 3s 유지) | `Earth_Earthquake_Quake.wav` | zoneFx 반복 파티클 zoneTime 3s |

### Earth_EarthSmash

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A massive fist smash into the earth: explosive rock burst, stone spikes erupting, debris raining down. (1.2s) | `Earth_EarthSmash_Slam.wav` | ⚠ 손이 땅 치는 1.27s(게임 +0.15s) vs impactFx burst 0s — 0.15s 어긋나서 손 동작 쪽으로 맞춤 |

### Earth_GraspOfEarth

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.6s | A summoning magic gesture: low earthy rumble swelling up with a soft stone grind. (0.6s) | `Earth_GraspOfEarth_Release.wav` | ⚠ Cast_Sky 는 손 속도 급감이 없음(봉우리 없이 서서히) → CastMotions release 0.60 사용 |
| windupFx +0s | The ground cracking open with pebbles shaking, a short building rumble. (0.5s) | `Earth_GraspOfEarth_Rumble.wav` | windupFx burst 0s, 0.4s 뒤 손이 솟음 |
| impactFx +0s | Giant stone hands bursting out of the ground and clamping shut: rock crunch and grinding grip. (0.9s) | `Earth_GraspOfEarth_Grab.wav` | impactFx burst 0s (delay 0.4) |

### Earth_RockWall

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | A short forceful earth magic gesture with a low stone resonance. (0.5s) | `Earth_RockWall_Release.wav` | Cast_Self 0.63s |
| barrierFx +0s | A thick rock wall erupting from the ground: heavy grinding stone rising fast, crunch and dust. (1.2s) | `Earth_RockWall_Rise.wav` | barrierFx spikes burst 0s |

### Earth_StoneShield

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | Stone armor forming around a body: rocks clattering and locking together with a solid thunk. (0.8s) | `Earth_StoneShield_Release.wav` | Cast_Self 0.63s |
| impactFx +0s | A stone shield shattering outward: rock explosion blasting enemies away, debris scattering. (1s) | `Earth_StoneShield_Burst.wav` | explodeDelay 3s 뒤 impactFx burst 0s |

### Electric_ChainLightning

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A sharp electric discharge from the hand: crackling lightning zap release. (0.6s) | `Electric_ChainLightning_Release.wav` | Cast_Forward 0.80s |
| hitFx +0s | A quick lightning arc striking a target: snapping electric crack. (0.5s) | `Electric_ChainLightning_Hit.wav` | 적마다 hitFx (jumpDelay 0.08s 간격) — minInt 0.05 라 튈 때마다 울림 |

### Electric_ElectricNet

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | An electric net thrown: buzzing crackle with a quick whoosh. (0.5s) | `Electric_ElectricNet_Release.wav` | Cast_Forward 0.80s |
| impactFx +0s | An electric net snapping open and wrapping around enemies: three rapid zaps then a sustained buzzing crackle. (1s) | `Electric_ElectricNet_Snare.wav` | impactFx strikes 0/0.07/0.14s + current 1.2s |

### Electric_Flash

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| castFx +0s | An instant lightning teleport: sharp electric zap with a thunder snap. (0.5s) | `Electric_Flash_Depart.wav` | 순간이동이라 몸 동작 대신 castFx 0s |
| impactFx +0s | A lightning strike landing burst: electric explosion crackle. (0.8s) | `Electric_Flash_Arrive.wav` | 도착 impactFx burst 0s |

### Electric_LightningSpear

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A lightning spear thrown: electric charge snapping into a fast zapping whoosh. (0.6s) | `Electric_LightningSpear_Release.wav` | Cast_Forward 0.80s |
| hitFx +0s | A lightning spear piercing through a target: sharp electric crack. (0.5s) | `Electric_LightningSpear_Hit.wav` | 관통이라 폭발 대신 맞을 때마다 |

### Electric_Overcharge

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | An electric power-up surge: buzzing charge rising to a high-pitched energy hum with crackles. (1.5s) | `Electric_Overcharge_Release.wav` | Cast_Self 0.63s |

### Electric_StaticShell

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | An electric shield dome forming: crackling static field hum. (1s) | `Electric_StaticShell_Release.wav` | Cast_Self 0.63s |
| hitFx +0s | A quick electric counter shock zap. (0.5s) | `Electric_StaticShell_Shock.wav` | retaliate 5s 동안 때린 적에게 hitFx |

### Electric_Thunderbolt

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.6s | Electric charge gathering overhead: rising crackle. (0.5s) | `Electric_Thunderbolt_Release.wav` | ⚠ Cast_Sky 봉우리 없음 → release 0.60 |
| impactFx +0s | A lightning bolt striking the ground: loud thunder crack with a booming rumble tail. (1.2s) | `Electric_Thunderbolt_Strike.wav` | delay 0.25s 뒤 impactFx burst 0s |

### Water_BubblePrison

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A watery bubble launched: wobbly bloop with a splashy whoosh. (0.6s) | `Water_BubblePrison_Release.wav` | Cast_Forward 0.80s |
| impactFx +0s | A big water bubble engulfing a target: gloopy splash then a muffled underwater wobble. (0.9s) | `Water_BubblePrison_Trap.wav` | impactFx burst 0s |

### Water_HealingRain

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A water magic gesture with a sparkling splash. (0.6s) | `Water_HealingRain_Release.wav` | Cast_Ground 1.27s |
| zoneFx +0s | Gentle magical rain shower: soft raindrops with twinkling healing chimes, four seconds, fading out. (4s, 4s 유지) | `Water_HealingRain_Rain.wav` | zoneTime 4s |

### Water_WaterCannon

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A high-pressure water jet blast: powerful gushing spray. (1s) | `Water_WaterCannon_Release.wav` | Cast_Forward 0.80s |
| impactFx +0s | A water jet impact: heavy spray burst splash. (0.7s) | `Water_WaterCannon_Splash.wav` | 끝점 impactFx burst 0s |

### Water_WaterVeil

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | Water surging upward: splashy rising whoosh. (0.6s) | `Water_WaterVeil_Release.wav` | Cast_Self 0.63s |
| barrierFx +0s | A waterfall curtain: continuous rushing falling water, four seconds, fading out. (4s, 4s 유지) | `Water_WaterVeil_Falls.wav` | barrier lifetime 4s |

### Water_Wave

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A forceful water throw: splash whoosh. (0.5s) | `Water_Wave_Release.wav` | Cast_Forward 0.80s |
| 시전 +0.05s | A rolling ocean wave rushing forward and crashing surf, about one second. (1s, 0.9s 유지) | `Water_Wave_Surge.wav` | ⚠ travelFx 는 칸 이벤트가 없어 시전 기준. 비행 = 9/10 = 0.9s |

### Water_Whirlpool

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A water spell cast: swirling splash. (0.5s) | `Water_Whirlpool_Release.wav` | Cast_Ground 1.27s |
| zoneFx +0s | A swirling whirlpool vortex: churning water sucking inward and gurgling, four seconds, fading out. (4s, 4s 유지) | `Water_Whirlpool_Vortex.wav` | zoneTime 4s |

### Wind_Gust

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.567s | A powerful gust of wind sweeping forward: strong whoosh with rustling leaves. (0.8s) | `Wind_Gust_Release.wav` | ⚠ Cast_Sweep 는 휘두르기가 이어져서 급감 봉우리 없음 → release 0.57 |
| hitFx +0s | A wind blast hitting a body: punchy air thump. (0.5s) | `Wind_Gust_Hit.wav` | hitFx burst 0s |

### Wind_Headwind

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A wind spell gesture: sharp air burst. (0.5s) | `Wind_Headwind_Release.wav` | Cast_Ground 1.27s |
| zoneFx +0s | A howling strong wind zone: continuous gale with swirling gusts, four seconds, fading out. (4s, 4s 유지) | `Wind_Headwind_Gale.wav` | zoneTime 4s |

### Wind_Leap

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.083s | A wind-powered leap: fast rising air whoosh. (0.5s) | `Wind_Leap_Jump.wav` | ⚠ Dash 클립 손·발 속도 시작 0.08s, 봉우리가 약하고 여러 개라 애매 |
| impactFx +0s | Landing with a burst of wind: air shockwave thump and swirling dust. (0.8s) | `Wind_Leap_Land.wav` | 착지 impactFx burst 0s (dashTime 0.3) |

### Wind_Tailwind

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | Swirling wind wrapping around a body: speed boost whoosh rising and spinning. (1s) | `Wind_Tailwind_Release.wav` | Cast_Self 0.63s, castFx 폭발 3.5m 도 같이 |

### Wind_Tornado

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | A wind spell thrust: short air burst. (0.5s) | `Wind_Tornado_Release.wav` | Cast_Forward 0.80s |
| 시전 +0.05s | A small tornado roaring and spinning: swirling wind howl moving forward for two seconds. (2s, 2s 유지) | `Wind_Tornado_Twister.wav` | ⚠ 비행 = 12/6 = 2s (커서 거리와 무관, 관통) |

### Wind_WindCutter

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.8s | Several sharp wind blades slicing the air: rapid whistling swishes. (0.5s) | `Wind_WindCutter_Release.wav` | Cast_Forward 0.80s, 칼날 5개 |
| hitFx +0s | A wind blade slicing hit: sharp cutting swish. (0.5s) | `Wind_WindCutter_Hit.wav` | 적마다 hitFx |

### Wind_WindVeil

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | A wind spell gesture: quick swirling whoosh. (0.5s) | `Wind_WindVeil_Release.wav` | Cast_Self 0.63s |
| barrierFx +0s | A spinning wind barrier dome: steady whooshing air vortex, three seconds, fading out. (3s, 3s 유지) | `Wind_WindVeil_Dome.wav` | lifetime 3s |
| blockFx +0s | A projectile deflected by wind: sharp air whoosh with a light ping. (0.5s) | `Wind_WindVeil_Block.wav` | 막을 때 blockFx |

### Finisher_Blessing

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.633s | A divine water blessing: shimmering sparkling chime swell with a gentle splash. (1.5s) | `Finisher_Blessing_Release.wav` | Cast_Self 0.63s |

### Finisher_MeteorFall

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.6s | A fire spell summoning upward: rising flame whoosh. (0.5s) | `Finisher_MeteorFall_Release.wav` | ⚠ Cast_Sky 봉우리 없음 → release 0.60 |
| windupFx +0s | A meteor whistling down from the sky: burning roar growing louder. (0.7s) | `Finisher_MeteorFall_Fall.wav` | windupFx 0s, delay 0.6s 뒤 충돌 |
| impactFx +0s | A massive meteor impact explosion: deep boom with a fiery blast and rock debris. (1.5s) | `Finisher_MeteorFall_Impact.wav` | impactFx burst 0s |

### Finisher_PlasmaBurst

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A huge plasma explosion: electric fire blast with a crackling energy boom. (1.5s) | `Finisher_PlasmaBurst_Burst.wav` | ⚠ 손이 땅 치는 1.27s(+0.15s) vs impactFx 0s — 손 동작 쪽으로 |

### Finisher_Storm

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A storm being summoned: thunder rumble swell with wind. (0.8s) | `Finisher_Storm_Release.wav` | Cast_Ground 1.27s |
| impactFx +0s | A lightning bolt strike: short thunder snap crack. (0.6s) | `Finisher_Storm_Strike.wav` | 0.45s 마다 번개 1개 impactFx |
| 시전 +0.15s | Thunderstorm ambience: rain and distant thunder rumbles, four seconds, fading out. (4s, 4s 유지) | `Finisher_Storm_Ambience.wav` | zoneTime 4s |

### Finisher_Tsunami

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 1.267s | A giant tsunami wave crashing outward: massive water roar and surf explosion. (2s) | `Finisher_Tsunami_Wave.wav` | ⚠ 손이 땅 치는 1.27s(+0.15s) vs impactFx 0s — 손 동작 쪽으로 |

### Gunner_Dash

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.083s | A quick tactical dash: short air whoosh with light gear rattle. (0.5s) | `Gunner_Dash_DashStart.wav` | ⚠ Dash 클립 시작 0.08s, 봉우리 약함 |
| 시전 +0.2s | A fast gun magazine reload: crisp click and clack. (0.6s) | `Gunner_Dash_Reload.wav` | reloadOnDash, duration 0.2s 끝 |

### Gunner_DashBlast

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| Anim 0.083s | A quick forward dash: short punchy whoosh. (0.5s) | `Gunner_DashBlast_Dash.wav` | WeaponSkill 클립 손 속도 급감 0.08s |
| Anim 0.208s | A shotgun-like electric shock blast: powerful bang with crackling electricity. (1s) | `Gunner_DashBlast_Blast.wav` | 엉덩이 이동 멈춤 0.21s = 코드 dashTime 0.18s 와 거의 같음 |

### Gunner_ShockCannon

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | An energy cannon charging: rising electric whine. (0.5s) | `Gunner_ShockCannon_Charge.wav` | ⚠ 코드 chargeTime 0.3s. Ultimate 클립 급감(0.08/0.21s)과 안 맞아 코드 값 사용 |
| 시전 +0.3s | A massive energy cannon shot: heavy boom with recoil and a long echo. (1.5s) | `Gunner_ShockCannon_Fire.wav` | ⚠ chargeTime 0.3s 뒤 발사 |

### RuneMage_Blink

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | An arcane blink teleport: magical whoosh with a shimmering pop. (0.6s) | `RuneMage_Blink_Blink.wav` | 모델 없음 → 코드 값 (즉시) |
| 시전 +1s | An arcane rune trap detonating: magical burst with sparkles. (0.8s) | `RuneMage_Blink_Trap.wav` | trapDelay 1s |

### RuneMage_RuneBurst

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | Rapid arcane runes launched in quick succession: magical zips. (0.7s) | `RuneMage_RuneBurst_Launch.wav` | launchInterval 0.05s |
| 충돌 +0s | An arcane rune explosion: magical pop burst with sparkle. (0.7s) | `RuneMage_RuneBurst_Blast.wav` | 룬 폭발 반경 2m 마다 SkillImpacts |

### RuneMage_ArcaneCollapse

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | An arcane teleport: deep magical whoosh. (0.5s) | `RuneMage_ArcaneCollapse_Blink.wav` | blinkDelay 0.25s 전 |
| 시전 +0.45s | An arcane implosion blast: deep magical boom pulling inward. (0.8s) | `RuneMage_ArcaneCollapse_Blast.wav` | firstBlastTime 0.45s |
| 시전 +0.95s | An arcane implosion blast: deep magical boom pulling inward. (0.8s) | `RuneMage_ArcaneCollapse_Blast.wav` | + blastInterval 0.5s |
| 시전 +1.45s | A massive final arcane collapse: huge magical explosion with a deep booming tail. (2s) | `RuneMage_ArcaneCollapse_Collapse.wav` | 3번째 = 마지막 폭발 x1.5 |

### ChainDash

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | A quick chain dash: whoosh with metal chain rattle. (0.5s) | `ChainDash_Dash.wav` | 모델 없음 → 코드 값 |

### ChainDashStrike

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | A chain blade slash: metallic whoosh with a fiery edge. (0.5s) | `ChainDashStrike_Slash.wav` | tick 0s |
| 시전 +0.3s | A chain blade slash: metallic whoosh with a fiery edge. (0.5s) | `ChainDashStrike_Slash.wav` | tick 0.3s |
| 시전 +0.6s | A chain strike landing: heavy fiery slam. (0.9s) | `ChainDashStrike_Land.wav` | dashDuration 0.6s 끝 착지 tick |

### ChainPullSpin

| 시점 | 사운드 설명 | 파일명 | 비고 |
|---|---|---|---|
| 시전 +0s | A chain yank pulling upward: rising whoosh. (0.5s) | `ChainPullSpin_Rise.wav` | riseDuration 0.35s |
| 시전 +0.65s | A diving slam: heavy ground impact with electric crackle. (1s) | `ChainPullSpin_Slam.wav` | rise 0.35 + dive 0.3 |
| 시전 +0.65s | A spinning chain whirlwind: continuous metallic whooshing with an electric buzz for two and a half seconds. (2.5s, 2.5s 유지) | `ChainPullSpin_Spin.wav` | spinDuration 2.5s |

## 이미 소리가 있던 스킬 — 비교 후 건너뜀

불 스킬 7개는 Mixkit 녹음 소리(FxAutoSound)와 타임라인 소리가 이미 이펙트 프리팹에 붙어 있음. 같은 기준(파티클 burst 시각과 맞는지 / 빠진 시점이 있는지 / 길이가 이펙트에 맞는지)으로 비교:

| 스킬 | 기존 소리 (시점) | 비교 | 결정 |
|---|---|---|---|
| Fire_Fireball | 시전 Fire_woosh(castFx 0s), 비행 반복 Fire_swoosh_burning, 폭발 Fire_explosion(impactFx 0s), 맞음 Short_fire_whoosh | 시전·비행·폭발·맞음 다 있고 burst 0s 와 맞음 | 건너뜀 |
| Fire_Inferno | 시전 woosh, 폭발 Fire_spell_with_explosion(0s) + Big_fire_burning(0.2s) | BurningAsh burst 0/0.25/0.5s 와 맞음 | 건너뜀 |
| Fire_FlameThrower | 시전 woosh, 방사 반복 Intense_long_fire_beam | 반복이 이펙트와 같이 꺼짐 | 건너뜀 |
| Fire_FlameDash | 시전 woosh, 돌진 Catapult_fire_whoosh(dashFx 0s) | 돌진 0.2s 시작과 맞음 | 건너뜀 |
| Fire_FireVeil | 시전 woosh, 장막 Wizard_fire_woosh(0s) + 반복 불길(0.1s) | 장막 5s 동안 반복 | 건너뜀 |
| Fire_Frenzy | 타임라인 소리 3개(0/0.02/0.05s) + 오라 swoosh·반복 불길 | 손으로 맞춘 타임라인 | 건너뜀 |
| Fire_Meteor | 타임라인 시전·낙하·충돌 소리 + Mixkit 운석 낙하/폭발 | 손으로 맞춘 타임라인 | 건너뜀 |

새로 만든 소리는 녹음 소리보다 낫다고 확인할 근거가 없어서(직접 들어 보지 못함) 기존 것을 유지. 물·전기 스킬이 쓰던 공통 원소 소리(SfxBank, 원소마다 1개)는 스킬 전용 소리가 생기면 그 스킬에서만 꺼짐 — SkillSoundData 의 `replacesGenericCast / replacesGenericImpact` 를 끄면 공통 소리로 돌아감.

Swap_* (예전 원소 교체 스킬, JJH SkillSwapTest 씬에서만 씀)와 패시브·빈 칸 스킬은 대상에서 뺌.
