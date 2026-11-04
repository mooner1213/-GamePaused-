# 일시정지! · GamePaused

**시간을 멈추고 점프를 설계하며 성벽을 오르는 2D 도트 등반 플랫포머입니다.**

[게임 기획서 PDF](Docs/Planning/GamePaused_게임기획서.pdf) · [기획 요약](Docs/Planning/README.md) · [플레이 씬](Assets/Scenes/MainScene.unity) · [개발 기록](Docs/DevelopmentNotes.md)

| 항목 | 내용 |
| --- | --- |
| 제작 | 문재혁 · 전주호 / 2인 팀 프로젝트 |
| 목표 플랫폼 | PC |
| 개발 환경 | Unity 6000.3.22f1 · C# |
| 현재 단계 | 점프 예약·벽 반사·예상 궤도를 포함한 프로토타입 개발 중 |

## 팀 구성

| 팀원 | 담당 |
| --- | --- |
| 문재혁 | 기획·스토리·플레이 흐름·UI 콘셉트 디자인 |
| 전주호 | Unity 개발·게임 기능 구현·애니메이션 |

기획 구체화와 디자인·콘텐츠 제작 및 개발 과정에 AI를 활용합니다.

## 핵심 플레이

1. 지형과 벽의 위치를 살펴보고 이동 경로를 생각합니다.
2. ESC로 시간을 멈춘 뒤 Space를 누르는 시간으로 점프 강도와 순서를 예약합니다.
3. 정지를 해제해 예약한 점프를 실행하고 착지·추락 결과를 확인합니다.
4. 벽 충돌에 따른 방향 전환을 활용해 다음 경로를 계획합니다.

방향키 없이 점프와 벽 반사로 이동합니다. 초반에는 예상 궤도로 조작을 익히고 이후에는 거리 감각과 경로 판단의 비중을 높이는 구성을 기획했습니다.

## 기획 방향

| 요소 | 설계 의도 |
| --- | --- |
| 시간 정지와 점프 예약 | 관찰 → 계획 → 실행 → 결과 확인의 반복 |
| 점프와 벽 반사 | 단순한 조작 안에서 발판과 반사 지점을 읽는 경로 선택 |
| 높이에 따른 궤도 가이드 축소 | 물리 규칙 학습에서 거리 판단과 숙련으로 연결 |
| ESC의 배신과 정지 기능 상실 | 스토리 사건을 전·후반의 플레이 규칙 변화로 연결 |
| 업적과 콘텐츠 해금 | 클리어 이후 추가 스테이지·코스튬과 기록 갱신 목표 제공 |

세부 규칙과 스토리·UI 방향은 [기획 요약](Docs/Planning/README.md)에서 확인할 수 있습니다.

## 진행 상태

| 구분 | 범위 |
| --- | --- |
| 프로토타입 코드 | 점프 입력 기록·이동 시뮬레이션·벽 반사·예상 궤도·발판과 기믹·카메라·HUD |
| 기획 | 전·후반 스토리·학습과 난이도·UI·업적과 해금·재플레이 기록 |
| 후속 설계·제작 | 연속 점프 세부 규칙·후반부 직접 점프·보스전·업적 수치·최종 UI |
| 검증 | 개별 기능의 기존 검사 기록 보관. 전체 스토리 구현과 완주 검증은 후속 작업 |

## 실행 방법

1. Unity Hub에서 프로젝트를 추가하고 **Unity 6000.3.22f1**로 엽니다.
2. [MainScene](Assets/Scenes/MainScene.unity)을 엽니다.
3. Play Mode로 실행합니다.

<details>
<summary>현재 프로토타입 조작</summary>

| 입력 | 동작 |
| --- | --- |
| Esc / 정지 버튼 | 시간 정지와 예약 재생 전환 |
| Space / 점프 버튼 | 누른 시간을 기록하고 놓을 때 점프 예약 |
| 마우스 휠 | 위아래 둘러보기 |
| 휠 클릭 / Esc | 자동 카메라 위치로 복귀 |

예약된 점프가 남아 있으면 추가 입력이 제한됩니다. 후반부 조작은 후속 설계 대상입니다.

</details>

## 개발 자료

- [기획서 PDF](Docs/Planning/GamePaused_게임기획서.pdf) · [기획 요약과 세부 규칙](Docs/Planning/README.md)
- [개발 상세 기록](Docs/DevelopmentNotes.md) · [플레이 피드백 기록](Docs/2026-10-20-플레이-피드백.md)
- [발판 길이와 엔딩 어퍼컷 수정](Docs/2026-10-26-발판길이와-엔딩어퍼컷.md)
- [아트 자료](Docs/Art) · [검증 기록](Docs/Validation) · [프로젝트 작업 규칙](AGENTS.md)

<details>
<summary>주요 코드와 검증 범위</summary>

| 항목 | 관련 코드 |
| --- | --- |
| 점프 명령 기록 | [JumpInputRecorder](Assets/Scripts/JumpInputRecorder.cs) · [JumpCommand](Assets/Scripts/JumpCommand.cs) |
| 이동과 충돌 계산 | [PauseSimulation](Assets/Scripts/PauseSimulation.cs) |
| 이동 계산을 공유하는 궤도 예측 | [PauseTrajectory](Assets/Scripts/PauseTrajectory.cs) |
| 발판과 기믹 | [PausePlatformObject](Assets/Scripts/PausePlatformObject.cs) · [PauseGimmickObject](Assets/Scripts/PauseGimmickObject.cs) |
| 캐릭터와 카메라 | [PauseCharacterView](Assets/Scripts/PauseCharacterView.cs) · [PauseBatchCamera](Assets/Scripts/PauseBatchCamera.cs) |

`-expansionQA` 검사는 시작점의 제한된 도약과 장치별 정지 표시를 확인하는 범위입니다. 전체 코스 완주 검증은 별도로 진행해야 합니다. 개발 도구에는 Windows·Android 빌드 경로가 있으며 기획상 목표 플랫폼은 PC입니다.

</details>

외부 폰트와 리소스는 각 제공자의 라이선스를 따릅니다.

