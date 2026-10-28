using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 물리 상태에 맞는 사람 도트 자세와 정지 색상을 표시하며 위치·판정은 변경하지 않습니다.
    /// Inspector의 캐릭터 연결에 씬 SpriteRenderer를 지정하거나 게임 관리자의 Initialize로 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseCharacterView : MonoBehaviour
    {
        #region Variables

        private const float INACTIVE_POSE_CLOCK = 99f;
        private const int ROPE_POSE_FRAME = 5;
        private const int LADDER_POSE_FRAME = 3;
        private const int RISING_POSE_FRAME = 4;
        private const int APEX_POSE_FRAME = 6;
        private const int FALLING_POSE_FRAME = 8;
        private const int LANDING_FIRST_FRAME = 9;
        private const int LANDING_FRAME_COUNT = 3;
        private const int HIGH_FALL_LAST_FRAME = 5;
        private const int BELLY_CONTACT_FRAME = 5;
        private const int BELLY_FIRST_ROLL_FRAME = 6;
        private const int BELLY_ROLL_FRAME_COUNT = 10;
        private const int EXPECTED_SPRITE_COUNT = 154;
        private const string RESOURCE_ROOT = "Characters/HumanPlayer/";
        private const int IDLE_POSE_COUNT = 4;
        private const float IDLE_POSE_TIME_RATIO = 0.5f;
        private enum ClipKind { Idle, Jump, WallUp, WallDown, BellyLoop, GetUp, Uppercut }



        // [TUNE] 아래 값은 표시 타이밍과 자세 기준만 바꾸며 이동·충돌 수식에는 영향을 주지 않습니다.
        [Header("사람 도트 표시 설정")]
        [SerializeField, Tooltip("벽에 닿은 뒤 반동 자세를 표시하는 시간입니다. 실제 물리 반사 시간은 바꾸지 않습니다.")]
        private float _wallPoseSeconds = 0.68f;
        [SerializeField, Range(0, 11), Tooltip("상승 벽 반동 원화가 자체적으로 방향을 바꾸는 첫 프레임입니다. 이 뒤의 원화를 반대로 선택해 충돌 직후 방향을 끝까지 유지합니다.")]
        private int _wallUpTurnFrame = 7;
        [SerializeField, Range(0, 11), Tooltip("하강 벽 반동 원화가 자체적으로 방향을 바꾸는 첫 프레임입니다. 이 뒤의 원화를 반대로 선택해 다시 벽 쪽을 보지 않게 합니다.")]
        private int _wallDownTurnFrame = 5;
        [SerializeField, Range(0, 11), Tooltip("문구를 향해 날아오르는 동안 유지할 어퍼컷 원화 프레임입니다. 도약 준비 이후 주먹을 올린 자세를 유지합니다.")]
        private int _uppercutFlightFrame = 5;
        [SerializeField, Range(0, 11), Tooltip("문구를 부순 뒤 하늘로 계속 올라갈 때 고정할 자세입니다. 현재 원화에서 주먹이 가장 높은 03번 프레임을 사용합니다.")]
        private int _skyUppercutFrame = 3;
        [SerializeField, Tooltip("일반 착지 뒤 압축 자세를 표시하는 시간입니다. 다음 예약 점프가 실행되면 즉시 도약 자세로 바뀝니다.")]
        private float _landingPoseSeconds = 0.24f;
        [SerializeField, Tooltip("정지했을 때 사람 도트를 실제 회색 픽셀로 바꿉니다. 다시 흐르면 원색으로 돌아옵니다.")]
        private bool _isGrayscaleWhenFrozen = true;
        [SerializeField, Tooltip("상승 자세를 정점 자세로 바꾸는 수직 속도 기준입니다. 실제 점프 힘은 바꾸지 않습니다.")]
        private float _risingPoseVelocity = 3f;
        [SerializeField, Tooltip("정점 자세를 하강 자세로 바꾸는 수직 속도 기준입니다.")]
        private float _fallingPoseVelocity = -2f;
        [SerializeField, Tooltip("긴 낙하의 엎어짐 준비 자세를 표시하기 시작할 낙하 거리입니다. 실제 배 반동 판정은 물리 설정을 따릅니다.")]
        private float _highFallPoseHeight = 3.5f;
        [SerializeField, Tooltip("긴 낙하 중 1m마다 진행할 준비 도트 수입니다. 배 반동 시작 이후에는 실제 모션 시간을 따릅니다.")]
        private float _highFallFramesPerMetre = 2.8f;
        [Header("원화 프레임 시간")]
        [SerializeField, Tooltip("사람 도트 일곱 모션의 프레임 시간을 Inspector에서 조절합니다. 배열 개수와 원화 순서는 유지하세요.")]
        private PauseCharacterTiming _timing = new PauseCharacterTiming();
        [Header("실시간 모션 · 실행 중 확인")]
        [SerializeField, Tooltip("현재 물리 상태에 맞춰 선택한 사람 모션입니다. 자세 변경 때만 Console에 사건을 남깁니다.")]
        private string _motionLabel = "사람 도트 준비";
        [SerializeField, Tooltip("현재 표시한 도트의 0부터 시작하는 프레임 번호입니다.")]
        private int _frameIndex;

        private bool _isLoaded, _hasDisplayed, _isWallDescending;
        private float _wallClock = INACTIVE_POSE_CLOCK, _landingClock = INACTIVE_POSE_CLOCK;
        private Vector3 _baseScale;
        #endregion

        #region Reference

        [Header("캐릭터 연결")]
        [SerializeField, Tooltip("씬에서 사람을 그릴 SpriteRenderer입니다. 위치는 게임 관리자가 배치합니다.")]
        private SpriteRenderer _playerRenderer;

        private readonly Dictionary<ClipKind, SpriteClip> _clips = new Dictionary<ClipKind, SpriteClip>();
        private readonly Dictionary<Sprite, Sprite> _grayscaleFrames = new Dictionary<Sprite, Sprite>();
        private readonly List<Texture2D> _grayscaleTextures = new List<Texture2D>();
        private Sprite _displayedSprite;

        private PauseSimulation _simulation;
        private bool _isPreview;

        #endregion

        #region Property

        public bool Ready => _isLoaded && _playerRenderer != null;
        public string MotionLabel => _motionLabel;
        public int FrameIndex => _frameIndex;

        public SpriteRenderer Renderer => _playerRenderer;
        public Sprite DisplayedSprite => _displayedSprite;
        #endregion

        #region Unity Event Method

        // Inspector에서 미리 연결한 표시 대상이 있으면 모션을 준비합니다.
        private void Awake()
        {
            InitializeFromInspector();
        }

        // 구독한 물리 사건과 직접 만든 회색 이미지 자원을 정리합니다.
        private void OnDestroy()
        {
            ReleaseViewResources();
        }

        #endregion

        #region Custom Method

        // 게임 관리자가 지정한 표시 대상을 연결하고 사람 PNG를 한 번만 읽습니다.
        public void Initialize(SpriteRenderer target)
        {
            _playerRenderer = target;
            if (_playerRenderer == null)
            {
                Debug.LogError("[사람 캐릭터] 표시할 SpriteRenderer가 연결되지 않았습니다.", this);
                return;
            }
            LoadClips();
            _baseScale = _playerRenderer.transform.localScale;
            ResetView();
        }

        // 실제 캐릭터와 같은 자세 설정을 복사한 보이지 않는 예측 전용 뷰를 준비합니다.
        public void InitializePreview(SpriteRenderer target, PauseCharacterView source)
        {
            _isPreview = true;
            _wallPoseSeconds = source._wallPoseSeconds; _wallUpTurnFrame = source._wallUpTurnFrame; _wallDownTurnFrame = source._wallDownTurnFrame;
            _landingPoseSeconds = source._landingPoseSeconds; _risingPoseVelocity = source._risingPoseVelocity; _fallingPoseVelocity = source._fallingPoseVelocity;
            _highFallPoseHeight = source._highFallPoseHeight; _highFallFramesPerMetre = source._highFallFramesPerMetre; _timing = source._timing;
            Initialize(target); target.enabled = false;
        }
        // 예측을 다시 만들 때 현재 실제 자세의 시계를 출발점으로 사용합니다.
        public void SeedPreview(PauseCharacterView source)
        {
            _wallClock = source._wallClock; _landingClock = source._landingClock; _isWallDescending = source._isWallDescending;
            _displayedSprite = source._displayedSprite; _hasDisplayed = source._hasDisplayed;
        }

        // [수정됨: 실제 물리 반동·회복 상태와 사람 PNG를 연결하고 정지 프레임을 보존합니다.]
        // [NOTE] 정지 중에는 회색만 바꾸고 뷰 시계·프레임·좌우를 모두 유지합니다.
        public void Draw(PauseSimulation sim, bool isFrozen, bool isEnding, float endingProgress, float dt, bool hasEndingHit = false)
        {
            if (sim == null || _playerRenderer == null) return;
            LoadClips();
            BindSimulation(sim);
            if (isFrozen && !isEnding && _hasDisplayed)
            {
                ApplySprite(_displayedSprite, true);
                return;
            }

            float elapsed = isFrozen ? 0f : Mathf.Max(0f, dt);
            _wallClock += elapsed;
            _landingClock += elapsed;
            if (isEnding)
                ShowFrame(ClipKind.Uppercut, !hasEndingHit ? Mathf.Min(_uppercutFlightFrame, Mathf.FloorToInt(endingProgress * _clips[ClipKind.Uppercut].Right.Length * 2f))
                    : _skyUppercutFrame, sim.Facing, hasEndingHit ? "엔딩 · 최고 어퍼컷 자세로 하늘 상승" : "엔딩 · 상승 어퍼컷");
            else if (sim.Recovering)
                ShowProgress(ClipKind.GetUp, sim.RecoveryProgress, sim.Facing, "마지막 착지 · 엎어져 일어나기");
            else if (sim.BellyBouncing)
                // [수정됨: 작은 반동에서도 실제 체공 진행률에 맞춰 앞구르기 한 바퀴를 모두 표시합니다.]
                ShowFrame(ClipKind.BellyLoop, sim.BellyContact ? BELLY_CONTACT_FRAME : BELLY_FIRST_ROLL_FRAME +
                    Mathf.Min(BELLY_ROLL_FRAME_COUNT - 1, Mathf.FloorToInt(Mathf.Clamp01(sim.BellyFlightProgress) * BELLY_ROLL_FRAME_COUNT)),
                    sim.Facing, "높은 낙하 · 배 반동 " + sim.BellyBounceCount + "회");
            else if (sim.OnRope)
                ShowFrame(ClipKind.Jump, ROPE_POSE_FRAME, sim.Facing, "밧줄 · 잡고 흔들리기");
            else if (sim.OnLadder)
                ShowFrame(ClipKind.Jump, LADDER_POSE_FRAME, sim.Facing, "사다리 · 매달려 오르기");
            else if (!sim.Grounded && _wallClock < Mathf.Max(0.1f, _wallPoseSeconds))
                ShowTimed(_isWallDescending ? ClipKind.WallDown : ClipKind.WallUp, _wallClock,
                    sim.Facing, _isWallDescending ? "벽 반동 · 하강" : "벽 반동 · 상승", false);
            else if (!sim.Grounded && sim.FallDistance >= _highFallPoseHeight)
                ShowFrame(ClipKind.BellyLoop, Mathf.Clamp(Mathf.FloorToInt((sim.FallDistance - _highFallPoseHeight) * _highFallFramesPerMetre), 0, HIGH_FALL_LAST_FRAME), sim.Facing, "높은 낙하 · 배 착지 준비");
            else if (!sim.Grounded)
                ShowFrame(ClipKind.Jump, sim.Velocity.y > _risingPoseVelocity ? RISING_POSE_FRAME : sim.Velocity.y > _fallingPoseVelocity ? APEX_POSE_FRAME : FALLING_POSE_FRAME,
                    sim.Facing, sim.Velocity.y > _risingPoseVelocity ? "점프 · 상승" : sim.Velocity.y > _fallingPoseVelocity ? "점프 · 정점" : "점프 · 하강");
            else if (_landingClock < Mathf.Max(0.05f, _landingPoseSeconds))
                ShowFrame(ClipKind.Jump, LANDING_FIRST_FRAME + Mathf.Min(LANDING_FRAME_COUNT - 1, Mathf.FloorToInt(_landingClock / Mathf.Max(0.02f, _landingPoseSeconds / LANDING_FRAME_COUNT))), sim.Facing, "점프 · 일반 착지");
            else
                ShowTimed(ClipKind.Idle, sim.MotionTime, sim.Facing, "대기 · 숨 고르기", true);
            ApplySprite(_displayedSprite, isFrozen && !isEnding);
        }

        // 어퍼컷 확대는 발 기준 Transform에만 적용하며 물리 판정과 다른 캐릭터의 배율은 바꾸지 않습니다.
        public void SetEndingScale(float multiplier)
        { if (_playerRenderer != null) _playerRenderer.transform.localScale = _baseScale * Mathf.Max(1f, multiplier); }

        // 재시작 때 이전 벽 반동·착지 연출과 프레임을 초기화합니다. 물리 위치는 건드리지 않습니다.
        public void ResetView()
        {
            _wallClock = _landingClock = INACTIVE_POSE_CLOCK;
            _isWallDescending = false;
            _hasDisplayed = false;
            _motionLabel = "사람 도트 준비";
            _frameIndex = 0;
            SetEndingScale(1f);
            if (_isLoaded && _playerRenderer != null)
            {
                ShowFrame(ClipKind.Idle, 0, 1, "대기 · 다음 점프 준비");
                ApplySprite(_displayedSprite, false);
            }
        }



        // 런타임 생성 이전에도 Inspector 참조가 있는 씬에서는 바로 사람 자세를 보여 줍니다.
        private void InitializeFromInspector()
        {
            if (_playerRenderer != null) Initialize(_playerRenderer);
        }

        // 좌우 PNG를 각각 읽고 파일명 순서와 원화의 프레임 시간을 그대로 유지합니다.
        private void LoadClips()
        {
            if (_isLoaded) return;
            // [수정됨: 코드 내부 시간 배열을 Inspector에서 편집하는 일곱 모션 프로필로 분리했습니다.]
            if (_timing == null) _timing = new PauseCharacterTiming();
            AddClip(ClipKind.Idle, "Idle", _timing.Idle, true);
            AddClip(ClipKind.Jump, "Jump", _timing.Jump, false);
            AddClip(ClipKind.WallUp, "WallBellyBounceUp", _timing.WallUp, false);
            AddClip(ClipKind.WallDown, "WallBellyBounceDown", _timing.WallDown, false);
            AddClip(ClipKind.BellyLoop, "HighFallBellyLoop", _timing.BellyLoop, true);
            AddClip(ClipKind.GetUp, "BellyGetUp", _timing.GetUp, false);
            AddClip(ClipKind.Uppercut, "EndingUppercut", _timing.Uppercut, false);
            _isLoaded = true;
            if (!_isPreview) Debug.Log("[사람 캐릭터] 사람 도트 154개와 좌우 모션을 준비했습니다. 기존 거북이 자산은 보존합니다.", this);
        }

        // 대기와 동작의 리소스 경로를 구분하고 빠진 모션은 오류로 알립니다.
        private void AddClip(ClipKind key, string folder, float[] durations, bool isLooping)
        {
            string basePath = RESOURCE_ROOT;
            Sprite[] right;
            Sprite[] left;
            if (key == ClipKind.Idle)
            {
                // 서 있는 기존 원화만 순환해 착지·기립 뒤에는 다시 웅크리지 않는 독립 대기를 만듭니다.
                right = LoadIdlePoses(basePath, "Right"); left = LoadIdlePoses(basePath, "Left");
                durations = Enumerable.Repeat(Mathf.Max(0.1f, durations[0]) * IDLE_POSE_TIME_RATIO, IDLE_POSE_COUNT).ToArray();
            }
            else
            {
                right = Resources.LoadAll<Sprite>(basePath + "Frames/" + folder + "/Right").OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
                left = Resources.LoadAll<Sprite>(basePath + "Frames/" + folder + "/Left").OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
            }
            _clips[key] = new SpriteClip(right, left, durations, isLooping);
            if (right.Length != durations.Length || left.Length != durations.Length || right.Any(sprite => sprite == null) || left.Any(sprite => sprite == null))
                Debug.LogError("[사람 캐릭터] " + folder + " 모션 프레임 연결이 부족합니다. Resources의 좌우 PNG를 확인하세요.", this);
        }

        // 발 위치가 같은 대기·최종 착지·최종 기립의 선 자세를 재사용하며 원본 PNG는 변경하지 않습니다.
        private static Sprite[] LoadIdlePoses(string root, string side)
        {
            Sprite idle = Resources.Load<Sprite>(root + "Idle/Idle_" + side);
            return new[] { idle, Resources.Load<Sprite>(root + "Frames/Jump/" + side + "/Jump_" + side + "_11"),
                idle, Resources.Load<Sprite>(root + "Frames/BellyGetUp/" + side + "/BellyGetUp_" + side + "_11") };
        }

        // 다른 물리 모델을 받으면 이전 구독을 해제해 사건이 중복 처리되지 않게 합니다.
        private void BindSimulation(PauseSimulation sim)
        {
            if (ReferenceEquals(_simulation, sim)) return;
            if (_simulation != null) _simulation.EventRaised -= HandleMotionEvent;
            _simulation = sim;
            _simulation.EventRaised += HandleMotionEvent;
        }

        // 실제 벽 충돌과 착지 사건만 짧은 자세 연출의 시작점으로 사용합니다.
        private void HandleMotionEvent(ClimbEvent kind, ClimbPlatform platform)
        {
            if (kind == ClimbEvent.WallBounce)
            {
                _wallClock = 0f;
                _isWallDescending = _simulation.LastWallBounceDescending;
            }
            else if (kind == ClimbEvent.Land)
            {
                _landingClock = 0f;
                _wallClock = INACTIVE_POSE_CLOCK;
            }
            else if (kind == ClimbEvent.Jump || kind == ClimbEvent.Recovered || kind == ClimbEvent.TrampolineRest)
            {
                _wallClock = _landingClock = INACTIVE_POSE_CLOCK;
            }
        }

        // [NOTE] 원화의 각 프레임 표시 시간을 합산하며 실제 물리 시계와 정지 상태를 그대로 따릅니다.
        private void ShowTimed(ClipKind key, float seconds, int facing, string label, bool isRepeating)
        {
            SpriteClip clip = _clips[key];
            float time = isRepeating && clip.IsLooping ? Mathf.Repeat(Mathf.Max(0f, seconds), clip.TotalSeconds) : Mathf.Clamp(seconds, 0f, clip.TotalSeconds);
            int index = clip.Durations.Length - 1;
            for (int i = 0; i < clip.Durations.Length; i++)
            {
                if (time < clip.Durations[i]) { index = i; break; }
                time -= clip.Durations[i];
            }
            ShowFrame(key, index, facing, label);
        }

        // 물리 회복 진행률에 맞춰 엎어짐부터 기상까지의 모든 도트를 순서대로 표시합니다.
        private void ShowProgress(ClipKind key, float progress, int facing, string label)
        {
            ShowTimed(key, _clips[key].TotalSeconds * Mathf.Clamp01(progress), facing, label, false);
        }

        // [수정됨: 벽 충돌 직후부터 새 방향을 보며 원화 내부의 후반 방향 전환도 보정합니다.]
        private void ShowFrame(ClipKind key, int index, int facing, string label)
        {
            SpriteClip clip = _clips[key];
            bool hasAuthoredTurn = (key == ClipKind.WallUp && index >= _wallUpTurnFrame) ||
                (key == ClipKind.WallDown && index >= _wallDownTurnFrame);
            int spriteFacing = hasAuthoredTurn ? -facing : facing;
            Sprite[] frames = spriteFacing < 0 ? clip.Left : clip.Right;
            if (frames.Length == 0) return;
            _frameIndex = Mathf.Clamp(index, 0, frames.Length - 1);
            _displayedSprite = frames[_frameIndex];
            _hasDisplayed = _displayedSprite != null;
            if (_motionLabel != label)
            {
                _motionLabel = label;
                if (!_isPreview) Debug.Log("[사람 캐릭터] " + label, this);
            }
        }

        // 정지 중에만 회색 사본을 쓰며 SpriteRenderer의 좌우 반전은 항상 꺼 둡니다.
        private void ApplySprite(Sprite source, bool isFrozen)
        {
            if (source == null || _playerRenderer == null) return;
            _playerRenderer.sprite = isFrozen && _isGrayscaleWhenFrozen ? GetGrayscale(source) : source;
            _playerRenderer.flipX = false;
            _playerRenderer.color = Color.white;
        }

        // 원본 PNG를 수정하지 않고 현재 보이는 프레임의 회색 사본만 필요할 때 생성합니다.
        private Sprite GetGrayscale(Sprite source)
        {
            if (_grayscaleFrames.TryGetValue(source, out Sprite cached)) return cached;
            int width = Mathf.RoundToInt(source.rect.width), height = Mathf.RoundToInt(source.rect.height);
            Color[] pixels = source.texture.GetPixels(Mathf.RoundToInt(source.rect.x), Mathf.RoundToInt(source.rect.y), width, height);
            for (int index = 0; index < pixels.Length; index++)
            {
                float gray = pixels[index].grayscale;
                pixels[index] = new Color(gray, gray, gray, pixels[index].a);
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = source.name + " · 정지 회색";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(source.pivot.x / width, source.pivot.y / height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = source.name + " · 정지 회색";
            _grayscaleFrames.Add(source, sprite);
            _grayscaleTextures.Add(texture);
            return sprite;
        }

        // 뷰가 소유한 회색 사본만 해제하며 Resources 원본 이미지와 기존 거북이 자산은 건드리지 않습니다.
        private void ReleaseViewResources()
        {
            if (_simulation != null) _simulation.EventRaised -= HandleMotionEvent;
            foreach (Sprite sprite in _grayscaleFrames.Values) if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in _grayscaleTextures) if (texture != null) Destroy(texture);
            _grayscaleFrames.Clear();
            _grayscaleTextures.Clear();
        }

        /// <summary>
        /// 한 모션의 좌우 PNG와 원화별 프레임 시간을 함께 보관합니다.
        /// Inspector 연결은 상위 뷰에서 관리하며 이 자료에는 별도 컴포넌트가 필요하지 않습니다.
        /// </summary>
        private sealed class SpriteClip
        {
            internal Sprite[] Right { get; }
            internal Sprite[] Left { get; }
            internal float[] Durations { get; }
            internal float TotalSeconds { get; }
            internal bool IsLooping { get; }

            // 각 자세의 표시 시간을 저장해 원화 프레임 타이밍을 재사용합니다.
            internal SpriteClip(Sprite[] right, Sprite[] left, float[] durations, bool isLooping)
            {
                Right = right;
                Left = left;
                Durations = durations;
                IsLooping = isLooping;
                TotalSeconds = durations.Sum();
            }
        }

        #endregion

    }
}
