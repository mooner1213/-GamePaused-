// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 게임 상태와 입력·물리·표시 담당 기능을 연결하는 실행 관리자입니다.
    /// Inspector에서 실제 씬 캐릭터 SpriteRenderer와 사람 모션을 연결합니다.
    /// </summary>
    [RequireComponent(typeof(PauseWorldView), typeof(PauseHud), typeof(PauseTouchInput))]
    public sealed class PauseGame : MonoBehaviour
    {
        #region Variables
        [UnityEngine.Serialization.FormerlySerializedAs("movement")]
        [Header("점프와 반사 물리")]
        [SerializeField, Tooltip("점프 높이·거리·얼음 관성·벽 반사율을 조절합니다. 방향키 입력은 사용하지 않습니다.")] private MovementTuning _movement = new MovementTuning();
        [UnityEngine.Serialization.FormerlySerializedAs("pauseKey")]
        [Header("조작 설정")]
        [SerializeField, Tooltip("시간 정지와 재생을 전환합니다. 재생 중 다시 정지해도 남은 입력은 바꿀 수 없습니다.")] private KeyCode _pauseKey = KeyCode.Escape;
        [UnityEngine.Serialization.FormerlySerializedAs("jumpKey")]
        [SerializeField, Tooltip("정지 상태에서 누른 시간과 횟수만큼 점프를 예약합니다.")] private KeyCode _jumpKey = KeyCode.Space;
        [UnityEngine.Serialization.FormerlySerializedAs("maximumCommands")]
        [SerializeField, Range(4, 20), Tooltip("한 번에 예약할 수 있는 최대 점프 수입니다. 마지막 구간에는 최소 네 번이 필요합니다.")] private int _maximumCommands = 12;
        [UnityEngine.Serialization.FormerlySerializedAs("fullGuideHeight")]
        [Header("초반 예상 궤도")]
        [SerializeField, Min(0f), Tooltip("이 높이까지 연속 점프와 벽 반사 궤도를 100% 표시합니다.")] private float _fullGuideHeight = 6f;
        [UnityEngine.Serialization.FormerlySerializedAs("hiddenGuideHeight")]
        [SerializeField, Min(0.1f), Tooltip("이 높이까지 궤도가 서서히 사라집니다. 현재 높이가 낮아지면 다시 나타납니다.")] private float _hiddenGuideHeight = 12f;
        [UnityEngine.Serialization.FormerlySerializedAs("cameraSize")]
        [Header("화면과 소리")]
        [SerializeField, Range(6f, 9f), Tooltip("화면에 보이는 수직 범위의 절반입니다.")] private float _cameraSize = 7.2f;
        [UnityEngine.Serialization.FormerlySerializedAs("volume")]
        [SerializeField, Range(0f, 1f), Tooltip("배경음과 효과음의 공통 음량입니다.")] private float _volume = 0.35f;
        [UnityEngine.Serialization.FormerlySerializedAs("modeLabel")]
        [Header("실시간 상태 · 조절하지 않는 표시값")]
        [SerializeField, Tooltip("관찰·입력 기록·입력 잠금·글자 박치기 등의 현재 상태입니다.")] private string _modeLabel;
        [UnityEngine.Serialization.FormerlySerializedAs("lastEvent")]
        [SerializeField, Tooltip("최근 발생한 사건입니다. Console에도 한 번씩 기록됩니다.")] private string _lastEvent;
        [UnityEngine.Serialization.FormerlySerializedAs("currentHeight")]
        [SerializeField, Tooltip("현재 발 위치의 높이입니다. 궤도 노출은 최고 높이가 아닌 이 값으로 결정합니다.")] private float _currentHeight;
        [UnityEngine.Serialization.FormerlySerializedAs("bestHeight")]
        [SerializeField, Tooltip("이번 도전의 최고 높이입니다.")] private float _bestHeight;
        private readonly GameStateManager _state = new GameStateManager();
        private readonly JumpInputRecorder _recorder = new JumpInputRecorder();
        private bool _hasStarted, _isSettingsOpen, _isSoundFrozen, _isSummitReached, _isFinalBatch;
        private float _physicsAccumulator, _guideRefresh, _endingElapsed;
        private int _attempts, _finalChain, _bindingTarget;
        [Header("정상 엔딩 동작")]
        [SerializeField, Tooltip("어퍼컷의 시간·상승 높이·글자 충돌 높이를 조절합니다.")] private PauseEndingSequence _endingSequence = new PauseEndingSequence();
        [Header("예상 궤도 갱신")]
        [SerializeField, Min(0.01f), Tooltip("정지 중 예상 궤도를 다시 계산하는 간격입니다.")] private float _guideRefreshSeconds = 0.07f;
        internal bool DriverEnabled { get; set; }
        internal bool DriverHeld { get; set; }
        internal bool DriverPressed { get; set; }
        internal bool DriverToggle { get; set; }
        internal float DriverDelta { get; set; } = -1f;
        #endregion
        #region Reference
        private Camera _gameCamera;
        [UnityEngine.Serialization.FormerlySerializedAs("playerRenderer")]
        [SerializeField, Tooltip("씬에 배치한 임시 캐릭터 표시 오브젝트입니다.")] private SpriteRenderer _playerRenderer;
        private PauseWorldView _worldView;
        [UnityEngine.Serialization.FormerlySerializedAs("characterView")]
        [Header("사람 캐릭터 모션")]
        [SerializeField, Tooltip("점프·벽 반동·배 반동·회복·어퍼컷 도트를 실제 물리 상태와 연결합니다.")] private PauseCharacterView _characterView;
        private PauseHud _hud;
        private PauseTouchInput _touchInput;
        private PauseLevel _level;
        private PauseSimulation _simulation;
        private PauseTrajectory _trajectory;
        private PauseSoundPlayer _sounds;
        private PauseBatchCamera _batchCamera;
        #endregion
        #region Property
        public bool HasStarted => _hasStarted;
        public bool CameraBatchLocked => _batchCamera != null && _batchCamera.IsLocked;
        public bool CameraFollowingFall => _batchCamera != null && _batchCamera.IsFollowingFall;
        public bool EndingImpactTriggered => _endingSequence.HasHit;
        public bool IsRecording => _state.Phase == PlayPhase.Recording;
        public bool IsPaused => _state.IsPaused;
        public bool IsPlayingBack => _simulation != null && _simulation.Busy;
        public bool IsInputLocked => IsPlayingBack || _state.Phase == PlayPhase.CloudPaused;
        public bool IsFinished => _state.Phase == PlayPhase.Credits;
        public bool IsEndingJump => _state.Phase == PlayPhase.EndingJump;
        public bool IsSettingsOpen => _isSettingsOpen;
        public bool UseTouchControls => _touchInput != null && _touchInput.EnabledForPlatform;
        public bool SummitReached => _isSummitReached && _simulation.Grounded && _simulation.Support.Kind == PlatformKind.Goal;
        public float Height => _currentHeight;
        public float BestHeight => _bestHeight;
        public float Charge01 => Mathf.Clamp01(_recorder.HeldSeconds / _movement.ChargeSeconds);
        public float HeldSeconds => _recorder.HeldSeconds;
        public float MaximumCharge => _movement.ChargeSeconds;
        public float EndingElapsed => _endingElapsed;
        public float EndingOffset => _endingSequence.Offset;
        public int RecordedCount => _recorder.Count;
        public int PlaybackRemaining => _simulation.Remaining;
        public int Attempts => _attempts;
        public int FinalChain => _finalChain;
        public string LastEvent => _lastEvent;
        public string ModeLabel => _modeLabel;
        public KeyCode PauseKey => _pauseKey;
        public KeyCode JumpKey => _jumpKey;
        public int BindingTarget => _bindingTarget;
        public int Facing => _simulation.Facing;
        public Vector2 PlayerPosition => _simulation.Position;
        public Vector2 WordPosition => _simulation.Position + new Vector2(0f, _simulation.CollisionHeight + _endingSequence.WordOffset);
        public Camera GameCamera => _gameCamera;
        public PauseTouchInput TouchInput => _touchInput;
        public PauseTrajectory Trajectory => _trajectory;
        public IReadOnlyList<JumpCommand> Recording => _recorder.Commands;
        public PauseSimulation Simulation => _simulation;
        public PauseLevel Level => _level;
        public MovementTuning Tuning => _movement;
        public float CrumbleRemaining => _simulation.Grounded && _simulation.Support.Kind == PlatformKind.Crumble ? _simulation.Support.BreakRemaining : -1f;
        public float Volume { get => _volume; set { _volume = Mathf.Clamp01(value); ApplyVolume(); } }
        #endregion
        #region Unity Event Method
        // 장면의 연결값과 게임 모델을 준비합니다.
        private void Awake() { InitializeGame(); }
        // 생명주기에서는 입력 처리와 실제 기능을 호출만 합니다.
        private void Update() { TickGame(); }
        // [NOTE] 일반 플레이 물리는 고정 프레임에서만 진행하며 입력과 화면 갱신은 Update에 남깁니다.
        private void FixedUpdate() { TickFixedWorld(); }
        // 공유 정지 상태와 직접 만든 리소스를 정리합니다.
        private void OnDestroy() { ReleaseResources(); }
        #endregion
        #region Custom Method
        // 같은 모델을 실제 플레이·예상 궤도·완주 검증에서 공유합니다.
        private void InitializeGame()
        {
            PauseSceneMap sceneMap = FindFirstObjectByType<PauseSceneMap>();
            if (sceneMap == null) throw new InvalidOperationException("[맵] 편집 가능한 PauseSceneMap이 없습니다. MainScene을 열어 주세요.");
            _level = sceneMap.CreateLevel(); _simulation = new PauseSimulation(_level, _movement); _trajectory = new PauseTrajectory();
            _simulation.EventRaised += HandleSimulationEvent;
            _gameCamera = Camera.main;
            if (_gameCamera == null) { GameObject cameraObject = new GameObject("등반 카메라"); cameraObject.tag = "MainCamera"; _gameCamera = cameraObject.AddComponent<Camera>(); }
            _gameCamera.orthographic = true; _gameCamera.orthographicSize = _cameraSize;
            if (_gameCamera.GetComponent<AudioListener>() == null) _gameCamera.gameObject.AddComponent<AudioListener>();
            _recorder.CommandCommitted += HandleRecordedCommand;
            _batchCamera = GetComponent<PauseBatchCamera>() ?? gameObject.AddComponent<PauseBatchCamera>(); _batchCamera.Initialize(_gameCamera); _batchCamera.ResetToStart();
            _sounds = GetComponent<PauseSoundPlayer>() ?? gameObject.AddComponent<PauseSoundPlayer>(); _sounds.Initialize(_volume);
            if (_playerRenderer == null) throw new InvalidOperationException("[맵] 씬 캐릭터 연결이 없습니다.");
            _characterView = GetComponent<PauseCharacterView>() ?? gameObject.AddComponent<PauseCharacterView>();
            _characterView.Initialize(_playerRenderer);
            _worldView = GetComponent<PauseWorldView>() ?? gameObject.AddComponent<PauseWorldView>(); _worldView.Initialize(_level, _gameCamera, _movement);
            _touchInput = GetComponent<PauseTouchInput>() ?? gameObject.AddComponent<PauseTouchInput>();
            _hud = GetComponent<PauseHud>() ?? gameObject.AddComponent<PauseHud>(); _hud.Initialize(this);
            Announce("점프 전용 코스를 준비했습니다. 정지 중 눌렀다 놓으면 점프 한 번을 예약합니다."); DrawGame(0f);
        }
        // 실제 시간은 녹화와 붕괴 발판에, 고정 시간은 월드와 예약 점프에 사용합니다.
        private void TickGame()
        {
            float dt = DriverDelta > 0f ? DriverDelta : Mathf.Max(0f, Time.unscaledDeltaTime);
            _touchInput.Tick(_hasStarted && !_isSettingsOpen && !IsFinished);
            if (_isSettingsOpen) { HandleRebind(); DrawGame(0f); return; }
            if (!_hasStarted) { DrawGame(0f); return; }
            if (IsFinished) { _endingElapsed += dt; DrawGame(dt); return; }
            if (IsEndingJump) { TickEndingJump(dt); DrawGame(dt); return; }
            _level.TickCrumble(dt);
            bool isHeld = DriverEnabled ? DriverHeld : Input.GetKey(_jumpKey) || _touchInput.JumpHeld;
            bool isPressed = DriverEnabled ? DriverPressed : Input.GetKeyDown(_jumpKey) || _touchInput.JumpPressed;
            bool isToggle = DriverEnabled ? DriverToggle : Input.GetKeyDown(_pauseKey) || _touchInput.TogglePressed;
            DriverPressed = DriverToggle = false;
            if (IsRecording && SummitReached && isPressed) { StartEndingJump(); DrawGame(dt); return; }
            if (IsRecording) RecordJumpInput(isHeld, dt);
            if (isToggle) RequestPauseToggle();
            // [NOTE] 자동 검증 드라이버는 같은 물리 함수를 명시적 60Hz 시계로 재생합니다.
            if (DriverEnabled) AdvancePhysics(dt);
            _currentHeight = Mathf.Max(0f, _simulation.Position.y); _bestHeight = Mathf.Max(_bestHeight, _currentHeight);
            UpdateTrajectory(dt); DrawGame(dt);
        }
        // 생산 실행에서는 키 입력과 분리된 FixedUpdate만 실제 월드를 진행합니다.
        private void TickFixedWorld()
        {
            if (DriverEnabled) return;
            AdvancePhysics(Time.fixedDeltaTime);
        }
        // 실제 실행·자동 재생은 같은 고정 시간 시뮬레이션 호출을 공유합니다.
        private void AdvancePhysics(float deltaTime)
        {
            if (!_hasStarted || _isSettingsOpen || IsPaused || IsFinished || IsEndingJump) return;
            _physicsAccumulator += deltaTime;
            while (_physicsAccumulator + Mathf.Epsilon >= PauseSimulation.STEP)
            {
                _simulation.Tick(); _physicsAccumulator -= PauseSimulation.STEP;
                if (_state.Phase != PlayPhase.Playback || _simulation.Busy) continue;
                _state.Phase = PlayPhase.Observe; _batchCamera.ReleaseBatch();
                Announce("예약 점프와 자동 반동이 끝났습니다. 카메라가 캐릭터 위치로 이동합니다.");
            }
        }
        // 누르는 시간 한 덩어리를 점프 하나로 저장합니다. 입력 사이 대기 시간은 점프 명령으로 재생하지 않습니다.
        internal void RecordJumpInput(bool isHeld, float dt)
        { if (IsRecording && !SummitReached) _recorder.Record(isHeld, dt, _maximumCommands, _movement.ChargeSeconds); }
        // 현재 누른 입력을 한 번만 확정합니다.
        private void CommitHeldJump() { _recorder.Commit(_movement.ChargeSeconds); }
        // 입력 기록기는 물리나 카메라를 변경하지 않고 확정 사건만 전달합니다.
        private void HandleRecordedCommand(JumpCommand command, int count)
        { _guideRefresh = 0f; Announce($"점프 {count}번 예약 · {command.HoldSeconds:0.00}초"); }
        // 재생 중 ESC는 허용하되 남은 예약 명령은 절대 변경하지 않습니다.
        public void RequestPauseToggle()
        {
            if (!_hasStarted || _isSettingsOpen || IsFinished || IsEndingJump) return;
            if (IsRecording) { EndRecording(); return; }
            if (_state.Phase == PlayPhase.PlaybackPaused) { _state.Phase = PlayPhase.Playback; Announce("예약 점프를 같은 지점부터 이어 재생합니다."); return; }
            if (_state.Phase == PlayPhase.CloudPaused) { _state.Phase = PlayPhase.Observe; Announce("구름 위 정지를 해제합니다. 구름 구간은 진입 전에 연속 점프를 예약하세요."); return; }
            if (_simulation.Busy) { _state.Phase = PlayPhase.PlaybackPaused; Announce("재생 일시정지 · 남은 점프가 끝날 때까지 새 입력은 잠깁니다."); return; }
            if (_simulation.Grounded && _simulation.Support.Kind == PlatformKind.Cloud)
            { _state.Phase = PlayPhase.CloudPaused; Announce("구름 위에서는 새 계획을 만들 수 없습니다. 진입 전에 여러 번 예약해야 합니다."); return; }
            StartRecording();
        }
        // 정지 전 관성과 방향을 유지한 채 빈 점프 계획을 엽니다.
        private void StartRecording()
        {
            _state.Phase = PlayPhase.Recording; _recorder.Reset(); _guideRefresh = 0f;
            Announce(SummitReached ? "머리 위의 일시정지 글자를 점프로 깨세요!" : "일시정지 · 점프를 누른 시간과 횟수로 도약을 설계하세요.");
        }
        // 연속 점프 목록을 한 번에 넘기며 마지막 네 번이 같은 계획인지 표시합니다.
        private void EndRecording()
        {
            CommitHeldJump();
            _isFinalBatch = !SummitReached && _simulation.Grounded && _simulation.Support.Id == _level.FinalStartPlatformId && _recorder.Count == _level.FinalStepIds.Count;
            if (!SummitReached) _finalChain = 0;
            if (_recorder.Count == 0) { _state.Phase = PlayPhase.Observe; Announce("빈 계획을 닫았습니다."); return; }
            if (!_simulation.LoadCommands(_recorder.Commands)) { _state.Phase = PlayPhase.PlaybackPaused; Announce("남은 점프가 있어 계획을 바꿀 수 없습니다.", true); return; }
            _state.Phase = PlayPhase.Playback; _attempts++; _physicsAccumulator = 0f;
            _batchCamera.LockBatch();
            Announce($"{_recorder.Count}번의 예약 점프를 자동 재생합니다. 정지·재개는 가능하며 추가 입력은 잠깁니다.");
        }
        // 낮은 구간으로 다시 떨어지면 현재 높이에 맞춰 궤도가 즉시 돌아옵니다.
        private void UpdateTrajectory(float dt)
        {
            _trajectory.SetHeight(_simulation.Position.y, _fullGuideHeight, _hiddenGuideHeight);
            if (!IsPaused || _trajectory.Opacity <= 0f) { _trajectory.Hide(); return; }
            _guideRefresh -= dt;
            if (_guideRefresh > 0f) return;
            _guideRefresh = _guideRefreshSeconds;
            List<JumpCommand> candidate = new List<JumpCommand>(_recorder.Commands);
            if (_recorder.IsHolding && candidate.Count < _maximumCommands) candidate.Add(new JumpCommand(Mathf.Min(_recorder.HeldSeconds, _movement.ChargeSeconds)));
            _trajectory.Rebuild(_level, _simulation, candidate, IsInputLocked);
        }
        // 점프·착지·반사 사건에만 로그와 소리를 남겨 프레임 단위 도배를 막습니다.
        private void HandleSimulationEvent(ClimbEvent kind, ClimbPlatform platform)
        {
            switch (kind)
            {
                case ClimbEvent.Jump: PlayEffect(PauseSoundCue.Jump); Announce($"도약 {_simulation.LaunchedCount}번 · {(_simulation.Facing > 0 ? "오른쪽" : "왼쪽")}을 향해 점프합니다."); break;
                case ClimbEvent.WallBounce: PlayEffect(PauseSoundCue.Bounce); Announce("벽 박치기 · 바라보는 방향과 남은 수평 힘이 반사됐습니다."); break;
                case ClimbEvent.LongFall: _batchCamera.NotifyFallContact(_simulation.FallDistance); _isFinalBatch = false; _finalChain = 0; _isSummitReached = false; Announce("아래로 추락했습니다. 낮은 구간에서는 궤도가 다시 보입니다."); break;
                case ClimbEvent.BellyBounce: PlayEffect(PauseSoundCue.Bounce); Announce($"배 반동 {_simulation.BellyBounceCount}회 · 앞구르기로 다시 공중에 뜹니다."); break;
                case ClimbEvent.BellyRest: Announce("마지막 반동 · 엎드렸다가 일어납니다. 남은 예약 점프는 보존합니다."); break;
                case ClimbEvent.Recovered: Announce("일어나기 완료 · 다음 예약 점프를 이어갑니다."); break;
                case ClimbEvent.Portal: PlayEffect(PauseSoundCue.Bounce); Announce("포탈 이동 · 출구 방향으로 운동을 이어갑니다."); break;
                case ClimbEvent.RopeGrab: Announce("로프 자동 매달림 · 다음 점프는 줄이 움직이는 방향으로 발사됩니다."); break;
                case ClimbEvent.RopeRelease: PlayEffect(PauseSoundCue.Jump); Announce("로프에서 점프 · 흔들리는 방향의 운동량을 받았습니다."); break;
                case ClimbEvent.LadderGrab: Announce("사다리 자동 매달림 · 위 발판으로 올라갑니다."); break;
                case ClimbEvent.LadderExit: Announce("사다리 등반 완료 · 다음 점프를 준비합니다."); break;
                case ClimbEvent.Trampoline: PlayEffect(PauseSoundCue.Bounce); Announce("트램펄린 · 방금 낙하한 높이의 2/3만큼 자동 반동합니다."); break;
                case ClimbEvent.Land:
                    PlayEffect(PauseSoundCue.Land);
                    if (_isFinalBatch && _finalChain < _level.FinalStepIds.Count && platform.Id == _level.FinalStepIds[_finalChain] && _simulation.LaunchedCount == _finalChain + 1) _finalChain++;
                    else if (platform.Id != _level.FinalStartPlatformId) { _finalChain = 0; _isFinalBatch = false; }
                    if (platform.Kind == PlatformKind.Goal && _finalChain == _level.FinalStepIds.Count)
                    { _isSummitReached = true; Announce("한 계획으로 4연속 도약 성공! 정지 후 머리 위 글자를 점프로 깨세요."); }
                    else Announce($"{platform.Id}번 발판 · {platform.Y:0.0}m 착지" + (platform.Kind == PlatformKind.Crumble ? $" · 정지 중에도 {_movement.CrumbleBreakSeconds:0.##}초 뒤 붕괴!" : platform.Kind == PlatformKind.Cloud ? " · 다음 예약 점프로 즉시 통과!" : ""));
                    break;
            }
        }
        // 도트는 실제 모델의 바라보는 방향만 사용하며 경사 미끄러짐으로 임의 회전하지 않습니다.
        private void DrawGame(float dt)
        {
            if (_simulation == null) return;
            UpdateCameraViewport();
            bool isFrozen = IsPaused || _isSettingsOpen || !_hasStarted;
            _modeLabel = _simulation.Recovering ? "엎드림 · 회복 중" : _simulation.OnRope ? "로프 · 자동 매달림" : _simulation.OnLadder ? "사다리 · 자동 등반" : _simulation.BellyBouncing ? "배 반동 · 앞구르기" : IsFinished ? "저주 해제" : IsEndingJump ? "글자 박치기" : _isSettingsOpen ? "설정" : !_hasStarted ? "등반 준비" : IsPaused ? IsInputLocked ? "일시정지 · 입력 잠금" : "일시정지 · 도약 설계" : IsPlayingBack ? "연속 도약 · 화면 고정" : "관찰 · 다음 계획 준비";
            _characterView.Draw(_simulation, isFrozen, IsEndingJump, _endingSequence.Elapsed, dt);
            _playerRenderer.transform.position = new Vector3(_simulation.Position.x, _simulation.Position.y + _endingSequence.Offset, -1f);
            _batchCamera.Draw(_simulation, _level.GoalHeight, isFrozen, IsEndingJump || IsFinished, dt);
            if (CameraFollowingFall && !isFrozen)
                _modeLabel = _modeLabel.Replace(" · 화면 고정", string.Empty) + " · 낙하 화면 추적";
            _worldView.DrawWorld(isFrozen);
            if (_isSoundFrozen != isFrozen) { _isSoundFrozen = isFrozen; AudioListener.pause = isFrozen; }
        }
        // 태블릿의 4:3 화면에서도 HUD와 월드가 같은 16:9 안전 영역을 사용합니다.
        private void UpdateCameraViewport()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            PauseCanvasLayout.Calculate(UseTouchControls, out float scale, out Vector2 offset);
            float width = PauseCanvasLayout.CANVAS_WIDTH * scale;
            float height = PauseCanvasLayout.CANVAS_HEIGHT * scale;
            _gameCamera.pixelRect = new Rect(offset.x, Screen.height - offset.y - height, width, height);
        }
        // 타이틀 안내를 닫고 관찰 상태로 시작합니다.
        public void BeginGame() { _hasStarted = true; Announce("정지 → 점프 세기와 횟수 입력 → 재생. 방향 전환은 벽 박치기로만 가능합니다."); }
        // 설정은 녹화·재생 상태를 그대로 보존합니다. 누르던 입력은 현재 세기까지만 확정합니다.
        public void OpenSettings()
        { if (IsRecording) CommitHeldJump(); _recorder.IgnoreUntilReleased(); _isSettingsOpen = true; _bindingTarget = 0; Announce("설정을 열었습니다. 게임과 모든 발판 시간을 잠시 멈춥니다."); }
        // 설정을 닫아도 녹화가 자동 재생으로 바뀌지 않습니다.
        public void CloseSettings() { _isSettingsOpen = false; _bindingTarget = 0; Announce("설정을 닫았습니다. 열기 전의 기록·재생 상태를 유지합니다."); }
        // 프로젝트의 초기 코스를 그대로 복원합니다.
        public void RestartGame()
        {
            _level.Reset(); _simulation.Reset(_level.StartPosition); _recorder.Reset(); _state.Phase = PlayPhase.Observe;
            _hasStarted = true; _isSettingsOpen = _isSummitReached = _isFinalBatch = false;
            _currentHeight = _bestHeight = _physicsAccumulator = _endingElapsed = 0f;
            _attempts = _finalChain = _bindingTarget = 0; _endingSequence.Reset(); _characterView.ResetView(); _batchCamera.ResetToStart();
            _trajectory.Hide(); Announce("처음부터 다시 등반합니다. 걷기 없이 두 키로 도전하세요.");
        }
        // 두 기능의 키를 실제 키 입력으로 바꿉니다. 중복 키 지정은 허용하지 않습니다.
        public void BeginRebind(bool isJump) { _bindingTarget = isJump ? 2 : 1; Announce((isJump ? "점프" : "정지") + "에 사용할 키를 누르세요."); }
        // 설정 화면에서만 키 변경 대기를 처리합니다.
        private void HandleRebind()
        {
            if (_bindingTarget == 0 || !Input.anyKeyDown) return;
            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
            {
                if ((int)code >= (int)KeyCode.Mouse0 || !Input.GetKeyDown(code)) continue;
                if (code == (_bindingTarget == 2 ? _pauseKey : _jumpKey)) { Announce("점프와 정지에는 서로 다른 키가 필요합니다.", true); return; }
                if (_bindingTarget == 2) _jumpKey = code; else _pauseKey = code;
                _bindingTarget = 0; Announce("조작 키를 변경했습니다."); break;
            }
        }
        // 플랫폼의 정상 종료 기능을 사용합니다.
        public void QuitGame()
        {
            Announce("게임을 종료합니다.");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        // 정상의 정지 글자는 즉시 사라지지 않고 실제로 머리가 위로 이동해 닿아야 깨집니다.
        private void StartEndingJump()
        { _state.Phase = PlayPhase.EndingJump; _endingSequence.Reset(); Announce("머리 위 글자를 향해 어퍼컷합니다."); AudioListener.pause = false; PlayEffect(PauseSoundCue.Jump); }
        // 글자 충돌 후에도 어퍼컷의 하강·착지가 끝나야 크레딧을 시작합니다.
        private void TickEndingJump(float dt)
        {
            bool isHadHit = _endingSequence.HasHit; _endingSequence.Tick(dt);
            if (!isHadHit && _endingSequence.HasHit) { PlayEffect(PauseSoundCue.Ending); Announce("어퍼컷 적중! 글자를 깨고 발판으로 돌아옵니다."); }
            if (!_endingSequence.IsComplete) return;
            _state.Phase = PlayPhase.Credits; _endingElapsed = 0f; Announce("착지 완료 · 일시정지의 저주가 깨졌습니다.");
        }
        // 사건 하나를 HUD와 Console에 동시에 남깁니다.
        private void Announce(string message, bool isWarning = false)
        { _lastEvent = message; if (isWarning) Debug.LogWarning("[일시정지] " + message); else Debug.Log("[일시정지] " + message); }
        // 전체 음량은 음원 담당 컴포넌트에 전달합니다.
        private void ApplyVolume() { if (_sounds != null) _sounds.SetVolume(_volume); }
        // 사건 효과음은 정지 중에 재생하지 않습니다.
        private void PlayEffect(PauseSoundCue cue) { if (!IsPaused && !_isSettingsOpen) _sounds.Play(cue); }
        // 직접 생성한 그래픽과 음원만 해제합니다.
        private void ReleaseResources()
        {
            AudioListener.pause = false; if (_simulation != null) _simulation.EventRaised -= HandleSimulationEvent;
            _recorder.CommandCommitted -= HandleRecordedCommand;
        }
        #endregion
    }
}
