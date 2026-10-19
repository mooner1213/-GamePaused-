using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 직렬화 HUD 설정을 읽어 시작·등반·점프 계획 화면만 그립니다.
    /// PauseHud가 게임과 공통 그림 도구를 전달하며 자체 입력·물리 갱신은 하지 않습니다.
    /// </summary>
    internal sealed class PauseHudGameScreen
    {
        #region Reference
        private readonly PauseHudContext _ui;
        #endregion

        #region Custom Method
        // 공통 설정과 그림 도구를 한 번 연결합니다.
        internal PauseHudGameScreen(PauseHudContext ui) { _ui = ui; }

        // DrawTitle 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawTitle()
        {
            _ui.Fill(PauseCanvasLayout.FullCanvas, _ui.Theme.TitleVeil);
            _ui.Panel(_ui.Layout.TitlePanel);
            _ui.Text(_ui.Layout.TitleHeading, "일시정지!", _ui.Fonts.Title, _ui.Theme.Paper, TextAnchor.MiddleCenter);
            _ui.Text(_ui.Layout.TitleSubtitle, "시간을 멈추고, 배와 기믹으로 성을 오르다", _ui.Fonts.Subtitle, _ui.Theme.Mint, TextAnchor.MiddleCenter);
            Rule(_ui.Effects.RuleRows.x, "01", "정지", _ui.Game.UseTouchControls ? "왼쪽 정지 버튼으로 시간을 멈추세요." : "ESC 한 번으로 시간을 멈추세요.");
            Rule(_ui.Effects.RuleRows.y, "02", "설계", _ui.Game.UseTouchControls ? "오른쪽 점프 버튼을 누른 시간과 횟수로 예약하세요." : "SPACE를 누른 시간과 횟수로 점프를 예약하세요.");
            Rule(_ui.Effects.RuleRows.z, "03", "재생", _ui.Game.UseTouchControls ? "왼쪽 재생 버튼을 누르면 예약 점프가 이어집니다." : "ESC를 다시 누르면 착지마다 다음 점프가 이어집니다.");
            _ui.Text(_ui.Layout.TitleFootnote, "점프 전용 · 벽 반사와 로프의 흔들림으로 방향을 설계하세요", _ui.Fonts.Body, _ui.Theme.Gold, TextAnchor.MiddleCenter);
            if (_ui.Button(_ui.Layout.StartButton, "등반 시작")) _ui.Game.BeginGame();
            _ui.Text(_ui.Layout.TitleBelowNote, "초반에는 궤도를 보며 익히세요. 떨어지면 궤도가 다시 돌아옵니다.", _ui.Fonts.Hint, _ui.Theme.Paper, TextAnchor.MiddleCenter);
        }

        // DrawClimbHud 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawClimbHud()
        {
            if (_ui.Game.DriverEnabled) _ui.Text(_ui.Layout.DriverNotice, "자동 입력 시연", _ui.Fonts.Hint, _ui.Theme.Gold);
            if (!_ui.Game.IsPaused)
            {
                _ui.Panel(_ui.Layout.HeightPanel);
                _ui.Text(_ui.Layout.HeightLabel, $"{_ui.Game.Height:0.0} / {_ui.Game.Level.GoalHeight:0.#} m", _ui.Fonts.Height, _ui.Theme.Paper);
                _ui.Text(_ui.Layout.FacingLabel, (_ui.Game.Facing > 0 ? "오른쪽 →" : "← 왼쪽") + $"   최고 {_ui.Game.BestHeight:0.0}m", _ui.Fonts.Caption, _ui.Theme.Mint);
                if (_ui.Game.IsPlayingBack) _ui.Text(_ui.Layout.PlaybackNotice, $"연속 도약 · {_ui.Game.PlaybackRemaining}번 남음", _ui.Fonts.Playback, _ui.Theme.Mint, TextAnchor.MiddleCenter);
            }
            if (_ui.Button(_ui.Layout.SettingsButton, "설정")) _ui.Game.OpenSettings();
            if (_ui.Game.IsPaused)
            {
                if (_ui.Game.SummitReached) _ui.DrawSummitWord();
                else
                {
                    _ui.Text(_ui.Layout.PauseHeading, "일시정지", _ui.Fonts.PauseTitle, _ui.Theme.Paper, TextAnchor.MiddleCenter);
                    _ui.Text(_ui.Layout.PauseDetail, _ui.Game.IsInputLocked ? "입력 잠금 · 남은 점프 그대로 보존" : "누른 시간 = 점프 세기", _ui.Fonts.Body, _ui.Game.IsInputLocked ? _ui.Theme.Gold : _ui.Theme.Mint, TextAnchor.MiddleCenter);
                }
            }
            if (!_ui.Game.IsEndingJump) DrawPlanStrip();
            if (_ui.Game.UseTouchControls && !_ui.Game.IsEndingJump)
            {
                _ui.Panel(_ui.Game.TouchInput.ToggleArea); _ui.Text(_ui.Game.TouchInput.ToggleArea, _ui.Game.IsPaused ? "재생" : "정지", _ui.Fonts.TouchToggle, _ui.Theme.Paper, TextAnchor.MiddleCenter);
                _ui.Panel(_ui.Game.TouchInput.JumpArea); _ui.Text(_ui.Game.TouchInput.JumpArea, _ui.Game.IsInputLocked ? "입력 잠김" : "점프\n누르고 떼기", _ui.Fonts.TouchJump, _ui.Game.IsInputLocked ? _ui.Theme.Disabled : _ui.Theme.Mint, TextAnchor.MiddleCenter);
            }
            if (_ui.Game.CrumbleRemaining >= 0f && _ui.Game.CrumbleRemaining <= _ui.Effects.CrumbleHintSeconds)
                _ui.Text(_ui.Layout.CrumbleNotice, $"붕괴 {_ui.Game.CrumbleRemaining:0.0}초 · 정지 중에도 게이지가 줄어듭니다", _ui.Fonts.Body, _ui.Theme.Gold, TextAnchor.MiddleCenter);
        }

        // DrawPlanStrip 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawPlanStrip()
        {
            _ui.Panel(_ui.Layout.PlanPanel);
            if (_ui.Game.IsRecording && !_ui.Game.SummitReached)
            {
                _ui.Text(_ui.Layout.PlanCount, $"점프 {_ui.Game.RecordedCount}번 예약   {_ui.Game.HeldSeconds:0.00}초", _ui.Fonts.Body, _ui.Theme.Paper);
                for (int i = 0; i < _ui.Game.Recording.Count; i++)
                {
                    _ui.DrawPlanBar(i, _ui.Game.Recording[i].HoldSeconds / _ui.Game.MaximumCharge);
                }
                _ui.Fill(_ui.Layout.ChargeTrack, _ui.Theme.ChargeTrack);
                _ui.Fill(_ui.WidthFraction(_ui.Layout.ChargeTrack, _ui.Game.Charge01), _ui.Theme.Gold);
                _ui.Text(_ui.Layout.PlanInputHint, _ui.Game.UseTouchControls ? "점프 누르고 떼기 · 재생 버튼으로 실행" : $"{_ui.KeyName(_ui.Game.JumpKey)} 누르고 떼기 · {_ui.KeyName(_ui.Game.PauseKey)} 재생", _ui.Fonts.Hint, _ui.Theme.Paper, TextAnchor.MiddleRight);
            }
            else
            {
                string message = _ui.Game.IsInputLocked ? "추가 입력 잠금 · 정지와 재개만 가능합니다" : _ui.Game.SummitReached ? "정지 후 점프하여 머리 위 글자를 깨세요" : GetContextHint();
                _ui.Text(_ui.Layout.ContextHint, message, _ui.Fonts.Context, _ui.Game.IsInputLocked ? _ui.Theme.Gold : _ui.Theme.Paper, TextAnchor.MiddleCenter);
                _ui.Text(_ui.Layout.HotkeyHint, _ui.Game.UseTouchControls ? "왼쪽 정지·재생   /   오른쪽 점프 버튼 유지로 세기 입력" : $"{_ui.KeyName(_ui.Game.PauseKey)} 정지·재생   /   {_ui.KeyName(_ui.Game.JumpKey)} 정지 중 점프 입력", _ui.Fonts.Hint, _ui.Theme.Mint, TextAnchor.MiddleCenter);
            }
        }


        // 현재 설정을 읽어 규칙 한 행을 배치합니다.
        private void Rule(float y, string number, string title, string description)
        {
            _ui.Text(_ui.AtY(_ui.Layout.RuleNumber, y), number, _ui.Fonts.StepNumber, _ui.Theme.Mint);
            _ui.Text(_ui.AtY(_ui.Layout.RuleTitle, y), title, _ui.Fonts.Subtitle, _ui.Theme.Paper);
            _ui.Text(_ui.AtY(_ui.Layout.RuleBody, y), description, _ui.Fonts.Body, _ui.Theme.Paper);
        }

        // 현재 설정을 읽어 필요한 구간 안내를 반환합니다.
        private string GetContextHint()
        {
            if (_ui.Game.Simulation.Recovering) return "엎드려 회복 중 · 남은 점프는 일어난 뒤 이어집니다";
            if (_ui.Game.Simulation.BellyBouncing) return "배 반동 · 공중 앞구르기 중에는 다음 예약이 보존됩니다";
            if (_ui.Game.Simulation.OnRope) return "로프가 가는 방향으로 점프 · 정지해서 다음 입력을 준비하세요";
            if (_ui.Game.Simulation.OnLadder) return "사다리 자동 등반 중 · 꼭대기에서 다음 점프가 이어집니다";
            if (_ui.Game.CameraBatchLocked) return "전체 입력이 끝날 때까지 화면 고정 · 완료 후 캐릭터에게 이동";
            if (_ui.Game.Simulation.Grounded && _ui.Game.Simulation.Support.Id == _ui.Effects.CloudHintPlatformId) return "구름 진입 전 3번을 예약하세요 · 구름 위에서는 새 입력 불가";
            if (_ui.Game.Simulation.Grounded && _ui.Game.Simulation.Support.Id == _ui.Game.Level.FinalStartPlatformId) return "마지막 구간 · 네 번의 점프를 한 계획으로 예약하세요";
            if (_ui.Game.Height < _ui.Effects.FirstHintHeight) return "걷기 없이 점프만 · 초반 궤도로 거리와 세기를 익히세요";
            if (_ui.Game.Height < _ui.Effects.WallHintHeight) return "반대쪽으로 가려면 벽에 박치기하세요";
            if (_ui.Game.Height < _ui.Effects.IceHintHeight) return "얼음은 착지 후에도 미끄러집니다 · 궤도가 서서히 사라집니다";
            if (_ui.Game.Height < _ui.Effects.CrumbleHintHeight) return "붕괴 발판은 정지해도 3초 · 구름은 연속 점프로 통과하세요";
            return "남은 힘은 벽에서 반사됩니다 · 떨어져도 다시 계획하세요";
        }
        #endregion
    }
}
