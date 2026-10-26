using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 낙하 후 현재 위치에서 점프로 닿을 수 있는 위쪽 발판을 찾아 바라볼 방향을 정합니다.
    /// 실제 이동과 예상 궤도가 같은 순수 함수를 사용하며 Inspector 설정은 MovementTuning에 있습니다.
    /// </summary>
    public static class PausePlatformDirection
    {
        #region Variables
        private const float HEIGHT_EPSILON = 0.05f;
        private const float HORIZONTAL_EPSILON = 0.1f;
        #endregion

        #region Custom Method
        // 최대 점프의 하강 교차 시간으로 수평 도달 범위를 구하고 가까운 위 발판을 우선합니다.
        public static int FindAbove(PauseLevel level, Vector2 feet, MovementTuning tuning, int fallback)
        {
            float maximumHeight = tuning.MaximumJump * tuning.MaximumJump / (2f * tuning.Gravity);
            float bestScore = float.PositiveInfinity;
            int direction = fallback;
            foreach (ClimbPlatform platform in level.Platforms)
            {
                float rise = platform.Y - feet.y;
                if (platform.Broken || rise <= HEIGHT_EPSILON || rise > maximumHeight) continue;
                float delta = platform.X - feet.x;
                if (Mathf.Abs(delta) < HORIZONTAL_EPSILON) continue;
                float time = (tuning.MaximumJump + Mathf.Sqrt(Mathf.Max(0f,
                    tuning.MaximumJump * tuning.MaximumJump - 2f * tuning.Gravity * rise))) / tuning.Gravity;
                float nearestEdge = Mathf.Max(0f, Mathf.Abs(delta) - platform.Width * 0.5f - tuning.CollisionHalfWidth);
                if (nearestEdge > tuning.MaximumForward * time) continue;
                float score = rise * tuning.AutoFaceHeightWeight + nearestEdge;
                if (score >= bestScore) continue;
                bestScore = score; direction = delta < 0f ? -1 : 1;
            }
            return direction;
        }
        #endregion
    }
}
