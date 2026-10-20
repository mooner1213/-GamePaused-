// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using UnityEngine;
namespace PauseClimb
{
    // 벽 표시와 같은 범위를 반사 판정에 사용합니다.
    /// <summary>
    /// 벽 표시와 같은 범위를 반사 판정에 사용합니다.
    /// Inspector 연결 대상은 직렬화된 오브젝트 참조 항목에서 확인합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseWallObject : MonoBehaviour
    {
        #region Reference
        [UnityEngine.Serialization.FormerlySerializedAs("wall")]
        [Header("벽 반사면")]
        [SerializeField, Tooltip("표시의 폭·높이·위치를 반사 판정에 사용합니다. 수직으로 유지하고 이동·크기를 조절하세요.")] private SpriteRenderer _wall;
        #endregion
        #region Custom Method
        // 씬 변환 시 기존 벽 표시를 연결합니다.
        public void Configure(SpriteRenderer source) { _wall = source; }
        // 편집한 표시의 경계를 실제 반사 범위로 읽습니다.
        public ClimbWall ReadWall()
        {
            if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, 0)) > 0.01f) throw new System.InvalidOperationException("[맵] 반사 벽은 수직으로 배치하세요: " + name);
            Bounds bounds = _wall.bounds; return new ClimbWall(bounds.center.x, bounds.min.y, bounds.max.y, bounds.size.x);
        }
        #endregion
    }
}
