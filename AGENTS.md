# 일시정지! 프로젝트 작업 규칙

사용자가 제공한 [Unity C# 코딩 기본 규칙](Docs/unity_codex_coding_rules.md)을 게임 자체 `Assets/Scripts`와 `Assets/Editor` 코드에 적용한다. Packages와 외부 플러그인 코드는 변경하지 않는다.

- Variables / Reference / Property / Unity Event Method / Custom Method 구역을 구분한다.
- 클래스 XML 요약에는 역할과 필수 연결을 한국어로 적는다. 기능 함수 앞에는 목적을 주석으로 적는다.
- private 필드는 `_camelCase`, bool 상태는 is/has/can 접두어, 상수는 UPPER_SNAKE_CASE를 사용한다. 직렬화 필드 이름을 바꾸면 FormerlySerializedAs로 기존 Inspector 값을 보존한다.
- 조절값은 private SerializeField와 한국어 Header/Tooltip으로 제공한다. 값 검증은 읽기 프로퍼티에서 한다.
- 입력과 HUD는 Update, 실제 이동은 FixedUpdate에서 처리한다. 예측과 실제 이동은 같은 PauseSimulation 계산을 사용한다.
- 필수 컴포넌트는 RequireComponent를 지정하고 시작할 때 캐싱한다. Update 반복 탐색은 금지한다.
- [TUNE], [NOTE], [수정됨: 이유]로 중요한 수정과 물리 예외의 이유를 남긴다.
- Console 로그는 [시스템명] 내용으로 초기화·입력·상태 변경·오류 사건에만 남긴다. 매 프레임 도배하지 않는다.
- Inspector는 설정과 연결, HUD는 현재 상태, Console은 방금 발생한 사건을 설명한다.
- MainScene의 발판·벽·기믹은 편집 가능한 실제 오브젝트다. 사용자가 수정한 씬을 전체 재생성하지 말고 기존 값과 ID를 보존한다.
- 작업 완료 전 컴파일, 실제 씬 연결, 관련 모델 및 실행 검증을 구분해 확인한다.
