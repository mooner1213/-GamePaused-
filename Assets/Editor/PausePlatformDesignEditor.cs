using System.Collections.Generic;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;

/// <summary>
/// 기존 발판도 팔레트 타일로 디자인을 바꿀 수 있는 한국어 Inspector입니다.
/// 원래 표면과 이동 설정은 보존하며 선택한 발판 하나만 수정합니다.
/// </summary>
[CustomEditor(typeof(PausePlatformObject))]
public sealed class PausePlatformDesignEditor : Editor
{
    #region Unity Event Method
    // 조절값을 보여 주고 팔레트 선택을 기존 발판에 적용하는 기능을 호출합니다.
    public override void OnInspectorGUI() { DrawInspector(); }
    #endregion
    #region Custom Method
    // 타일 자산을 끌어 넣거나 현재 팔레트에서 고른 타일을 버튼으로 적용할 수 있게 합니다.
    private void DrawInspector()
    {
        var source = (PausePlatformObject)target;
        PauseDesignTile previous = source.DesignTile;
        base.OnInspectorGUI();
        if (!Application.isPlaying && previous != source.DesignTile && source.DesignTile != null)
            ApplyTile(source, source.DesignTile);
        EditorGUILayout.HelpBox("기존 그림을 한 칸씩 수정: '이 발판의 Tilemap 칠하기'를 누르세요. 위치·충돌은 기존 표면을 유지합니다.\n빈 공간에 실제 발판 추가: 공통 Grid의 '03 발판 추가'에 칠하세요.\n발판 전체 디자인 교체: 팔레트의 발판 하나를 고른 뒤 디자인 적용 버튼을 누르세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("이 발판의 Tilemap 칠하기"))
            { Selection.activeGameObject = source.gameObject; PauseCourseTilemapEditor.PaintSelected(); }
            if (GUILayout.Button("팔레트에서 고른 발판 디자인 적용")) ApplyBrush(source);
            if (GUILayout.Button("연결된 디자인 다시 적용") && source.DesignTile != null) ApplyTile(source, source.DesignTile);
        }
    }

    // 기본 브러시가 현재 들고 있는 발판 타일을 한 번 읽습니다.
    private static void ApplyBrush(PausePlatformObject source)
    {
        var brush = GridPaintingState.gridBrush as GridBrush;
        PauseDesignTile tile = brush == null ? null : brush.cells.Select(cell => cell.tile).OfType<PauseDesignTile>().FirstOrDefault(item => item.IsPlatform);
        if (tile == null) { Debug.LogWarning("[발판 디자인] Tile Palette에서 발판 타일 하나를 먼저 고르세요.", source); return; }
        ApplyTile(source, tile);
    }

    // 사용자가 선택한 한 발판의 원본 표면·좌표를 보존하고 새 타일의 재질만 연결합니다.
    public static void ApplyTile(PausePlatformObject source, PauseDesignTile tile)
    {
        if (source == null || tile == null || !tile.IsPlatform)
        { Debug.LogWarning("[발판 디자인] 일반·기름·금간·타이머 발판 타일을 선택하세요.", source); return; }
        Undo.RecordObject(source, "발판 디자인과 재질 교체");
        Transform editable = source.transform.Find(PauseCourseTilemapEditor.GRID_NAME);
        bool hadTilemap = editable != null;
        if (hadTilemap) Undo.DestroyObjectImmediate(editable.gameObject);
        Transform old = source.transform.Find(PauseStageThemeAssets.SKIN_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var properties = new SerializedObject(source);
        PlatformKind previousKind = source.ReadSurface().Kind;
        properties.FindProperty("_kind").enumValueIndex = (int)(previousKind == PlatformKind.Goal || previousKind == PlatformKind.SummitStep ? previousKind : tile.PlatformKind);
        properties.FindProperty("_designTile").objectReferenceValue = tile;
        properties.ApplyModifiedProperties();
        var kit = PauseRgbAssets.LoadNewKit();
        foreach (string role in new[] { "Normal", "Oil", "Cracked", "Timer", "Moving", "Goal" }) kit[role] = tile.SourceSprite;
        PauseRgbStageArt.ApplyOne(source, kit);
        if (hadTilemap) PauseCourseTilemapEditor.ConvertPlatform(source);
        Undo.RegisterCreatedObjectUndo(source.transform.Find(PauseStageThemeAssets.SKIN_NAME).gameObject, "발판 새 도트");
        EditorUtility.SetDirty(source);
        EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
        Debug.Log("[발판 디자인] " + source.Id + "번에 '" + tile.name + "' 적용 · 위치·크기·경사는 유지했습니다.", source);
    }
    #endregion
}
