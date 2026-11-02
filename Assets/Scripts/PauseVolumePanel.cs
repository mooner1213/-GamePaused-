using UnityEngine;
using UnityEngine.UI;

namespace PauseClimb
{
    /// <summary>두 슬라이더와 백분율을 실제 BGM·SFX 설정에 연결합니다. 음악/효과음 Slider와 Text를 각각 연결합니다.</summary>
    public sealed class PauseVolumePanel : MonoBehaviour
    {
        #region Reference
        [Header("배경음악")]
        [SerializeField, Tooltip("배경음악 음량 0~1을 조절하는 슬라이더입니다.")] private Slider _music;
        [SerializeField, Tooltip("배경음악의 현재 백분율 표시입니다.")] private Text _musicValue;
        [Header("효과음")]
        [SerializeField, Tooltip("효과음 음량 0~1을 조절하는 슬라이더입니다.")] private Slider _effects;
        [SerializeField, Tooltip("효과음의 현재 백분율 표시입니다.")] private Text _effectsValue;
        #endregion
        #region Unity Event Method
        private void Awake() { Bind(); }
        private void OnEnable() { Refresh(); }
        private void OnDisable() { PauseAudioPreferences.Save(); }
        private void OnDestroy() { Unbind(); }
        #endregion
        #region Custom Method
        // 편집 도구가 생성한 실제 슬라이더와 글자를 저장합니다.
        public void Configure(Slider music, Text musicValue, Slider effects, Text effectsValue)
        { _music = music; _musicValue = musicValue; _effects = effects; _effectsValue = effectsValue; }
        // 컴포넌트 수명 동안 한 번만 변경 사건을 연결합니다.
        private void Bind() { _music.onValueChanged.AddListener(SetMusic); _effects.onValueChanged.AddListener(SetEffects); }
        // 닫힌 씬의 슬라이더에 사건 참조가 남지 않게 합니다.
        private void Unbind() { if (_music != null) _music.onValueChanged.RemoveListener(SetMusic); if (_effects != null) _effects.onValueChanged.RemoveListener(SetEffects); }
        // 메뉴를 다시 열면 다른 장면에서 바꾼 설정도 즉시 표시합니다.
        private void Refresh()
        {
            if (_music == null || _effects == null) return;
            _music.SetValueWithoutNotify(PauseAudioPreferences.Music); _effects.SetValueWithoutNotify(PauseAudioPreferences.Effects); DrawValues();
        }
        // 배경음악 슬라이더 값을 실제 음원에 전달합니다.
        public void SetMusic(float value) { PauseAudioPreferences.SetMusic(value); DrawValues(); }
        // 효과음 슬라이더 값을 실제 음원에 전달합니다.
        public void SetEffects(float value) { PauseAudioPreferences.SetEffects(value); DrawValues(); }
        // 두 설정의 현재 백분율을 화면에 표시합니다.
        private void DrawValues()
        { _musicValue.text = Mathf.RoundToInt(PauseAudioPreferences.Music * 100f) + "%"; _effectsValue.text = Mathf.RoundToInt(PauseAudioPreferences.Effects * 100f) + "%"; }
        #endregion
    }
}
