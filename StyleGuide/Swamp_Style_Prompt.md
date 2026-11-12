# 게임 풍(스타일) 프롬프트 — "밤의 발광 늪지 / 핸드페인트 다크 판타지"

맵(`_HJH_Map`, `Map_Bridge_Normal`)의 현재 룩을 글로 정리한 것. 적·소품·새 에셋을 Tripo / GPT 이미지 / Midjourney 로 만들 때 그대로 붙여 쓰면 맵과 같은 풍으로 나옴.
플레이어는 셰이더(`HJH/Character/Toon` 의 Swamp Paint 옵션)로 맞췄고, 적은 아래 프롬프트로 모델을 만든 뒤 같은 셰이더 값을 쓰면 됨.

---

## 1. 풍 한 줄 요약

**어두운 밤의 맹그로브 늪지, 푸른 발광 식물이 포인트인 핸드페인트 스타일라이즈드 다크 판타지.**
Hades / Shape of Dreams 계열의 탑다운 액션 로그라이크. 밝고 귀여운 카툰이 아니라 "진지하지만 그림 같은" 톤.

## 2. 핵심 규칙 (이걸 어기면 맵과 따로 논다)

| 항목 | 규칙 |
|---|---|
| 팔레트 | 배경 색은 짙은 남색·청록·보라 회색. **채도 낮은 어둠 위에 발광색만 포인트.** 형광 초록·살구색 살·새빨강 같은 순색은 금지 |
| 포인트 색 | 청록 시안(`#4DFFF2` 근처), 이끼 초록(`#42703D`), 가끔 호박색 노랑. 눈·균열·장식 라인·약점에만 |
| 형태 | 덩어리를 쌓은 각진 면(facet), 바위처럼 묵직한 실루엣. 둥글고 말랑한 만화 형태 금지. 큰 형태 → 중간 → 작은 디테일 3단 |
| 재질 | 손으로 칠한 듯한 거친 붓 터치, 스크래치/균열 질감. PBR 사실 질감·금속 반사·플라스틱 광택 금지 |
| 명암 | 2단 툰 명암(밝은 면/그늘 면이 또렷), 그늘은 회색이 아니라 **푸른 보라색 그늘** |
| 테두리 | 얇은 진한 남색 외곽선(`#1A213D`), 검정 아님. 가장자리에 청록빛 림라이트 |
| 오염 | 위를 보는 면에 이끼, 뿌리·덩굴 감김, 젖은 흙. 늪에서 자란 것처럼 |
| 분위기 | 안개, 반딧불, 발광 포자. 신비롭고 약간 불길하지만 잔인하지 않음 |
| 탑다운 가독성 | 위에서 내려다봤을 때 실루엣이 읽혀야 함. 머리·어깨·무기가 크고, 포인트 색은 위쪽 면에 |

## 3. 프롬프트 (영문, 이미지/3D 생성용 — 복사해서 사용)

### 3-1. 공통 스타일 블록 (모든 프롬프트 끝에 붙임)

```
stylized hand-painted dark fantasy, bioluminescent mangrove swamp at night,
chunky faceted shapes, painterly brush-stroke texture with subtle scratches,
muted desaturated indigo and teal palette, cool violet-blue shadows,
two-tone cel shading with soft painted edges, thin deep-indigo outline,
small glowing cyan accents (eyes, cracks, runes, spores), moss and roots growing on the top surfaces,
soft cyan rim light, mysterious misty atmosphere,
readable silhouette from a top-down isometric camera,
game asset, full body, neutral T-pose or idle pose, plain background
```

### 3-2. 적 몸체 프롬프트 틀

```
[ENEMY DESCRIPTION: e.g. "a swamp-dwelling skeleton warrior with a cracked mossy shield"],
[BODY: heavy shoulders, oversized head, thick chunky limbs],
made of weathered stone, wet bark and old bone, overgrown with moss and vines,
glowing cyan eyes and glowing cyan cracks,
+ (3-1 공통 스타일 블록)
```

예시 3종 (현재 적 교체용):
- **해골 병사** → `a drowned skeleton soldier in rotten swamp armor, mossy round shield, rusty short sword, hollow glowing cyan eye sockets`
- **폭탄 거북** → `a stone-shelled bog turtle carrying a bundle of glowing cyan spore pods on its back, cracked shell with moss, small glowing cyan eyes`
- **외눈 구체** → `a floating bulb-like mangrove seed monster with one large amber glowing eye, root tendrils hanging below, cracked bark skin`

### 3-3. 네거티브 프롬프트

```
cute cartoon, chibi, glossy plastic, bright saturated colors, neon green, pink skin,
flat vector, realistic photo, metallic PBR sheen, thick black outline, anime moe, rainbow colors,
bright daylight, clean polished surface
```

### 3-4. 한국어 한 줄 버전 (기획 설명용)

> 어두운 밤 맹그로브 늪지에서 자란 듯한, 이끼 낀 돌·나무·뼈 재질의 각진 덩어리 형태. 채도 낮은 남색·청록 팔레트에 눈과 균열만 푸르게 빛나고, 붓 터치가 보이는 손그림 질감과 얇은 진남색 외곽선을 가진 다크 판타지 로그라이크 몬스터.

## 4. 만든 뒤 Unity 에서 맞추는 값 (적 머티리얼)

적 텍스처를 `HJH/Character/Toon` 머티리얼에 연결한 뒤 플레이어와 같은 값으로 시작 (`GunnerV2_Toon.mat` 복사해서 텍스처만 교체하면 가장 빠름):

| 속성 | 값 |
|---|---|
| Saturation / Brightness | 0.8 / 1.0 |
| Palette Grade (색 / 양) | (0.8, 0.9, 1.0) / 1 |
| Shadow Tint | (0.5, 0.56, 0.8) |
| Rim Color / Strength | (0.5, 0.92, 1.0) / 0.55 |
| Outline Color | (0.1, 0.13, 0.24) |
| Brush Strength / Scale | 0.7 / 36 |
| Simplify | 1.2 |
| Glow Amount / Color | 1.6 / (0.3, 1.0, 0.95) — 텍스처의 청록·노랑 부분이 자체 발광 |
| Ground Bounce | 0.5 |
| Moss Amount | 0.2~0.4 (적은 더 이끼 낀 쪽이 어울림) |

주의: 적은 `EnemyOutline.cs` 가 `_OutlineSolid`(빨강·흰 테두리)를 쓰므로, 적 식별용 테두리는 유지하고 나머지 값만 맞출 것.

## 5. 체크리스트 (생성물 검수)

- [ ] 어두운 맵 위에 올려놓았을 때 색이 튀지 않는가 (순색·살구색 없음)
- [ ] 위에서 봤을 때 실루엣이 읽히는가
- [ ] 포인트 발광이 눈·균열 등 2~3곳에만 있는가 (전신이 빛나면 실패)
- [ ] 이끼·뿌리 같은 늪 요소가 있는가
- [ ] 외곽선이 검정이 아니라 진남색인가
