using System;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>성 중앙을 통과한 뒤의 가로 추적과 성 외부 노출 범위를 관리합니다. Camera와 실제 코스 벽을 초기화 때 전달합니다.</summary>
    [Serializable]
    public sealed class PauseCastleFraming
    {
        #region Variables
        [Header("성 가장자리 화면 제한")]
        [SerializeField, Min(0f), Tooltip("성의 왼쪽 또는 오른쪽 끝에서 추가로 보일 수 있는 바깥 풍경의 폭입니다.")] private float _exteriorMargin = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("성 중앙을 지난 뒤 가로로 따라가는 완충 시간입니다.")] private float _followSeconds = 0.12f;
        [SerializeField, Tooltip("실제 외벽의 안쪽 왼쪽 경계입니다. 게임 초기화 때 씬에서 읽습니다.")] private float _left = -12f;
        [SerializeField, Tooltip("실제 외벽의 안쪽 오른쪽 경계입니다. 게임 초기화 때 씬에서 읽습니다.")] private float _right = 12f;
        [SerializeField, Tooltip("캐릭터가 성 중앙에 도달해 가로 추적을 시작했는지 표시합니다.")] private bool _hasCrossedCenter;
        private float _previousX, _velocity;
        private bool _hasPrevious;
        #endregion
        #region Property
        public bool HasCrossedCenter => _hasCrossedCenter;
        public float Left => _left;
        public float Right => _right;
        public float ExteriorMargin => Mathf.Max(0f, _exteriorMargin);
        #endregion
        #region Custom Method
        // 외벽의 안쪽 면을 읽으며, 벽이 없는 검사 코스에서는 기본 경계를 사용합니다.
        public void Configure(PauseLevel level)
        {
            if (level == null) return;
            ClimbWall[] walls = level.Walls.Where(wall => wall.Top - wall.Bottom > 100f).ToArray();
            if (walls.Length < 2) return;
            ClimbWall first = walls.OrderBy(wall => wall.X).First(), last = walls.OrderBy(wall => wall.X).Last();
            _left = first.X + first.Width * 0.5f; _right = last.X - last.Width * 0.5f;
        }
        // 재시작은 중앙 통과 기록과 가로 속도를 비웁니다.
        public void Reset() { _hasCrossedCenter = _hasPrevious = false; _velocity = 0f; }
        // 중앙에 닿거나 한 프레임 사이에 통과한 경우를 놓치지 않고 추적을 한 번 시작합니다.
        public float Follow(Camera camera, float playerX, float deadZone, float dt)
        {
            float center = (_left + _right) * 0.5f;
            if (!_hasCrossedCenter && (Mathf.Abs(playerX - center) <= deadZone ||
                (_hasPrevious && (_previousX - center) * (playerX - center) <= 0f)))
            { _hasCrossedCenter = true; Debug.Log("[카메라] 성 중앙 통과 · 가로 추적을 시작합니다."); }
            _previousX = playerX; _hasPrevious = true;
            float current = camera.transform.position.x;
            float target = center;
            if (_hasCrossedCenter)
            {
                float difference = playerX - current;
                target = Mathf.Abs(difference) <= deadZone ? current : playerX - Mathf.Sign(difference) * deadZone;
            }
            target = Clamp(camera, target);
            return Clamp(camera, Mathf.SmoothDamp(current, target, ref _velocity, Mathf.Max(0.01f, _followSeconds), Mathf.Infinity, dt));
        }
        // 화면이 성보다 넓으면 성 중앙에 고정합니다. 그 외에는 바깥 풍경이 지정 폭 이상 늘어나지 않게 합니다.
        public float Clamp(Camera camera, float x)
        {
            float half = camera.orthographicSize * camera.aspect;
            float minimum = _left - ExteriorMargin + half, maximum = _right + ExteriorMargin - half;
            return minimum > maximum ? (_left + _right) * 0.5f : Mathf.Clamp(x, minimum, maximum);
        }
        #endregion
    }
}
