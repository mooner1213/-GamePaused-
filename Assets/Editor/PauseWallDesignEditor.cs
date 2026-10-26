using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>성벽의 실제 반사면과 직접 칠할 디자인 레이어를 구분해 보여 줍니다. PauseWallObject를 선택하면 사용합니다.</summary>
[CustomEditor(typeof(PauseWallObject))]
public sealed class PauseWallDesignEditor : Editor
{
    #region Unity Event Method
    // Inspector는 연결과 편집 방법을 보여 주고 실제 선택 작업은 편집 도구에 맡깁니다.
    public override void OnInspectorGUI() { DrawWallInspector(); }
    #endregion
    #region Custom Method
    // 몸통·이음띠·상하 마감은 각 Tilemap에서 편집하며 반사 판정은 기존 연결을 유지합니다.
    private void DrawWallInspector()
    {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox("아래 버튼으로 몸통 Tilemap을 칠할 수 있습니다. 이음띠·상하 마감은 자식 Grid의 해당 Tilemap을 선택하세요. 디자인을 지워도 실제 반사면은 유지됩니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("이 성벽의 Tilemap 칠하기"))
            { Selection.activeGameObject = ((PauseWallObject)target).gameObject; PauseCourseTilemapEditor.PaintSelected(); }
    }
    #endregion
}
