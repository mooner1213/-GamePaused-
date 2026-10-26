// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using UnityEngine;
namespace PauseClimb
{
    // 발판 루트의 이동·회전·크기를 실제 착지 표면으로 변환합니다.
    /// <summary>
    /// 발판 루트의 이동·회전·크기를 실제 착지 표면으로 변환합니다.
    /// Inspector 연결 대상은 직렬화된 오브젝트 참조 항목에서 확인합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PausePlatformObject : MonoBehaviour
    {
        #region Variables
        [UnityEngine.Serialization.FormerlySerializedAs("id")]
        [Header("발판 역할")]
        [SerializeField, Tooltip("기존 코스 식별자입니다. 마지막 연속 도약은 20~24번이므로 해당 번호는 유지하세요.")] private int _id;
        [UnityEngine.Serialization.FormerlySerializedAs("kind")]
        [SerializeField, Tooltip("돌·기름·이동·타이머·금간·정상 역할을 결정합니다.")] private PlatformKind _kind;
        [Header("발이 서는 입체 윗면")]
        [SerializeField, Min(0f), Tooltip("발판 그림의 윗부분이 실제 발보다 올라오는 거리입니다. 0.2이면 윗면 안쪽에 서 보입니다. 실제 충돌 좌표는 유지합니다.")] private float _visualSurfaceInset = 0.2f;
        [UnityEngine.Serialization.FormerlySerializedAs("amplitude")]
        [Header("왕복 이동")]
        [SerializeField, Min(0), Tooltip("기준 위치에서 좌우로 이동하는 거리입니다.")] private float _amplitude;
        [UnityEngine.Serialization.FormerlySerializedAs("speed")]
        [SerializeField, Min(0), Tooltip("왕복 이동의 속도입니다.")] private float _speed;
        [UnityEngine.Serialization.FormerlySerializedAs("phase")]
        [SerializeField, Tooltip("이동 시작 시점의 위상입니다. 씬 위치를 왕복의 중심으로 사용합니다.")] private float _phase;
        #endregion
        #region Reference
        [UnityEngine.Serialization.FormerlySerializedAs("surface")]
        [Header("착지 표면 연결")]
        [SerializeField, Tooltip("상단 표면입니다. 이 오브젝트의 가로 길이와 기울기가 실제 충돌 판정입니다.")] private SpriteRenderer _surface;
        [Header("팔레트 디자인 교체")]
        [SerializeField, Tooltip("다른 테마의 발판 타일을 넣으면 편집기에서 모양과 재질을 바꿉니다. 기존 위치·크기·경사는 유지합니다.")] private PauseDesignTile _designTile;
        #endregion
        #region Property
        public int Id => _id;
        public PauseDesignTile DesignTile => _designTile;
        public float VisualSurfaceInset => Mathf.Max(0f, _visualSurfaceInset);
        #endregion
        #region Unity Event Method
        // 선택한 발판의 실제 착지 구간을 녹색 선으로 표시합니다.
        private void OnDrawGizmosSelected() { DrawSurface(); }
        #endregion
        #region Custom Method
        // 기존 코스의 역할과 이동 설정을 씬 컴포넌트에 보관합니다.
        public void Configure(ClimbPlatform data)
        { _id = data.Id; _kind = data.Kind; _amplitude = data.MoveAmplitude; _speed = data.MoveSpeed; _phase = data.MovePhase; _surface = transform.Find("발판 표면").GetComponent<SpriteRenderer>(); }
        // 표면 스프라이트의 실제 월드 좌표를 읽으므로 이동·크기·회전 편집이 판정에 반영됩니다.
        public ClimbPlatform ReadSurface()
        {
            if (_surface == null || _surface.sprite == null) throw new System.InvalidOperationException("[맵] " + name + "의 착지 표면이 없습니다.");
            float half = _surface.sprite.bounds.size.x * 0.5f;
            Vector3 left = _surface.transform.TransformPoint(new Vector3(-half, _surface.sprite.bounds.max.y, 0));
            Vector3 right = _surface.transform.TransformPoint(new Vector3(half, _surface.sprite.bounds.max.y, 0));
            if (Mathf.Abs(right.x-left.x) < 0.01f) throw new System.InvalidOperationException("[맵] 발판은 수직으로 세울 수 없습니다: " + name);
            Vector3 center = (left + right) * 0.5f;
            return new ClimbPlatform(_id, _kind, center.x, center.y, Mathf.Abs(right.x-left.x), (right.y-left.y)/(right.x-left.x))
            { MoveAmplitude = _amplitude, MoveSpeed = _speed, MovePhase = _phase };
        }
        // 실제 물리 표면과 이동 범위를 편집 화면에서 확인합니다.
        private void DrawSurface()
        {
            if (_surface == null || _surface.sprite == null) return;
            ClimbPlatform data = ReadSurface(); Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(data.X-data.Width/2, data.TopAt(data.X-data.Width/2)), new Vector3(data.X+data.Width/2, data.TopAt(data.X+data.Width/2)));
            if (_amplitude > 0) { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(transform.position, new Vector3(_amplitude*2, 0.15f, 0)); }
        }
        #endregion
    }
}
