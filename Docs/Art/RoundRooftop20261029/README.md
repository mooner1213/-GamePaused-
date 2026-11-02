# 뒤로 누운 원형 옥상 시안

최종 요청은 **좌우를 비스듬히 돌리지 않고, 원을 뒤로 더 눕혀 보이는 낮은 시점의 납작한 타원**이다.

선택한 원화는 `Assets/Art/RoundRooftop20261029/Source-LowAngle.png`다. 기본 제공 `image_gen` 도구로 제작했다. 첫 시안과 잘못 해석한 좌우 사선 시안도 각각 `Source.png`, `Source-Oblique.png`에 별도로 보존했다. 최종 씬은 이 두 시안을 참조하지 않는다.

`Assets/Art/RoundRooftop20261029/<테마>/` 안에 뒤쪽 반원 성벽, 타원 바닥, 앞쪽 반원 난간이 각각 PNG와 Sprite 자산으로 분리되어 있다. PNG는 832×384, PPU 32, Point 필터, 불투명 도트/투명 배경으로 정리했다. 세 레이어는 동일한 발 기준 피벗을 공유하며 씬 배율은 모두 1이다. 그림의 세 연결 영역을 분리하고 앞뒤 벽 밑선을 바닥 테두리에 맞추는 작업은 Unity Editor의 자산 처리로 수행했다. 바닥 내부 투명 픽셀은 0개다.

15개 테마와 MainScene에 적용했으며 테마별 색을 사용한다. Tile이나 팔레트 항목은 추가하지 않았다. 기존 발판·충돌·Tilemap 내용·색·변환은 적용 전후 비교에서 동일했다.

## Unity에서 보기

맵 루트의 `09 뒤로 누운 원형 옥상 · 시안`을 펼치면 세 SpriteRenderer를 각각 선택할 수 있다. 뒤벽 정렬 순서는 8, 바닥 7, 앞 난간 30이며 사람은 20이다. 앞 난간은 캐릭터와 겹치는 부분을 가린다.

이전 그림은 `08 테마 옥상 · 원화 1·3·5줄`에 비활성 상태로 보존했다. 이전 그림과 비교하려면 새 `09` 오브젝트를 끄고 이전 `08` 오브젝트를 켠다. 타일이나 발판 데이터를 재생성할 필요가 없다.

## 최종 생성 프롬프트

> Edit this exact 3-layer pixel-art rooftop sheet, correcting ONLY the vertical viewing angle. The user wants the circular roof to lie MORE FLAT and RECLINE BACK AWAY from the viewer, as if the camera is LOWER, closer to side-on eye level. This means a MUCH FLATTER, SHALLOWER HORIZONTAL ELLIPSE. Keep both left and right ends at the SAME height: ZERO diagonal tilt, ZERO clockwise rotation, ZERO three-quarter side angle. Preserve symmetrical front-centered camera. Reduce the floor ellipse's visible vertical depth to about 55% of its current depth while preserving its full width (very wide ellipse about 9:1 width to depth), still enough paving surface to read as a circle lying horizontal. Make the back and front wall half-ellipse curves correspondingly shallower too. Keep the low parapet blocks only about 60% of their current vertical height, so the front parapet will not hide a small player. Preserve dark charcoal keycap stone, cyan and magenta insets, coarse crisp square pixel clusters, limited palette, uninterrupted solid paved floor, same three isolated horizontal bands (back half wall TOP, complete oval floor MIDDLE, front half low wall BOTTOM). All 3 layers must share exactly the SAME horizontal center, endpoints, width and ellipse perspective, ready to composite. No changes in design theme. No characters, no text, no extra props. Genuine transparent background with HARD opaque edges: NO atmospheric haze, NO glow/shadows outside the sprites, NO grey gradient background, NO checkerboard. Generous transparent gutters separate the three layers. The key change is a lower camera and flattened ellipse, NOT side-to-side slant.

이는 생성 지시문이며 출력 비율의 수치 보증이 아니다. 실제 선택된 도트와 조립 결과는 자산 및 `Docs/Validation/RoundRooftop20261029`의 이미지에서 확인한다.
