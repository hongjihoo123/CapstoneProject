# 스킬 효과음 배치 결과 (2026-11-04)

소리는 ElevenLabs 생성 대신 **받아 둔 무료 효과음을 잘라서** 넣음 (ElevenLabs 무료 사용이 계정에서 막힘). 원본·자른 구간: `SkillSoundSources.tsv`

시점: **Anim** = 몸 동작 클립 이벤트 `PlaySkillSound(이벤트)` (클립 안 시각. Cast_* 클립은 손 뻗기 0.15s 전부터 틀어서 실제로는 시전 0.15s 뒤) · **칸 +s** = 그 이펙트가 터진 뒤 · **시전 +s** = 스킬 쓴 뒤 · **충돌 +s** = 범위 공격이 닿은 뒤

파일: `Assets/Audio/SFX/Skills/` · ⚠ = 타이밍 추정이 애매했던 소리 · 출처 MS = Magic Spell SFX(CC BY 4.0), Hove = Hove Audio, Kenney = CC0, SciFiUI = Hove Sci-Fi UI


### Earth_Boulder

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 흙 마법 휙 | `Earth_Boulder_Release.wav` | MS earth_spell_cast_magic_whoosh_01.ogg | Cast_Forward 손 끝 속도 급감 0.80s (CastMotions release 0.80과 일치) |
| 시전 +0.05s | 돌 굴러가는 소리 (비행 0.9s) | `Earth_Boulder_Roll.wav` | MS earth_spell_cast_falling_rocks_02.ogg | ⚠ 비행시간 = 거리/11 (최대 0.9s) — 커서 거리마다 달라서 길이 고정이 애매 |
| hitFx +0s | 돌 둔탁한 충돌 | `Earth_Boulder_Hit.wav` | MS earth_spell_cast_stone_dry_impact_01.ogg | FX_Earth_PbrHit burst 0s |

### Earth_EarthSplitter

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 흙 마법 휙 | `Earth_EarthSplitter_Release.wav` | MS earth_spell_cast_magic_whoosh_02.ogg | Cast_Forward 0.80s |
| impactFx +0s | 바위 솟구침 | `Earth_EarthSplitter_Eruption.wav` | MS earth_spell_cast_rise_rocks_03.ogg | 첫 impactFx(burst 0s)에서 한 번만 (minInt 0.5) |

### Earth_Earthquake

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.367s | 땅 내려찍기 + 돌 부스러기 | `Earth_Earthquake_Stomp.wav` | MS earth_spell_cast_impact_ground_rock_debris_02.ogg | Cast_Ground 엉덩이 하강 멈춤(착지) 1.37s |
| zoneFx +0s | 지진 울림 (장판 3s) | `Earth_Earthquake_Quake.wav` | MS earth_spell_cast_earthquake_power_03.ogg | zoneFx 반복 파티클 zoneTime 3s |

### Earth_EarthSmash

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 큰 땅 폭발 | `Earth_EarthSmash_Slam.wav` | MS earth_spell_cast_meteor_explosion_02.ogg | ⚠ 손이 땅 치는 1.27s(게임 +0.15s) vs impactFx burst 0s — 0.15s 어긋나서 손 동작 쪽으로 맞춤 |

### Earth_GraspOfEarth

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.6s | 흙·잎 휙 (소환 동작) | `Earth_GraspOfEarth_Release.wav` | MS earth_spell_cast_leaves_whoosh_02.ogg | ⚠ Cast_Sky 는 손 속도 급감이 없음(봉우리 없이 서서히) → CastMotions release 0.60 사용 |
| windupFx +0s | 땅 갈라지며 올라오는 소리 앞부분 | `Earth_GraspOfEarth_Rumble.wav` | MS earth_spell_cast_rise_ground_02.ogg | windupFx burst 0s, 0.4s 뒤 손이 솟음 |
| impactFx +0s | 바위가 모양 바꾸며 조임 | `Earth_GraspOfEarth_Grab.wav` | MS earth_spell_cast_morph_rocks_01.ogg | impactFx burst 0s (delay 0.4) |

