using UnityEngine;
using UnityEngine.EventSystems;

namespace PauseClimb
{
    /// <summary>첫 화면과 세로형 음량 설정, 09번 진입을 제어합니다. 두 화면과 시작 장면 이름, 메뉴 음원을 연결합니다.</summary>
    public sealed class PauseMainMenu : MonoBehaviour
    {
        #region Variables
        [Header("시작할 게임 장면")]
        [SerializeField, Tooltip("게임 시작 버튼이 페이드 후 여는 빌드 목록의 씬 이름입니다.")] private string _gameScene = "Stage09_RGBKeyboard";
        #endregion
        #region Reference
        [Header("화면 연결")]
        [SerializeField, Tooltip("타이틀과 시작·설정·종료 버튼입니다.")] private GameObject _main;
        [SerializeField, Tooltip("기존 배경 위의 세로형 BGM·SFX 설정입니다.")] private GameObject _settings;
        [SerializeField, Tooltip("메인 화면의 음악과 효과음을 재생합니다.")] private PauseSoundPlayer _sound;
        #endregion
        #region Property
        public bool IsSettingsOpen => _settings.activeSelf;
        #endregion
        #region Unity Event Method
        private void Start() { InitializeMenu(); }
        private void Update() { ReadBackKey(); }
        #endregion
        #region Custom Method
        // 편집기에서 만든 두 화면과 실제 음원 연결을 저장합니다.
        public void Configure(GameObject main, GameObject settings, PauseSoundPlayer sound) { _main = main; _settings = settings; _sound = sound; }
        // 게임에서 돌아와도 듣기 정지가 남지 않게 새 메뉴 음악을 준비합니다.
        private void InitializeMenu() { AudioListener.pause = false; _sound.Initialize(1f); ShowMain(); Debug.Log("[메인 화면] 시작·설정·종료를 준비했습니다."); }
        // 배경을 유지하면서 세로형 설정으로 전환합니다.
        public void ShowSettings() { _main.SetActive(false); _settings.SetActive(true); Debug.Log("[메인 화면] 음악·효과음 설정을 열었습니다."); }
        // 설정을 저장한 뒤 원래 타이틀과 버튼을 표시합니다.
        public void ShowMain() { _settings.SetActive(false); _main.SetActive(true); PauseAudioPreferences.Save(); ClearSelection(); }
        // 설정한 게임 씬을 검은 전환막 뒤에서 로딩합니다.
        public void StartGame() { if (PauseSceneTransition.Instance != null) PauseSceneTransition.Instance.Load(_gameScene); }
        // 메인 설정에서만 ESC를 뒤로 가기로 사용합니다. 게임의 ESC 점프 예약과는 분리합니다.
        private void ReadBackKey() { if (_settings.activeSelf && Input.GetKeyDown(KeyCode.Escape)) ShowMain(); }
        // 실제 빌드는 종료하고 편집기에서는 Play만 종료합니다.
        public void QuitGame()
        {
            PauseAudioPreferences.Save(); Debug.Log("[메인 화면] 게임을 종료합니다.");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        // 키보드 입력이 직전 버튼에 남지 않게 합니다.
        private static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        #endregion
    }
}
