using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PauseClimb
{
    /// <summary>
    /// 씬에 저장된 발판·원경·정지 색상을 실제 게임 상태와 연결합니다.
    /// Inspector에 편집 맵 루트와 먼 배경을 연결하며 맵 제작은 Editor 도구가 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseWorldView : MonoBehaviour
    {
        #region Variables

        [Header("씬 배경 연결")]
        [FormerlySerializedAs("visualRoot")]
        [SerializeField, Tooltip("저장한 발판·반사 벽·배경이 들어 있는 실제 씬 루트입니다. 플레이 중 새 맵을 생성하지 않습니다.")]
        private Transform _visualRoot;
        [FormerlySerializedAs("distantRoot")]
        [SerializeField, Tooltip("카메라를 따라 천천히 움직일 먼 배경입니다. 비워 두면 원경 이동만 생략합니다.")]
        private Transform _distantRoot;
        [Header("원경과 붕괴 안내")]
        [FormerlySerializedAs("parallaxAmount")]
        [SerializeField, Range(0f, 0.15f), Tooltip("카메라 높이에 따른 원경 이동 비율입니다. 0이면 배경을 고정합니다.")]
        private float _parallaxAmount = 0.055f;
        [SerializeField, Min(0.01f), Tooltip("물리 설정이 전달되지 않은 구형 연결의 게이지 기준 시간입니다. 게임 관리자가 물리 설정을 전달하면 실제 붕괴 시간을 공유합니다.")]
        private float _crumbleGaugeSeconds = 3f;

        private Vector3 _distantOrigin;
        private bool _isInitialized, _hasFrozenState, _isLastFrozen;
        #endregion

        #region Reference

        private readonly List<PausePlatformVisual> _platformVisuals = new List<PausePlatformVisual>();
        private PauseLevel _level;
        private MovementTuning _tuning;
        private Camera _worldCamera;
        private PauseFrozenPalette _palette;
        private PauseSkyBackdrop _skyBackdrop;
        private PauseDesignWorldView _designWorld;
        private PausePaintedCourse _paintedCourse;

        #endregion

        #region Property

        public Transform VisualRoot => _visualRoot;
        public Transform DistantRoot => _distantRoot;
        #endregion

        #region Unity Event Method

        // 뷰가 만든 회색 사본만 정리하고 저장된 씬과 원본 PNG는 보존합니다.
        private void OnDestroy()
        {
            ReleaseRuntimeCache();
        }

        #endregion

        #region Custom Method

        // [수정됨: 씬 생성 코드를 Editor로 분리하고 저장된 표시 오브젝트만 한 번 캐시합니다.]
        public void Initialize(PauseLevel sourceLevel, Camera sourceCamera)
        {
            Initialize(sourceLevel, sourceCamera, null);
        }

        // 실제 물리 설정을 전달받으면 붕괴 제한 시간을 중복 조절하지 않고 그대로 공유합니다.
        public void Initialize(PauseLevel sourceLevel, Camera sourceCamera, MovementTuning tuning)
        {
            ReleaseRuntimeCache();
            if (sourceLevel == null || sourceCamera == null || _visualRoot == null)
            {
                Debug.LogError("[배경] 맵·카메라·씬 배경 루트를 모두 연결해야 표시할 수 있습니다.", this);
                return;
            }
            _level = sourceLevel;
            _tuning = tuning;
            _worldCamera = sourceCamera;
            _distantOrigin = _distantRoot == null ? Vector3.zero : _distantRoot.localPosition;
            CachePlatformViews();
            _skyBackdrop = _visualRoot.GetComponentInChildren<PauseSkyBackdrop>();
            if (_skyBackdrop != null) _skyBackdrop.Initialize();
            _paintedCourse = _visualRoot.GetComponentInChildren<PausePaintedCourse>();
            if (_paintedCourse != null) _paintedCourse.Bind(_level);
            _designWorld = _visualRoot.GetComponentInChildren<PauseDesignWorldView>();
            if (_designWorld != null) _designWorld.Initialize(_worldCamera);
            _palette = new PauseFrozenPalette(_visualRoot, _worldCamera);
            _isInitialized = true;
            DrawWorld(false);
            Debug.Log("[배경] 저장된 씬의 발판 " + _platformVisuals.Count + "개와 정지 색상을 연결했습니다.", this);
        }

        // [NOTE] 프레임 갱신에서는 캐시만 읽고 탐색·컴포넌트 조회·맵 생성을 하지 않습니다.
        public void DrawWorld(bool isFrozen)
        {
            if (!_isInitialized) return;
            float fullSeconds = _tuning == null ? _crumbleGaugeSeconds : Mathf.Max(Mathf.Epsilon, _tuning.CrumbleBreakSeconds);
            foreach (PausePlatformVisual visual in _platformVisuals) visual.Draw(fullSeconds);
            if (!isFrozen && _distantRoot != null)
                _distantRoot.localPosition = _distantOrigin + Vector3.up * (_worldCamera.transform.position.y * _parallaxAmount);
            if (!_hasFrozenState || _isLastFrozen != isFrozen)
            {
                _palette.SetFrozen(isFrozen);
                _isLastFrozen = isFrozen;
                _hasFrozenState = true;
            }
            if (_skyBackdrop != null) _skyBackdrop.Draw(isFrozen);
            if (_designWorld != null) _designWorld.Draw(_worldCamera.transform.position, isFrozen);
            if (_paintedCourse != null) _paintedCourse.Draw();
        }



        // 실제 식별자와 씬 표시를 초기화 때만 연결하여 비활성 레거시 맵을 섞지 않습니다.
        private void CachePlatformViews()
        {
            Dictionary<int, ClimbPlatform> dataById = new Dictionary<int, ClimbPlatform>();
            foreach (ClimbPlatform platform in _level.Platforms) dataById.Add(platform.Id, platform);
            foreach (PausePlatformObject source in _visualRoot.GetComponentsInChildren<PausePlatformObject>())
            {
                if (!dataById.TryGetValue(source.Id, out ClimbPlatform platform))
                {
                    Debug.LogWarning("[배경] 실제 판정이 없는 발판 표시를 건너뜁니다. 번호: " + source.Id, source);
                    continue;
                }
                _platformVisuals.Add(new PausePlatformVisual(source.transform, platform));
            }
        }

        // 재연결과 종료 때 회색 사본을 해제하고 표시 캐시만 비웁니다.
        private void ReleaseRuntimeCache()
        {
            _palette?.Release();
            _palette = null;
            _platformVisuals.Clear();
            _isInitialized = false;
            _hasFrozenState = false;
        }

        #endregion
    }
}
