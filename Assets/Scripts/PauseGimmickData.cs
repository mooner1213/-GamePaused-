using UnityEngine;

namespace PauseClimb
{
    // 씬에서 설정한 장치의 역할을 물리 계산과 예상 궤도가 같은 이름으로 구분합니다.
    public enum GimmickKind { Portal, Rope, Ladder, Trampoline, Wind }

    /// <summary>
    /// 장치의 편집값을 보관하는 불변 데이터이며 로프만 위치를 위 고정점으로 사용합니다.
    /// Inspector 필수 연결과 수치 편집은 PauseGimmickObject가 담당합니다.
    /// </summary>
    public sealed class ClimbGimmick
    {
        #region Property
        public int Id { get; }
        public GimmickKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public int TargetId { get; }
        public Vector2 ExitDirection { get; }
        public float RopeLength { get; }
        public float SwingSpeed { get; }
        public float SwingAmplitude { get; }
        public float ClimbSpeed { get; }
        public Vector2 WindAcceleration { get; }
        public float BounceRatio { get; }
        #endregion

        #region Custom Method
        // 포탈은 짝 장치 번호, 사다리는 도착 발판 번호를 받으며 장치별 공통 설정을 안전한 범위로 보관합니다.
        public ClimbGimmick(int id, GimmickKind kind, Vector2 position, Vector2 size,
            int targetId = -1, Vector2 exitDirection = default, float ropeLength = 4f,
            float swingSpeed = 1.1f, float swingAmplitude = 42f, float climbSpeed = 3.5f,
            Vector2 windAcceleration = default, float bounceRatio = 0.6666667f)
        {
            Id = id; Kind = kind; Position = position;
            Size = new Vector2(Mathf.Max(0.05f, Mathf.Abs(size.x)), Mathf.Max(0.05f, Mathf.Abs(size.y)));
            TargetId = targetId;
            ExitDirection = exitDirection.sqrMagnitude > 0.001f ? exitDirection.normalized : Vector2.right;
            RopeLength = Mathf.Max(0.25f, ropeLength);
            SwingSpeed = Mathf.Max(0f, swingSpeed);
            SwingAmplitude = Mathf.Clamp(swingAmplitude, 0f, 80f);
            ClimbSpeed = Mathf.Max(0.1f, climbSpeed);
            WindAcceleration = windAcceleration;
            BounceRatio = Mathf.Clamp01(bounceRatio);
        }

        // [NOTE] 미부착 로프의 기본 끝점만 구하며 매달린 캐릭터의 각도는 시뮬레이션 상태를 사용합니다.
        public Vector2 RopeEndAt(float worldTime)
        {
            float angle = Mathf.Sin(worldTime * SwingSpeed) * SwingAmplitude * Mathf.Deg2Rad;
            return Position + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * RopeLength;
        }
        #endregion
    }
}
