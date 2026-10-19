using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 직렬화 HUD 설정을 읽어 예상 궤도와 착지 순서 화면만 그립니다.
    /// PauseHud가 게임과 공통 그림 도구를 전달하며 자체 입력·물리 갱신은 하지 않습니다.
    /// </summary>
    internal sealed class PauseHudGuideScreen
    {
        #region Reference
        private readonly PauseHudContext _ui;
        #endregion

        #region Custom Method
        // 공통 설정과 그림 도구를 한 번 연결합니다.
        internal PauseHudGuideScreen(PauseHudContext ui) { _ui = ui; }

        // DrawTrajectory 화면을 현재 게임 상태와 Inspector 배치 설정으로 그립니다.
        internal void DrawTrajectory()
        {
            PauseTrajectory guide = _ui.Game.Trajectory;
            if (!_ui.Game.IsPaused || !guide.Visible) return;
            Color pathColor = _ui.WithAlpha(_ui.Theme.GuidePath, guide.Opacity);
            for (int i = 0; i < guide.Points.Count; i++)
            {
                Vector2 point = _ui.WorldToCanvas(guide.Points[i] + _ui.Effects.GuideWorldOffset);
                if (point.y > _ui.Effects.GuideVisibleY.x && point.y < _ui.Effects.GuideVisibleY.y) _ui.Fill(_ui.MarkerAt(point), pathColor);
            }
            foreach (Vector2 bounce in guide.Bounces)
            {
                Vector2 point = _ui.WorldToCanvas(bounce + _ui.Effects.GuideWorldOffset);
                if (point.y < _ui.Effects.GuideVisibleY.x || point.y > _ui.Effects.GuideVisibleY.y) continue;
                _ui.Text(_ui.Offset(_ui.Layout.GuideBounce, point), "×", _ui.Fonts.BounceMarker, _ui.WithAlpha(_ui.Theme.Gold, guide.Opacity), TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < guide.Landings.Count; i++)
            {
                Vector2 point = _ui.WorldToCanvas(guide.Landings[i]);
                if (point.y < _ui.Effects.LandingVisibleY.x || point.y > _ui.Effects.LandingVisibleY.y) continue;
                _ui.Fill(_ui.Offset(_ui.Layout.GuideLandingBackground, point), _ui.WithAlpha(_ui.Theme.GuideLanding, guide.Opacity));
                _ui.Text(_ui.Offset(_ui.Layout.GuideLandingLabel, point), (i + 1).ToString(), _ui.Fonts.Hint, pathColor, TextAnchor.MiddleCenter);
            }
        }
        #endregion
    }
}
