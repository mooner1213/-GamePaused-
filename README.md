# 일시정지! · GamePaused

**시간을 멈춘 상태에서 점프를 예약하고 재생하며 성벽을 오르는 2D 도트 플랫포머입니다.**

점프 버튼을 누른 시간을 기록하고 실행 시점에 재생합니다. 플레이어는 벽 반사와 발판·기믹을 이용해 다음 착지 위치를 판단합니다.

## 핵심 플레이

1. 시간을 정지합니다.
2. 점프 버튼을 누르는 시간으로 점프 세기를 기록합니다.
3. 정지를 해제해 예약한 점프를 실행합니다.
4. 착지 위치와 벽 반사를 확인하며 다음 도약을 준비합니다.

## 주요 구현

| 기능 | 구현 내용 | 코드 |
| --- | --- | --- |
| 입력 기록 | 정지 중 실제 입력 시간을 점프 명령으로 변환 | [JumpInputRecorder](Assets/Scripts/JumpInputRecorder.cs) · [JumpCommand](Assets/Scripts/JumpCommand.cs) |
| 이동 시뮬레이션 | 점프와 충돌·벽 반사 및 낙하·회복 계산 | [PauseSimulation](Assets/Scripts/PauseSimulation.cs) |
| 궤도 예측 | 실제 이동과 예측에 같은 시뮬레이션 계산 사용 | [PauseTrajectory](Assets/Scripts/PauseTrajectory.cs) |
| 발판과 기믹 | 발판 및 로프·사다리·트램펄린 등 처리 | [PauseGimmickObject](Assets/Scripts/PauseGimmickObject.cs) · [PausePlatformObject](Assets/Scripts/PausePlatformObject.cs) |
| 표시와 연출 | 캐릭터 표현과 카메라 및 HUD 분리 | [PauseCharacterView](Assets/Scripts/PauseCharacterView.cs) · [PauseBatchCamera](Assets/Scripts/PauseBatchCamera.cs) |
| 편집 가능한 맵 | 씬 오브젝트와 Tilemap 기반 편집 | [편집 기록](Docs/2026-10-22-발판과-옆성벽-Tilemap-편집.md) |

## 설계에서 다룬 문제

### 실제 이동과 예상 궤도의 일치

입력 기록과 이동 계산을 분리하고 실제 플레이와 예상 궤도에 같은 `PauseSimulation` 계산을 사용합니다. 예측은 이동 중 기믹 상태까지 고려해야 하므로 위치만으로 결과를 추정하는 방식과 구분합니다.

### 낙하 중 입력과 카메라 처리

큰 낙하가 발생하면 남은 점프를 취소하고 반동과 회복이 끝날 때까지 카메라가 추적하도록 구성했습니다. 조절값과 구현 범위는 [플레이 피드백 기록](Docs/2026-10-20-플레이-피드백.md)에서 확인합니다.

## 실행 방법

1. Unity Hub에서 저장소를 추가합니다.
2. **Unity 6000.3.22f1**로 엽니다.
3. `Assets/Scenes/MainScene.unity`를 엽니다.
4. Play Mode에서 실행합니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| Esc / 정지 버튼 | 시간 정지와 예약 재생 전환 |
| Space / 점프 버튼 | 누른 시간을 기록하고 놓을 때 점프 예약 |
| 마우스 휠 | 위아래 둘러보기 |
| 휠 클릭 / Esc | 자동 카메라 위치로 복귀 |

방향 전환은 벽 반사와 낙하 후 회복 처리에 의해 이루어집니다. 예약 점프가 남아 있으면 추가 입력이 제한됩니다.

## 프로젝트 구조

| 경로 | 역할 |
| --- | --- |
| [Assets/Scripts](Assets/Scripts) | 입력·시뮬레이션·기믹·표현·HUD |
| [Assets/Editor](Assets/Editor) | 편집 및 빌드 도구 |
| [Assets/Scenes](Assets/Scenes) | 플레이·편집 씬 |
| [Docs](Docs) | 개발 기록과 편집 방법 및 검증 자료 |
| [ProjectSettings](ProjectSettings) | Unity 설정 |

## 빌드와 검증 범위

Unity 메뉴 **일시정지 게임 → 확장판 빌드**에서 Windows 또는 Android 빌드를 생성합니다. 산출물은 `Build/Expansion`에 저장하며 Git에는 포함하지 않습니다.

`-expansionQA` 검사는 시작점의 제한된 도약과 장치별 정지 표시를 확인하는 검사입니다. 전체 코스 완주나 실제 Android 기기 성능을 증명하는 결과와 구분합니다.

- [검증 기록](Docs/Validation)
- [기존 개발 상세 기록](Docs/DevelopmentNotes.md)
- [발판 길이와 엔딩 어퍼컷 수정](Docs/2026-10-26-발판길이와-엔딩어퍼컷.md)

## 개발 자료와 리소스

- [프로젝트 작업 규칙](AGENTS.md)
- [코딩 규칙](Docs/unity_codex_coding_rules.md)
- [아트·팔레트 기록](Docs/Art)

기획 중인 스토리와 코드에 구현된 연출은 구분합니다. 외부 폰트와 리소스의 출처 및 라이선스는 해당 파일을 따릅니다.
