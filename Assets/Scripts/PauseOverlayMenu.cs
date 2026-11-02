using UnityEngine;
using UnityEngine.EventSystems;

namespace PauseClimb
{
    /// <summary>점프 계획과 별개인 검은 관리 메뉴입니다. 게임과 네 화면을 연결하며, 이어하기는 원래 재생·예약 상태를 보존합니다.</summary>
    public sealed class PauseOverlayMenu : MonoBehaviour
    {
        #region Variables
        private bool _isOpen, _wasHudEnabled;
        [Header("메인 복귀")]
        [SerializeField, Tooltip("문 아이콘을 누르면 페이드로 돌아갈 메인 씬 이름입니다.")] private string _mainScene = "MainMenu";
        #endregion
        #region Reference
        [Header("게임 연결")]
        [SerializeField, Tooltip("물리와 예약을 보존한 채 관리 정지를 적용할 실제 게임입니다.")] private PauseGame _game;
        [Header("관리 화면")]
        [SerializeField, Tooltip("우측 상단 일시정지 버튼입니다.")] private GameObject _pauseButton;
        [SerializeField, Tooltip("이어하기·재시작·메인 복귀·설정이 있는 검은 화면입니다.")] private GameObject _pause;
        [SerializeField, Tooltip("BGM은 왼쪽, SFX는 오른쪽에 배치한 설정입니다.")] private GameObject _settings;
        [SerializeField, Tooltip("예·아니오로 응답하는 재시작 확인창입니다.")] private GameObject _confirm;
        private PauseHud _hud;
        private PauseSoundPlayer _sound;
        #endregion
        #region Property
        public bool IsOpen => _isOpen;
        public bool IsConfirming => _confirm.activeSelf;
        public bool IsSettingsOpen => _settings.activeSelf;
        #endregion
        #region Unity Event Method
        private void Start() { InitializeMenu(); }
        #endregion
        #region Custom Method
        // 저장된 관리 화면과 해당 게임을 연결합니다.
        public void Configure(PauseGame game, GameObject pauseButton, GameObject pause, GameObject settings, GameObject confirm)
        { _game = game; _pauseButton = pauseButton; _pause = pause; _settings = settings; _confirm = confirm; }
        // 게임 초기화 후에만 음원·HUD를 캐싱하고 새 메인 화면에서 진입한 게임을 바로 시작합니다.
        private void InitializeMenu()
        {
            _hud = _game.GetComponent<PauseHud>(); _sound = _game.GetComponent<PauseSoundPlayer>();
            _pause.SetActive(false); _settings.SetActive(false); _confirm.SetActive(false);
            if (!_game.HasStarted) _game.BeginGame();
            Debug.Log("[관리 메뉴] ESC 예약과 분리된 일시정지 버튼을 준비했습니다.");
        }
        // 설정 정지는 현재 명령·위치·기름 관성까지 그대로 유지합니다.
        public void Open()
        {
            if (_isOpen || _game.IsEndingJump) return;
            _isOpen = true; _wasHudEnabled = _hud.enabled; _hud.enabled = false;
            _game.OpenSettings(); _sound.SetMenuPreview(true);
            _pauseButton.SetActive(false); _pause.SetActive(true); ClearSelection();
            Debug.Log("[관리 메뉴] 정지했습니다. 남은 점프와 타이머를 보존합니다.");
        }
        // 재생 중에 열었다면 이어 재생하고, ESC 계획 중이었다면 같은 계획 화면으로 돌아갑니다.
        public void Resume()
        {
            _pause.SetActive(false); _settings.SetActive(false); _confirm.SetActive(false); _pauseButton.SetActive(true);
            _game.CloseSettings(); _sound.SetMenuPreview(false); _hud.enabled = _wasHudEnabled;
            _isOpen = false; PauseAudioPreferences.Save(); ClearSelection();
            Debug.Log("[관리 메뉴] 원래 상태로 이어갑니다.");
        }
        // 게임을 계속 정지한 채 좌우형 음량 설정만 표시합니다.
        public void ShowSettings() { _pause.SetActive(false); _settings.SetActive(true); Debug.Log("[관리 메뉴] 음악·효과음 설정을 열었습니다."); }
        // 음량을 저장하고 재생을 시작하지 않은 채 관리 화면으로 돌아갑니다.
        public void BackToPause() { _settings.SetActive(false); _pause.SetActive(true); PauseAudioPreferences.Save(); Debug.Log("[관리 메뉴] 음량 설정을 저장하고 정지 화면으로 돌아왔습니다."); }
        // 확인을 받기 전까지 진행 상태를 바꾸지 않습니다.
        public void AskRestart() { _confirm.SetActive(true); Debug.Log("[관리 메뉴] 재시작 확인을 요청했습니다."); }
        // 아니오는 확인창만 닫습니다.
        public void CancelRestart() { _confirm.SetActive(false); ClearSelection(); Debug.Log("[관리 메뉴] 재시작을 취소했습니다."); }
        // 예를 누른 경우에만 맵·예약·캐릭터를 처음 상태로 되돌립니다.
        public void ConfirmRestart() { Resume(); _game.RestartGame(); Debug.Log("[관리 메뉴] 새 게임을 시작했습니다."); }
        // 게임은 정지한 상태로 유지하고 메인 씬을 페이드로 엽니다.
        public void ReturnToMain() { if (PauseSceneTransition.Instance != null) PauseSceneTransition.Instance.Load(_mainScene); }
        // 이어하기 직후 Space가 UI 버튼을 다시 누르지 않게 선택을 해제합니다.
        private static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        #endregion
    }
}
