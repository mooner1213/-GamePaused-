using UnityEngine;
using UnityEngine.Tilemaps;

namespace PauseClimb
{
    /// <summary>
    /// 원본 도트의 종횡비를 보존하는 팔레트 타일입니다.
    /// 발판 타일은 '발판 추가' Tilemap에 그리면 실제 물리와 예상 궤도에 함께 등록됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "일시정지 게임/디자인 타일", fileName = "디자인 타일")]
    public sealed class PauseDesignTile : Tile
    {
        #region Variables
        [Header("그림과 판정 역할")]
        [SerializeField, Tooltip("켜면 발판 추가 레이어에서 실제 착지 표면으로 읽습니다. 성벽·창문 장식에는 끕니다.")]
        private bool _isPlatform;
        [SerializeField, Tooltip("일반·기름·금간·타이머 발판의 동작입니다. 기존 저장 번호와 물리 계산을 유지합니다.")]
        private PlatformKind _platformKind;
        [Header("창문 안쪽 투명 구멍")]
        [SerializeField, Tooltip("창문 레이어에서 성벽 배경을 뚫을 모양입니다. 원본 창틀은 그대로 두고 안쪽에서만 원경이 보입니다.")]
        private Sprite _windowOpening;
        [Header("한 칸 분할 정보 · 제작 도구에서 설정")]
        [SerializeField, Tooltip("켜져 있으면 여러 칸짜리 그림에서 분할한 한 칸입니다.")] private bool _isSection;
        [SerializeField, Tooltip("전체 묶음의 가로·세로 칸 수입니다.")] private Vector2Int _sectionSize;
        [SerializeField, Tooltip("묶음의 왼쪽 위에서 이 조각의 열·행 번호입니다.")] private Vector2Int _sectionCell;
        [SerializeField, Tooltip("연결된 발판 조각을 같은 물리 표면으로 묶을 재질 이름입니다.")] private string _sectionFamily;
        [SerializeField, Min(0f), Tooltip("그림의 맨 위에서 실제 발이 서는 윗면까지 내려오는 거리입니다.")] private float _surfaceInset = 0.2f;
        #endregion
        #region Reference
        [SerializeField, Tooltip("기존 발판 오브젝트의 디자인을 교체할 때 사용할 전체 그림입니다.")] private Sprite _sourceSprite;
        #endregion

        #region Property
        public bool IsPlatform => _isPlatform;
        public PlatformKind PlatformKind => _platformKind;
        public Sprite WindowOpening => _windowOpening;
        public bool IsSection => _isSection;
        public Vector2Int SectionSize => _sectionSize;
        public Vector2Int SectionCell => _sectionCell;
        public string SectionFamily => _sectionFamily;
        public bool HasSurface => _isPlatform && (!_isSection || _sectionCell.y == 0);
        public float SurfaceInset => Mathf.Max(0f, _surfaceInset);
        public Sprite SourceSprite => _sourceSprite == null ? sprite : _sourceSprite;
        #endregion

        #region Custom Method
        // 제작 도구가 그림과 동일한 배율의 X·Y 크기를 저장하여 키캡이나 창문을 찌그러뜨리지 않습니다.
        public void Configure(Sprite picture, float width, bool isPlatform, PlatformKind kind)
        {
            sprite = picture;
            _isSection = false;
            _isPlatform = isPlatform;
            _platformKind = kind;
            float scale = width / Mathf.Max(0.001f, picture.bounds.size.x);
            float height = picture.bounds.size.y * scale;
            // [NOTE] 발판은 셀 위쪽, 장식은 셀 가운데를 기준으로 칠합니다.
            Vector3 offset = -picture.bounds.center * scale;
            if (isPlatform) offset += Vector3.down * height * 0.5f;
            transform = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));
            colliderType = ColliderType.None;
            flags = TileFlags.None;
            color = Color.white;
        }
        // 창틀 타일의 안쪽 모양을 연결합니다. 실제 발판이나 이동 포탈 판정은 만들지 않습니다.
        public void ConfigureWindow(Sprite opening) { _windowOpening = opening; }
        // 분할된 그림 한 칸은 균일한 32픽셀 밀도를 사용하고 성벽 배경만 1.01배 겹칩니다.
        public void ConfigureSection(Sprite picture, Sprite whole, PauseDesignTile source, Vector2Int size, Vector2Int cell, bool isBackground)
        {
            Configure(picture, isBackground ? 1.01f : 1f, source.IsPlatform, source.PlatformKind);
            _isSection = true; _sourceSprite = whole; _sectionSize = size; _sectionCell = cell;
            _sectionFamily = source.name; _surfaceInset = 0.2f;
            transform = Matrix4x4.Scale(new Vector3(isBackground ? 1.01f : 1f, isBackground ? 1.01f : 1f, 1f));
        }
        #endregion
    }
}
