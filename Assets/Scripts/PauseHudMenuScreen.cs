using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 직렬화 HUD 설정을 읽어 엔딩과 설정 화면만 그립니다.
    /// PauseHud가 게임과 공통 그림 도구를 전달하며 자체 입력·물리 갱신은 하지 않습니다.
    /// </summary>
    internal sealed class PauseHudMenuScreen
    {
        #region Reference
        private readonly PauseHudContext _ui;
        #endregion

        #region Custom Method
        // 공통 설정과 그림 도구를 한 번 연결합니다.
        internal PauseHudMenuScreen(PauseHudContext ui) { _ui = ui; }

        // DrawEnding 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawEnding()
        {
            if (_ui.Game.IsEndingJump) { if (!_ui.Game.EndingImpactTriggered) _ui.DrawSummitWord(); return; }
            float age = _ui.Game.EndingElapsed; Vector2 origin = _ui.WorldToCanvas(_ui.Game.WordPosition);
            if (age < _ui.Effects.FragmentSeconds)
            {
                for (int i = 0; i < _ui.Effects.FragmentCount; i++)
                {
                    float angle = i * _ui.Effects.FragmentAngleStep, speed = _ui.Effects.FragmentMinSpeed + i % _ui.Effects.FragmentSpeedVariants * _ui.Effects.FragmentSpeedStep;
                    Vector2 p = origin + new Vector2(Mathf.Cos(angle) * speed * age, -Mathf.Sin(angle) * speed * age + _ui.Effects.FragmentGravity * age * age);
                    _ui.Fill(_ui.FragmentAt(p, i), _ui.WithAlpha(_ui.Theme.Fragment, 1f - age / _ui.Effects.FragmentSeconds));
                }
            }
            _ui.Fill(PauseCanvasLayout.FullCanvas, _ui.WithAlpha(_ui.Theme.EndingVeil, Mathf.Clamp01((age - _ui.Effects.EndingVeilDelay) / _ui.Effects.EndingVeilSeconds) * _ui.Effects.EndingVeilOpacity));
            float y = Mathf.Max(_ui.Effects.CreditTop, _ui.Effects.CreditStart - age * _ui.Effects.CreditSpeed);
            _ui.Text(_ui.OffsetY(_ui.Layout.EndingHeading, y), "일시정지의 저주를 깨다", _ui.Fonts.EndingTitle, _ui.Theme.Gold, TextAnchor.MiddleCenter);
            _ui.Text(_ui.OffsetY(_ui.Layout.EndingBody, y), "시간을 멈추고, 방향을 설계하고,\n스스로의 도약으로 정상에 닿았습니다.", _ui.Fonts.Subtitle, _ui.Theme.Paper, TextAnchor.MiddleCenter);
            _ui.Text(_ui.OffsetY(_ui.Layout.EndingThanks, y), "기획  일시정지!\n캐릭터  배로 튀는 모험가\n플레이해 주셔서 감사합니다", _ui.Fonts.Credits, _ui.Theme.Mint, TextAnchor.MiddleCenter);
            if (age > _ui.Effects.RestartDelay && _ui.Button(_ui.Layout.EndingRestart, "처음부터 다시 도전")) _ui.Game.RestartGame();
        }

        // DrawSettings 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawSettings()
        {
            _ui.Fill(PauseCanvasLayout.FullCanvas, _ui.Theme.SettingsVeil); _ui.Panel(_ui.Layout.SettingsPanel);
            _ui.Text(_ui.Layout.SettingsHeading, "설정", _ui.Fonts.SettingsTitle, _ui.Theme.Paper, TextAnchor.MiddleCenter);
            _ui.Text(_ui.Layout.SettingsVolume, $"게임 음량   {_ui.Game.Volume * 100f:0}%", _ui.Fonts.SettingsBody, _ui.Theme.Paper);
            _ui.Game.Volume = GUI.HorizontalSlider(_ui.Layout.VolumeSlider, _ui.Game.Volume, 0f, 1f);
            if (_ui.Button(_ui.Layout.PauseKeyButton, "정지 키  " + _ui.KeyName(_ui.Game.PauseKey))) _ui.Game.BeginRebind(false);
            if (_ui.Button(_ui.Layout.JumpKeyButton, "점프 키  " + _ui.KeyName(_ui.Game.JumpKey))) _ui.Game.BeginRebind(true);
            if (_ui.Game.BindingTarget != 0) _ui.Text(_ui.Layout.BindingHint, "변경할 키를 누르세요", _ui.Fonts.Body, _ui.Theme.Gold, TextAnchor.MiddleCenter);
            if (_ui.Button(_ui.Layout.ContinueButton, "계속하기")) _ui.Game.CloseSettings();
            if (_ui.Button(_ui.Layout.RestartButton, "처음부터")) _ui.Game.RestartGame();
            if (_ui.Button(_ui.Layout.QuitButton, "게임 종료")) _ui.Game.QuitGame();
        }
        #endregion
    }
}
