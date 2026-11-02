using UnityEngine;
using UnityEngine.EventSystems;

namespace PauseClimb
{
    /// <summary>효과음 슬라이더를 놓았을 때 조정한 음량으로 짧은 확인음을 냅니다. 실제 음원 컴포넌트를 한 번 연결합니다.</summary>
    public sealed class PauseSliderPreview : MonoBehaviour, IPointerUpHandler
    {
        #region Reference
        [Header("효과음 미리 듣기")]
        [SerializeField, Tooltip("설정한 SFX 크기로 확인음을 재생할 음원입니다.")] private PauseSoundPlayer _sound;
        #endregion
        #region Unity Event Method
        public void OnPointerUp(PointerEventData eventData) { Preview(); }
        #endregion
        #region Custom Method
        // 효과음 슬라이더를 담당하는 실제 음원을 연결합니다.
        public void Configure(PauseSoundPlayer sound) { _sound = sound; }
        // 손잡이를 놓을 때 한 번만 확인음을 재생합니다.
        private void Preview() { if (_sound != null) _sound.Play(PauseSoundCue.Land); }
        #endregion
    }
}
