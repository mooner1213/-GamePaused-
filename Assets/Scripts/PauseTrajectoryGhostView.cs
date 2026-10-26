using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>예측 궤도를 점·번호 대신 같은 크기의 사람 잔상으로 표시합니다. 실제 캐릭터 뷰와 궤도를 초기화 때 연결합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseTrajectoryGhostView : MonoBehaviour
    {
        #region Variables
        [Header("예상 점프 잔상")]
        [SerializeField, Min(0), InspectorName("최대 잔상 수"), Tooltip("한 번에 표시할 잔상 수의 상한입니다. 0이면 기존처럼 전부 표시합니다. 줄이면 전체 예상 궤도에서 고르게 골라 표시하며, 예측된 자세 수보다 많아지지는 않습니다. 실행 중에도 즉시 반영됩니다.")] private int _maximumGhostCount;
        [SerializeField, Range(0.02f, 0.6f), Tooltip("각 잔상의 불투명도입니다. 낮출수록 세 프레임 간격으로 겹친 궤도가 옅게 보입니다.")] private float _opacity = 0.2f;
        [SerializeField, Tooltip("잔상의 색입니다. 원래 사람 PNG의 색과 곱해집니다.")] private Color _tint = new Color(0.8f, 1f, 0.96f, 1f);
        private int _visibleCount;
        #endregion
        #region Reference
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private PauseCharacterView _preview;
        private SpriteRenderer _player;
        private Transform _root;
        #endregion
        #region Property
        public int VisibleCount => _visibleCount;
        public int MaximumGhostCount => Mathf.Max(0, _maximumGhostCount);
        #endregion
        #region Custom Method
        // 실제 자세 계산을 재사용하되 예측 시 Console 상태 로그는 발생시키지 않습니다.
        public void Initialize(PauseTrajectory trajectory, PauseCharacterView character)
        {
            _player = character.Renderer;
            _root = new GameObject("3프레임 간격 점프 잔상 · 실행 표시").transform; _root.SetParent(transform, false);
            var calculator = new GameObject("예측 자세 계산 · 화면 비표시"); calculator.transform.SetParent(_root, false);
            SpriteRenderer hidden = calculator.AddComponent<SpriteRenderer>(); hidden.enabled = false;
            _preview = calculator.AddComponent<PauseCharacterView>(); _preview.InitializePreview(hidden, character);
            trajectory.BindPreview(_preview, character);
            Debug.Log("[점프 잔상] 같은 물리와 사람 모션으로 예상 궤도를 표시합니다. Inspector의 최대 잔상 수로 표시 개수를 조절할 수 있습니다. 0은 전체 표시입니다.", this);
        }
        // 미리 만든 렌더러를 재사용하여 카메라 이동·줌에서도 실제 캐릭터와 같은 월드 크기를 유지합니다.
        public void Draw(PauseTrajectory trajectory, bool isPaused)
        {
            if (_root == null) return;
            int count = isPaused && trajectory.Visible ? trajectory.Poses.Count : 0;
            if (MaximumGhostCount > 0) count = Mathf.Min(count, MaximumGhostCount);
            while (_pool.Count < count) AddRenderer();
            Color color = _tint; color.a = Mathf.Clamp01(_opacity) * trajectory.Opacity;
            for (int i = 0; i < _pool.Count; i++)
            {
                SpriteRenderer item = _pool[i]; item.enabled = i < count;
                if (i >= count) continue;
                // 개수를 줄여도 도착 지점까지 보이도록 기존 세 프레임 자세 중에서 고르게 선택합니다.
                int poseIndex = count <= 1 ? trajectory.Poses.Count - 1
                    : Mathf.RoundToInt(i * (trajectory.Poses.Count - 1f) / (count - 1));
                PauseTrajectory.GhostPose pose = trajectory.Poses[poseIndex];
                item.sprite = pose.Sprite; item.color = color;
                item.transform.position = new Vector3(pose.Feet.x, pose.Feet.y, _player.transform.position.z + 0.01f);
                item.transform.localScale = _player.transform.lossyScale;
            }
            _visibleCount = count;
        }
        // 풀 크기가 늘어날 때만 SpriteRenderer를 만들며 그림은 재생과 같은 PNG를 참조합니다.
        private void AddRenderer()
        {
            var item = new GameObject("예상 사람 잔상 " + _pool.Count); item.transform.SetParent(_root, false);
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>(); renderer.enabled = false;
            renderer.sharedMaterial = _player.sharedMaterial; renderer.sortingLayerID = _player.sortingLayerID;
            renderer.sortingOrder = _player.sortingOrder - 1; _pool.Add(renderer);
        }
        #endregion
    }
}