### Earth_RockWall

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 짧은 흙 마법 휙 | `Earth_RockWall_Release.wav` | MS earth_spell_cast_magic_whoosh_01.ogg | Cast_Self 0.63s |
| barrierFx +0s | 땅이 솟는 벽 | `Earth_RockWall_Rise.wav` | MS earth_spell_cast_rise_ground_04.ogg | barrierFx spikes burst 0s |

### Earth_StoneShield

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 돌이 몸에 붙는 소리 | `Earth_StoneShield_Release.wav` | MS earth_spell_cast_morph_rocks_04.ogg | Cast_Self 0.63s |
| impactFx +0s | 돌 갑옷 터짐 | `Earth_StoneShield_Burst.wav` | MS earth_spell_cast_impact_ground_rock_debris_01.ogg | explodeDelay 3s 뒤 impactFx burst 0s |

### Electric_ChainLightning

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 전기 시전 | `Electric_ChainLightning_Release.wav` | Hove MAGSpel_SpellCast_HoveAud_LightningMagic_01.wav | Cast_Forward 0.80s |
| hitFx +0s | 짧은 번개 튐 | `Electric_ChainLightning_Hit.wav` | MS lighting_spell_electric_zap_blast_sparkle_bright_01.ogg | 적마다 hitFx (jumpDelay 0.08s 간격) — minInt 0.05 라 튈 때마다 울림 |

### Electric_ElectricNet

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 짧은 지직 던짐 | `Electric_ElectricNet_Release.wav` | MS lighting_spell_electric_zap_blast_sparkle_dark_03.ogg | Cast_Forward 0.80s |
| impactFx +0s | 전기 터지며 지직거림 | `Electric_ElectricNet_Snare.wav` | MS lighting_spell_electric_impact_static_light_explode_01.ogg | impactFx strikes 0/0.07/0.14s + current 1.2s |

### Electric_Flash

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| castFx +0s | 순간 번개 출발 | `Electric_Flash_Depart.wav` | MS lighting_spell_electric_zap_blast_sparkle_bright_02.ogg | 순간이동이라 몸 동작 대신 castFx 0s |
| impactFx +0s | 도착 전기 폭발 | `Electric_Flash_Arrive.wav` | MS lighting_spell_electric_impact_static_light_explode_short_01.ogg | 도착 impactFx burst 0s |

### Electric_LightningSpear

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 전기 창 시전 | `Electric_LightningSpear_Release.wav` | Hove MAGSpel_SpellCast_HoveAud_LightningMagic_05.wav | Cast_Forward 0.80s |
| hitFx +0s | 관통 지직 | `Electric_LightningSpear_Hit.wav` | MS lighting_spell_electric_zap_blast_sparkle_dark_01.ogg | 관통이라 폭발 대신 맞을 때마다 |

### Electric_Overcharge

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 전기 충전 상승음 | `Electric_Overcharge_Release.wav` | Hove MAGElem_ElectricCharge_HoveAud_LightningMagic.wav | Cast_Self 0.63s |

### Electric_StaticShell

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 정전기 막 앞부분 | `Electric_StaticShell_Release.wav` | MS lighting_spell_electric_loop_static_sparkle_bright_02.ogg | Cast_Self 0.63s |
| hitFx +0s | 반격 지직 | `Electric_StaticShell_Shock.wav` | MS lighting_spell_electric_zap_blast_sparkle_bright_03.ogg | retaliate 5s 동안 때린 적에게 hitFx |

### Electric_Thunderbolt

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.6s | 전기 모이는 지직 | `Electric_Thunderbolt_Release.wav` | MS lighting_spell_electric_zap_blast_sparkle_dark_04.ogg | ⚠ Cast_Sky 봉우리 없음 → release 0.60 |
| impactFx +0s | 큰 천둥 낙뢰 | `Electric_Thunderbolt_Strike.wav` | MS lighting_spell_electric_impact_thunder_loud_dark_01.ogg | delay 0.25s 뒤 impactFx burst 0s |

### Water_BubblePrison

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 물 공 발사 | `Water_BubblePrison_Release.wav` | MS water_spell_cast_fast_ball_power_01.ogg | Cast_Forward 0.80s |
| impactFx +0s | 거품에 갇힘 | `Water_BubblePrison_Trap.wav` | MS water_spell_cast_drown_bubble_01.ogg | impactFx burst 0s |

