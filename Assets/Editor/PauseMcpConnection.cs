// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

// 이 프로젝트를 열면 공식 Unity MCP를 로컬 서버에 연결합니다. 게임 실행이나 맵 데이터는 변경하지 않습니다.
/// <summary>
/// 이 프로젝트를 열면 공식 Unity MCP를 로컬 서버에 연결합니다. 게임 실행이나 맵 데이터는 변경하지 않습니다.
/// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
/// </summary>
public static class PauseMcpConnection
{
    #region Variables
    private const string SESSION_KEY = "PauseClimb.McpConnected";
    private static bool _isConnecting;
    #endregion

    #region Unity Event Method
    // [NOTE] 로컬 서버가 준비된 뒤 도메인이 다시 로드되면 실패했던 연결을 다시 시도합니다.
    // 패키지 가져오기와 컴파일이 끝난 뒤 연결 기능을 한 번 호출합니다.
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (Application.isBatchMode || SessionState.GetBool(SESSION_KEY, false)) return;
        EditorApplication.delayCall += Connect;
    }
    #endregion

    #region Custom Method
    // 공식 패키지의 연결 API를 사용하며 컴퓨터 내부 주소에만 연결합니다.
    [MenuItem("일시정지 게임/Unity MCP 연결")]
    public static async void Connect()
    {
        if (_isConnecting) return;
        _isConnecting = true;
        try
        {
            EditorConfigurationCache.Instance.SetUseHttpTransport(true);
            EditorConfigurationCache.Instance.SetHttpBaseUrl("http://127.0.0.1:8080");
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            bool isConnected = MCPServiceLocator.Bridge.IsRunning || await MCPServiceLocator.Bridge.StartAsync();
            SessionState.SetBool(SESSION_KEY, isConnected);
            if (isConnected) Debug.Log("[Unity MCP] 로컬 연결 완료 · 플레이하지 않은 씬과 오브젝트를 조작할 준비가 됐습니다.");
            else Debug.LogWarning("[Unity MCP] 서버 연결을 확인하세요. Window > MCP for Unity에서 연결 상태를 볼 수 있습니다.");
        }
        catch (Exception error)
        {
            Debug.LogError("[Unity MCP] 연결 실패: " + error.Message);
        }
        finally { _isConnecting = false; }
    }
    #endregion
}
