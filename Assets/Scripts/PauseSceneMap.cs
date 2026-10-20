using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
namespace PauseClimb
{
    /// <summary>
    /// 저장된 씬 오브젝트의 편집값을 물리 모델과 예상 궤도에 전달합니다.
    /// Inspector에서 맵 루트와 출발 위치를 반드시 연결합니다.
    /// </summary>
    public sealed class PauseSceneMap : MonoBehaviour
    {
        #region Variables
        [Header("마지막 연속 도약")]
        [FormerlySerializedAs("finalStartPlatformId")]
        [SerializeField, Tooltip("마지막 네 번의 예약 점프를 시작할 준비 발판 번호입니다.")] private int _finalStartPlatformId = PauseLevel.FINAL_START_ID;
        [FormerlySerializedAs("finalStepIds")]
        [SerializeField, Tooltip("마지막 세 계단과 정상을 실제로 밟아야 하는 순서입니다.")] private int[] _finalStepIds = { 21, 22, 23, 24 };
        [Header("코스 버전")]
        [FormerlySerializedAs("expansionVersion")]
        [SerializeField, Tooltip("확장 생성기를 다시 실행했을 때 수동 배치를 덮어쓰지 않기 위한 버전입니다. 직접 조절하지 마세요.")] private int _expansionVersion;
        [Header("발판 복구 시간")]
        [SerializeField, Min(0.1f), Tooltip("사라진 구름과 붕괴 발판이 다시 나타날 때까지의 시간입니다.")] private float _restoreDelay = PauseLevel.DEFAULT_RESTORE_DELAY;
        #endregion
        #region Reference
        [Header("맵 연결")]
        [FormerlySerializedAs("mapRoot")]
        [SerializeField, Tooltip("플레이하지 않아도 수정할 수 있는 발판·벽·배경의 루트입니다.")] private Transform _mapRoot;
        [FormerlySerializedAs("spawn")]
        [SerializeField, Tooltip("새 게임과 재시작 때 캐릭터의 발이 놓일 위치입니다.")] private Transform _spawn;
        #endregion
        #region Property
        public Transform MapRoot => _mapRoot;
        public Transform Spawn => _spawn;
        public int FinalStartPlatformId => _finalStartPlatformId;
        public System.Collections.Generic.IReadOnlyList<int> FinalStepIds => _finalStepIds;
        public int ExpansionVersion => _expansionVersion;
        #endregion
        #region Custom Method
        // 변환한 씬의 맵과 출발 표시를 연결합니다.
        public void Configure(Transform root, Transform start) { _mapRoot = root; _spawn = start; }
        // [수정됨: 확장 코스 연결] 실제 원본과 최종 발판 순서를 저장하고 중복 생성 방지 표식을 남깁니다.
        public void ConfigureExpanded(Transform root, Transform start, int finalStart, int[] finalSteps, int version)
        { Configure(root, start); _finalStartPlatformId = finalStart; _finalStepIds = (int[])finalSteps.Clone(); _expansionVersion = version; }
        // 활성 오브젝트만 읽어 실제 게임과 예상 궤도가 같은 씬 좌표를 사용하게 합니다.
        public PauseLevel CreateLevel()
        {
            if (_mapRoot == null || _spawn == null) throw new System.InvalidOperationException("[씬 맵] 맵 루트와 출발 위치를 Inspector에서 연결하세요.");
            var platforms = _mapRoot.GetComponentsInChildren<PausePlatformObject>().Select(p => p.ReadSurface()).OrderBy(p => p.Id).ToArray();
            if (platforms.Length == 0 || platforms.Select(p => p.Id).Distinct().Count() != platforms.Length)
                throw new System.InvalidOperationException("[맵] 발판이 없거나 번호가 중복되었습니다. Inspector의 식별자를 확인하세요.");
            var walls = _mapRoot.GetComponentsInChildren<PauseWallObject>().Select(w => w.ReadWall()).ToArray();
            var gimmicks = _mapRoot.GetComponentsInChildren<PauseGimmickObject>().Select(g => g.ReadGimmick()).OrderBy(g => g.Id).ToArray();
            if (gimmicks.Select(g => g.Id).Distinct().Count() != gimmicks.Length)
                throw new System.InvalidOperationException("[씬 맵] 장치 번호가 중복되었습니다. Inspector의 식별자를 확인하세요.");
            foreach (ClimbGimmick portal in gimmicks.Where(g => g.Kind == GimmickKind.Portal))
                if (!gimmicks.Any(g => g.Id == portal.TargetId && g.Kind == GimmickKind.Portal && g.TargetId == portal.Id))
                    throw new System.InvalidOperationException("[씬 맵] 포탈 " + portal.Id + "번의 짝 연결이 올바르지 않습니다.");
            Debug.Log("[씬 맵] 발판 " + platforms.Length + "개, 반사 벽 " + walls.Length + "개, 장치 " + gimmicks.Length + "개를 읽었습니다.");
            return PauseLevel.FromScene(platforms, walls, _spawn.position, gimmicks, _finalStartPlatformId, _finalStepIds, _restoreDelay);
        }
        #endregion
    }
}