### Water_HealingRain

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 부드러운 물방울 | `Water_HealingRain_Release.wav` | MS water_spell_cast_soft_drop_02.ogg | Cast_Ground 1.27s |
| zoneFx +0s | 치유 반짝임 (빗소리 전용 파일은 없음) | `Water_HealingRain_Rain.wav` | MS Holy_spell_heal_cast_long_01.ogg | zoneTime 4s |

### Water_WaterCannon

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 물줄기 휙 | `Water_WaterCannon_Release.wav` | MS water_spell_cast_splash_whoosh_01.ogg | Cast_Forward 0.80s |
| impactFx +0s | 물 충돌 | `Water_WaterCannon_Splash.wav` | MS water_spell_cast_impact_ball_01.ogg | 끝점 impactFx burst 0s |

### Water_WaterVeil

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 물이 솟는 소리 | `Water_WaterVeil_Release.wav` | MS water_spell_cast_rise_smooth_impact_01.ogg | Cast_Self 0.63s |
| barrierFx +0s | 흐르는 물 (벽 4s) | `Water_WaterVeil_Falls.wav` | MS water_spell_cast_flowing_loop_02.ogg | barrier lifetime 4s |

### Water_Wave

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 물 던지는 휙 | `Water_Wave_Release.wav` | MS water_spell_cast_double_splash_whoosh_02.ogg | Cast_Forward 0.80s |
| 시전 +0.05s | 파도 밀려옴 | `Water_Wave_Surge.wav` | MS water_spell_cast_rise_impact_wave_03.ogg | ⚠ travelFx 는 칸 이벤트가 없어 시전 기준. 비행 = 9/10 = 0.9s |

### Water_Whirlpool

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 물 휙 | `Water_Whirlpool_Release.wav` | MS water_spell_cast_splash_whoosh_02.ogg | Cast_Ground 1.27s |
| zoneFx +0s | 부글부글 소용돌이 (장판 4s) | `Water_Whirlpool_Vortex.wav` | MS water_spell_cast_boil_loop_01.ogg | zoneTime 4s |

### Wind_Gust

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.567s | 강한 바람 휙 | `Wind_Gust_Release.wav` | MS wind_spell_cast_air_transient_whoosh_01.ogg | ⚠ Cast_Sweep 는 휘두르기가 이어져서 급감 봉우리 없음 → release 0.57 |
| hitFx +0s | 바람 충격 | `Wind_Gust_Hit.wav` | MS wind_spell_cast_air_impact_splash_01.ogg | hitFx burst 0s |

### Wind_Headwind

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 짧은 바람 | `Wind_Headwind_Release.wav` | MS wind_spell_cast_air_short_whoosh_03.ogg | Cast_Ground 1.27s |
| zoneFx +0s | 계속 부는 바람 (장판 4s) | `Wind_Headwind_Gale.wav` | MS wind_spell_cast_air_long_loop_01.ogg | zoneTime 4s |

### Wind_Leap

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.083s | 올라가는 휙 | `Wind_Leap_Jump.wav` | MS wind_spell_cast_air_short_rise_whoosh_01.ogg | ⚠ Dash 클립 손·발 속도 시작 0.08s, 봉우리가 약하고 여러 개라 애매 |
| impactFx +0s | 바람 착지 충격 | `Wind_Leap_Land.wav` | MS wind_spell_cast_air_impact_splash_02.ogg | 착지 impactFx burst 0s (dashTime 0.3) |

### Wind_Tailwind

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 바람이 감아 올라감 | `Wind_Tailwind_Release.wav` | MS wind_spell_cast_air_rise_cut_01.ogg | Cast_Self 0.63s, castFx 폭발 3.5m 도 같이 |

### Wind_Tornado

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 짧은 바람 터짐 | `Wind_Tornado_Release.wav` | MS wind_spell_cast_air_short_noisy_whoosh_02.ogg | Cast_Forward 0.80s |
| 시전 +0.05s | 길게 휘도는 바람 (비행 2s) | `Wind_Tornado_Twister.wav` | MS wind_spell_cast_frost_air_long_02.ogg | ⚠ 비행 = 12/6 = 2s (커서 거리와 무관, 관통) |

### Wind_WindCutter

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.8s | 날카로운 바람 발사 | `Wind_WindCutter_Release.wav` | MS wind_spell_cast_air_sharp_shoot_01.ogg | Cast_Forward 0.80s, 칼날 5개 |
| hitFx +0s | 베는 바람 | `Wind_WindCutter_Hit.wav` | MS wind_spell_cast_air_sharp_shoot_02.ogg | 적마다 hitFx |

### Wind_WindVeil

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 바람 휙 | `Wind_WindVeil_Release.wav` | MS wind_spell_cast_air_short_spacious_whoosh_03.ogg | Cast_Self 0.63s |
| barrierFx +0s | 계속 부는 바람 (장막 3s, Headwind 와 다른 구간) | `Wind_WindVeil_Dome.wav` | MS wind_spell_cast_air_long_loop_01.ogg | lifetime 3s |
| blockFx +0s | 튕겨내는 휙 | `Wind_WindVeil_Block.wav` | MS wind_spell_cast_air_short_whoosh_02.ogg | 막을 때 blockFx |

### Finisher_Blessing

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.633s | 밝은 축복 | `Finisher_Blessing_Release.wav` | MS Holy_spell_heal_cast_bright_02.ogg | Cast_Self 0.63s |

### Finisher_MeteorFall

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.6s | 불 소환 앞부분 | `Finisher_MeteorFall_Release.wav` | MS fire_spell_long_soft_01.ogg | ⚠ Cast_Sky 봉우리 없음 → release 0.60 |
| windupFx +0s | 불덩이 떨어지는 소리 대신 (휘파람 낙하음 없음) | `Finisher_MeteorFall_Fall.wav` | MS fire_spell_long_warm_01.ogg | windupFx 0s, delay 0.6s 뒤 충돌 |
| impactFx +0s | 운석 폭발 | `Finisher_MeteorFall_Impact.wav` | MS earth_spell_cast_meteor_explosion_01.ogg | impactFx burst 0s |

### Finisher_PlasmaBurst

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 큰 불 폭발 | `Finisher_PlasmaBurst_Burst.wav` | MS fire_spell_charge_explosion_dark_01.ogg | ⚠ 손이 땅 치는 1.27s(+0.15s) vs impactFx 0s — 손 동작 쪽으로 |

### Finisher_Storm

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 천둥 시작 | `Finisher_Storm_Release.wav` | MS lighting_spell_electric_impact_thunder_static_warm_01.ogg | Cast_Ground 1.27s |
| impactFx +0s | 짧은 낙뢰 | `Finisher_Storm_Strike.wav` | MS lighting_spell_electric_impact_thunder_dark_dry_01.ogg | 0.45s 마다 번개 1개 impactFx |
| 시전 +0.15s | 계속 지직거림 (비 소리는 없음) | `Finisher_Storm_Ambience.wav` | MS lighting_spell_electric_loop_static_warm_02.ogg | zoneTime 4s |

### Finisher_Tsunami

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 1.267s | 큰 파도 | `Finisher_Tsunami_Wave.wav` | MS water_spell_cast_rise_impact_wave_02.ogg | ⚠ 손이 땅 치는 1.27s(+0.15s) vs impactFx 0s — 손 동작 쪽으로 |

### Gunner_Dash

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.083s | 짧은 휙 | `Gunner_Dash_DashStart.wav` | MS wind_spell_cast_air_short_whoosh_01.ogg | ⚠ Dash 클립 시작 0.08s, 봉우리 약함 |
| 시전 +0.2s | 딸깍 (탄창 소리는 없음) | `Gunner_Dash_Reload.wav` | SciFiUI Click_Combo.wav | reloadOnDash, duration 0.2s 끝 |

### Gunner_DashBlast

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| Anim 0.083s | 짧은 휙 | `Gunner_DashBlast_Dash.wav` | MS wind_spell_cast_air_short_noisy_whoosh_01.ogg | WeaponSkill 클립 손 속도 급감 0.08s |
| Anim 0.208s | 전기 충격 폭발 | `Gunner_DashBlast_Blast.wav` | Hove MAGElem_LightningStrikeSolo_HoveAud_LightningMagic_08.wav | 엉덩이 이동 멈춤 0.21s = 코드 dashTime 0.18s 와 거의 같음 |

### Gunner_ShockCannon

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 에너지 충전 | `Gunner_ShockCannon_Charge.wav` | Kenney forceField_000.ogg | ⚠ 코드 chargeTime 0.3s. Ultimate 클립 급감(0.08/0.21s)과 안 맞아 코드 값 사용 |
| 시전 +0.3s | 낮은 큰 폭발 | `Gunner_ShockCannon_Fire.wav` | Kenney lowFrequency_explosion_000.ogg | ⚠ chargeTime 0.3s 뒤 발사 |

### RuneMage_Blink

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 순간이동 | `RuneMage_Blink_Blink.wav` | MS dark_spell_cast_warp_0-001.ogg | 모델 없음 → 코드 값 (즉시) |
| 시전 +1s | 룬 함정 터짐 | `RuneMage_Blink_Trap.wav` | MS Holy_spell_blast_cast_01.ogg | trapDelay 1s |

### RuneMage_RuneBurst

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 마법 구슬 발사 | `RuneMage_RuneBurst_Launch.wav` | MS dark_spell_cast_energy_ball_01.ogg | launchInterval 0.05s |
| 충돌 +0s | 룬 폭발 | `RuneMage_RuneBurst_Blast.wav` | MS Holy_spell_blast_cast_02.ogg | 룬 폭발 반경 2m 마다 SkillImpacts |

### RuneMage_ArcaneCollapse

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 순간이동 | `RuneMage_ArcaneCollapse_Blink.wav` | MS dark_spell_cast_warp_0-002.ogg | blinkDelay 0.25s 전 |
| 시전 +0.45s | 마법 폭발 | `RuneMage_ArcaneCollapse_Blast.wav` | MS dark_spell_cast_charge_impact_01.ogg | firstBlastTime 0.45s |
| 시전 +0.95s | 마법 폭발 | `RuneMage_ArcaneCollapse_Blast.wav` | MS dark_spell_cast_charge_impact_01.ogg | + blastInterval 0.5s |
| 시전 +1.45s | 큰 마법 폭발 | `RuneMage_ArcaneCollapse_Collapse.wav` | MS dark_spell_cast_energy_wave_01.ogg | 3번째 = 마지막 폭발 x1.5 |

### ChainDash

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 짧은 휙 (사슬 소리는 없음) | `ChainDash_Dash.wav` | MS wind_spell_cast_air_short_whoosh_02.ogg | 모델 없음 → 코드 값 |

### ChainDashStrike

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 베는 휙 | `ChainDashStrike_Slash.wav` | MS wind_spell_cast_air_sharp_shoot_03.ogg | tick 0s |
| 시전 +0.3s | 베는 휙 | `ChainDashStrike_Slash.wav` | MS wind_spell_cast_air_sharp_shoot_03.ogg | tick 0.3s |
| 시전 +0.6s | 불 착지 충격 | `ChainDashStrike_Land.wav` | MS fire_spell_burst_impact_short_01.ogg | dashDuration 0.6s 끝 착지 tick |

### ChainPullSpin

| 시점 | 사운드 설명 | 파일명 | 원본 | 비고 |
|---|---|---|---|---|
| 시전 +0s | 올라가는 휙 | `ChainPullSpin_Rise.wav` | MS wind_spell_cast_air_rise_sharp_shoot_03.ogg | riseDuration 0.35s |
| 시전 +0.65s | 전기 내려찍기 | `ChainPullSpin_Slam.wav` | Hove MAGElem_LightningStrikeWoutRum_HoveAud_LightningMagic_13.wav | rise 0.35 + dive 0.3 |
| 시전 +0.65s | 계속 지직거리는 회전 (2.5s) | `ChainPullSpin_Spin.wav` | Hove MAGElem_LightningShockHold_HoveAud_LightningMagic.wav | spinDuration 2.5s |

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
