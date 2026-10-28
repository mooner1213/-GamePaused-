// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    public enum ClimbEvent { Jump, Land, WallBounce, LongFall, BellyBounce, BellyRest, Recovered, Portal, RopeGrab, RopeRelease, LadderGrab, LadderExit, Trampoline, TrampolineRest, AutoFace }

    /// <summary>
    /// 실제 이동과 예상 궤도가 공유하는 고정 시간 물리와 장치 상태를 계산합니다.
    /// 생성할 때 씬의 PauseLevel과 PauseGame에서 조절한 MovementTuning을 전달합니다.
    /// </summary>
    public sealed partial class PauseSimulation
    {
        #region Variables
        public const float STEP = 1f / 60f;
        // [NOTE] 기존 검증 fixture의 기본 크기 호환값이며 실제 판정은 아래의 인스턴스 조절값을 사용합니다.
        public const float HALF_WIDTH = 0.22f;
        public const float BODY_HEIGHT = 0.85f;
        // [NOTE] 아래 값은 접촉 경계와 0에 가까운 분모의 수치 안정성을 위한 허용치이며 게임 밸런스 값이 아닙니다.
        private const float GROUND_ATTACH_TOLERANCE = 0.03f;
        private const float LANDING_CROSS_TOLERANCE = 0.025f;
        private const float SURFACE_PRIORITY_TOLERANCE = 0.03f;
        private const float WALL_CROSS_TOLERANCE = 0.001f;
        private const float LADDER_ARRIVAL_TOLERANCE = 0.001f;
        private const float DIRECTION_SQUARED_EPSILON = 0.001f;
        private const float ROPE_LINE_SQUARED_EPSILON = 0.01f;
        private const float ROPE_DIRECTION_SPEED_EPSILON = 0.01f;
        private const float SWEPT_AXIS_EPSILON = 0.000001f;
        private const int NO_TRAMPOLINE = -1;
        private Vector2 _position, _velocity;
        private int _facing = 1, _commandIndex, _bellyBounceCount;
        private int _supportTrampolineId = NO_TRAMPOLINE;
        private float _supportX, _peakHeight, _motionTime, _contactRemaining, _recoveryRemaining, _reboundHeight, _bellyFlightElapsed, _bellyFlightDuration;
        private float _portalCooldownRemaining, _attachmentCooldown, _ropeElapsed, _ropePhase, _ropeAmplitude, _ropeFrequency, _effectiveRopeLength, _ropeAngle;
        private bool _isActiveJump, _isBellyBouncing, _isLastWallBounceDescending;
        private bool _hasLongFall;
        private readonly List<JumpCommand> _commands = new List<JumpCommand>();
        private readonly MovementTuning _tuning;
        #endregion
        #region Reference
        private readonly PauseLevel _level;
        private ClimbPlatform _support;
        private ClimbGimmick _attachedGimmick;
        #endregion
        #region Property
        public MovementTuning Tuning => _tuning;
        public float CollisionHalfWidth => _tuning.CollisionHalfWidth;
        public float CollisionHeight => _tuning.CollisionHeight;
        public Vector2 Position => _position;
        public Vector2 Velocity => _velocity;
        public ClimbPlatform Support => _support;
        public bool Grounded => _support != null && !_support.Broken && !_isBellyBouncing;
        public int Facing => _facing;
        public bool Busy => _commandIndex < _commands.Count || _isActiveJump || _isBellyBouncing || Recovering || OnLadder || _hasLongFall;
        public int Remaining => _hasLongFall ? 0 : _commands.Count - _commandIndex + (_isActiveJump ? 1 : 0);
        public int LaunchedCount => _commandIndex;
        public IReadOnlyList<JumpCommand> Commands => _commands;
        public bool BellyBouncing => _isBellyBouncing;
        public bool BellyContact => _isBellyBouncing && _contactRemaining > 0f;
        public float BellyFlightProgress => _bellyFlightDuration > 0f ? Mathf.Clamp01(_bellyFlightElapsed / _bellyFlightDuration) : 0f;
        public bool Recovering => _recoveryRemaining > 0f;
        public bool HasLongFall => _hasLongFall;
        public bool OnTrampoline => Grounded && _supportTrampolineId != NO_TRAMPOLINE;
        public bool OnRope => _attachedGimmick != null && _attachedGimmick.Kind == GimmickKind.Rope;
        public bool OnLadder => _attachedGimmick != null && _attachedGimmick.Kind == GimmickKind.Ladder;
        public bool LastWallBounceDescending => _isLastWallBounceDescending;
        public int BellyBounceCount => _bellyBounceCount;
        public float MotionTime => _motionTime;
        public float RecoveryProgress => Recovering ? 1f - _recoveryRemaining / _tuning.RecoverySeconds : 1f;
        public float FallDistance => Mathf.Max(0f, _peakHeight - _position.y);
        public ClimbGimmick AttachedGimmick => _attachedGimmick;
        public float RopeAngle => _ropeAngle;
        public float EffectiveRopeLength => _effectiveRopeLength;
        public float PortalCooldownRemaining => _portalCooldownRemaining;
        public event Action<ClimbEvent, ClimbPlatform> EventRaised;
        #endregion
        #region Custom Method
        // 맵의 실제 출발점과 조절값을 연결합니다.
        public PauseSimulation(PauseLevel level, MovementTuning tuning)
        { this._level = level; this._tuning = tuning; Reset(level.StartPosition); }

        // 예측은 배 반동·포탈 유예·매달림 위상까지 같은 상태에서 시작합니다.
        private PauseSimulation(PauseSimulation source, PauseLevel copiedLevel)
        {
            _level = copiedLevel; _tuning = source._tuning; _position = source._position; _velocity = source._velocity;
            _facing = source._facing; _commandIndex = source._commandIndex; _isActiveJump = source._isActiveJump;
            _hasLongFall = source._hasLongFall; _supportTrampolineId = source._supportTrampolineId;
            _supportX = source._supportX; _peakHeight = source._peakHeight; _motionTime = source._motionTime;
            _isBellyBouncing = source._isBellyBouncing; _bellyBounceCount = source._bellyBounceCount;
            _contactRemaining = source._contactRemaining; _recoveryRemaining = source._recoveryRemaining; _reboundHeight = source._reboundHeight;
            _portalCooldownRemaining = source._portalCooldownRemaining; _attachmentCooldown = source._attachmentCooldown;
            _ropeElapsed = source._ropeElapsed; _ropePhase = source._ropePhase; _ropeAmplitude = source._ropeAmplitude;
            _ropeFrequency = source._ropeFrequency; _effectiveRopeLength = source._effectiveRopeLength; _ropeAngle = source._ropeAngle;
            _isLastWallBounceDescending = source._isLastWallBounceDescending; _bellyFlightElapsed = source._bellyFlightElapsed; _bellyFlightDuration = source._bellyFlightDuration;
            _support = source._support == null ? null : source.OnTrampoline ?
                new ClimbPlatform(source._support.Id, PlatformKind.Stone, source._support.X, source._support.Y, source._support.Width) : FindPlatform(source._support.Id);
            _attachedGimmick = source._attachedGimmick;
            _commands.AddRange(source._commands);
            CopySummitApproach(source);
        }

        // 현재 기믹과 물리 상태의 독립 복사본을 만듭니다.
        public PauseSimulation CopyForPlanning(PauseLevel copiedLevel) { return new PauseSimulation(this, copiedLevel); }

        // 재시작 또는 검사에서만 출발 상태를 지정합니다.
        public void Reset(Vector2 feet)
        {
            _isSummitApproach = false;
            _bellyFlightElapsed = _bellyFlightDuration = 0f;
            _hasLongFall = false; _supportTrampolineId = NO_TRAMPOLINE;
            _position = feet; _velocity = Vector2.zero; _facing = 1; _isActiveJump = false; _commands.Clear(); _commandIndex = 0;
            _support = null; _attachedGimmick = null; _peakHeight = feet.y; _motionTime = 0f;
            _isBellyBouncing = false; _bellyBounceCount = 0; _contactRemaining = _recoveryRemaining = _portalCooldownRemaining = _attachmentCooldown = 0f;
            foreach (ClimbPlatform p in _level.Platforms)
                if (!p.Broken && Contains(p, feet.x) && Mathf.Abs(p.TopAt(feet.x) - feet.y) < GROUND_ATTACH_TOLERANCE) _support = p;
            _supportX = _support == null ? 0f : _support.X;
        }

        // 남은 도약이나 자동 반동이 끝난 뒤에만 새 입력 묶음을 받습니다.
        public bool LoadCommands(IReadOnlyList<JumpCommand> input)
        {
            if (Busy) return false;
            _hasLongFall = false;
            _commands.Clear(); _commandIndex = 0;
            for (int i = 0; i < input.Count; i++) _commands.Add(input[i]);
            return true;
        }

        // 월드가 재생될 때만 시간·기믹·물리를 순서대로 갱신합니다.
        public void Tick(float dt = STEP)
        {
            if (dt <= 0f) return;
            _level.UpdateWorld(dt); _motionTime += dt;
            if (_isBellyBouncing && _contactRemaining <= 0f && _support == null) _bellyFlightElapsed += dt;
            _portalCooldownRemaining = Mathf.Max(0f, _portalCooldownRemaining - dt);
            _attachmentCooldown = Mathf.Max(0f, _attachmentCooldown - dt);
            CarrySupport();
            if (TickRecovery(dt) || TickContact(dt) || TickAttachment(dt)) return;
            if (Grounded && _commandIndex < _commands.Count) Launch(_commands[_commandIndex++]);
            if (_isSummitApproach) { TickSummitApproach(dt); return; }
            bool isIce = Grounded && (_support.Kind == PlatformKind.Ice || _support.Kind == PlatformKind.SlopeIce);
            if (Grounded)
            {
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, (isIce ? _tuning.IceFriction : _tuning.Friction) * dt);
                if (isIce) _velocity.x -= _support.Slope * _tuning.Gravity * dt;
            }
            ApplyWind(dt, isIce);
            Vector2 old = _position;
            _position.x += _velocity.x * dt;
            if (Grounded && Contains(_support, _position.x))
            { _position.y = _support.TopAt(_position.x); _velocity.y = 0f; _peakHeight = _position.y; }
            else
            {
                _support = null; _velocity.y -= _tuning.Gravity * dt; _position.y += _velocity.y * dt;
                _peakHeight = Mathf.Max(_peakHeight, _position.y);
            }
            DetectLongFall();
            if (TryPortal(old)) return;
            ResolveWalls(old);
            if (TryAttachment(old)) return;
            if (_support == null && _velocity.y <= 0f) FindLanding(old);
        }

        // 예약한 누르기 시간으로 도약 세기를 계산합니다.
        private void Launch(JumpCommand command)
        {
            TryBeginSummitApproach();
            float power = Mathf.Clamp01(command.HoldSeconds / _tuning.ChargeSeconds);
            float horizontal = Mathf.Lerp(_tuning.MinimumForward, _tuning.MaximumForward, power);
            float vertical = Mathf.Lerp(_tuning.MinimumJump, _tuning.MaximumJump, power);
            _velocity = new Vector2(_facing * horizontal, vertical);
            _support = null; _supportTrampolineId = NO_TRAMPOLINE; _isActiveJump = true; _peakHeight = _position.y; _bellyBounceCount = 0; _motionTime = 0f;
            EventRaised?.Invoke(ClimbEvent.Jump, null);
        }

        // 이동 발판의 접촉 위치를 운반하고 무너진 지지는 해제합니다.
        private void CarrySupport()
        {
            if (_support == null) return;
            if (_support.Broken) { _support = null; _recoveryRemaining = _contactRemaining = 0f; return; }
            _position.x += _support.X - _supportX; _supportX = _support.X;
        }

        // [수정됨: 회복 중에도 얼음 마찰·경사·바람·벽 반사를 적용하고 회복에서 대기로 바로 연결합니다.]
        private bool TickRecovery(float dt)
        {
            if (!Recovering) return false;
            if (_support == null) { _recoveryRemaining = 0f; return false; }
            bool isIce = _support.Kind == PlatformKind.Ice || _support.Kind == PlatformKind.SlopeIce;
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, (isIce ? _tuning.IceFriction : _tuning.Friction) * dt);
            if (isIce) _velocity.x -= _support.Slope * _tuning.Gravity * dt;
            ApplyWind(dt, isIce);
            Vector2 old = _position; _position.x += _velocity.x * dt; ResolveWalls(old);
            if (!Contains(_support, _position.x))
            {
                _support = null; _recoveryRemaining = 0f; _isActiveJump = true;
                _velocity.y -= _tuning.Gravity * dt; _position.y += _velocity.y * dt;
                return true;
            }
            _position.y = _support.TopAt(_position.x); _velocity.y = 0f;
            _recoveryRemaining = Mathf.Max(0f, _recoveryRemaining - dt);
            if (!Recovering)
            {
                _isActiveJump = false; _motionTime = 0f;
                FinishFall();
                EventRaised?.Invoke(ClimbEvent.Recovered, _support);
            }
            return true;
        }

        // 낙하 판정이 시작되는 순간 이후 명령을 제거하므로 예상 궤도에서도 같은 점프가 취소됩니다.
        private void DetectLongFall()
        {
            if (_hasLongFall || _velocity.y >= 0f || FallDistance < _tuning.BellyThreshold) return;
            _hasLongFall = true;
            if (_commandIndex < _commands.Count) _commands.RemoveRange(_commandIndex, _commands.Count - _commandIndex);
            EventRaised?.Invoke(ClimbEvent.LongFall, null);
        }

        // 낙하 후 안정된 발판에서만 도달 가능한 위쪽 발판 방향을 자동 선택합니다.
        private void FinishFall()
        {
            if (!_hasLongFall) return;
            _hasLongFall = false;
            if (!_tuning.AutoFaceAfterFall) return;
            int direction = PausePlatformDirection.FindAbove(_level, _position, _tuning, _facing);
            if (direction == _facing) return;
            _facing = direction; EventRaised?.Invoke(ClimbEvent.AutoFace, _support);
        }

        // 배가 눌리는 짧은 접촉 뒤 저장한 높이에 맞는 속도로 자동 발사합니다.
        private bool TickContact(float dt)
        {
            if (_contactRemaining <= 0f) return false;
            _contactRemaining = Mathf.Max(0f, _contactRemaining - dt);
            if (_support != null) _position.y = _support.TopAt(_position.x);
            if (_contactRemaining <= 0f)
            {
                _support = null; _velocity.y = Mathf.Sqrt(2f * _tuning.Gravity * _reboundHeight);
                _peakHeight = _position.y; _motionTime = 0f; _bellyFlightElapsed = 0f; _bellyFlightDuration = 2f * _velocity.y / _tuning.Gravity;
            }
            return true;
        }

        // 바람은 재생 중 모든 프레임에 가속도를 더하며 얼음에서는 더 오래 미끄러집니다.
        private void ApplyWind(float dt, bool isIce)
        {
            foreach (ClimbGimmick g in _level.Gimmicks)
                if (g.Kind == GimmickKind.Wind && BodyOverlaps(g))
                    _velocity += g.WindAcceleration * (isIce ? _tuning.WindOnIce : 1f) * dt;
        }

        // 벽의 실제 측면에서만 수평 운동을 반사합니다.
        private void ResolveWalls(Vector2 old)
        {
            float boundary = PauseLevel.WORLD_HALF_WIDTH - CollisionHalfWidth;
            if (!_level.SceneWalls && _position.x < -boundary) { _position.x = -boundary; Bounce(1); }
            else if (!_level.SceneWalls && _position.x > boundary) { _position.x = boundary; Bounce(-1); }
            foreach (ClimbWall wall in _level.Walls)
            {
                if (_position.y + CollisionHeight < wall.Bottom || _position.y > wall.Top) continue;
                float left = wall.X - wall.Width * 0.5f - CollisionHalfWidth;
                float right = wall.X + wall.Width * 0.5f + CollisionHalfWidth;
                if (_velocity.x > 0f && old.x <= left + WALL_CROSS_TOLERANCE && _position.x > left) { _position.x = left; Bounce(-1); }
                else if (_velocity.x < 0f && old.x >= right - WALL_CROSS_TOLERANCE && _position.x < right) { _position.x = right; Bounce(1); }
            }
        }

        // 충돌 직전 상승·하강 방향을 남겨 두 벽 반동 도트를 구분합니다.
        private void Bounce(int away)
        {
            _isLastWallBounceDescending = _velocity.y < 0f; _facing = away;
            _velocity.x = Mathf.Abs(_velocity.x) * away * _tuning.WallBounce; _motionTime = 0f;
            EventRaised?.Invoke(ClimbEvent.WallBounce, null);
        }

        // 짝 포탈로 같은 운동 에너지를 넘기며 출구 안쪽으로 다시 빨려들지 않도록 유예합니다.
        private bool TryPortal(Vector2 old)
        {
            if (_portalCooldownRemaining > 0f) return false;
            foreach (ClimbGimmick g in _level.Gimmicks)
            {
                if (g.Kind != GimmickKind.Portal || !SweptBodyOverlaps(g, old)) continue;
                ClimbGimmick target = FindPortal(g.TargetId);
                if (target == null) continue;
                Vector2 exit = target.ExitDirection.sqrMagnitude > DIRECTION_SQUARED_EPSILON ? target.ExitDirection.normalized : Vector2.right;
                float radius = Mathf.Abs(exit.x) * (target.Size.x * 0.5f + CollisionHalfWidth) + Mathf.Abs(exit.y) * (target.Size.y * 0.5f + CollisionHeight * 0.5f) + _tuning.PortalExitPadding;
                _position = target.Position + exit * radius - Vector2.up * (CollisionHeight * 0.5f);
                if (Mathf.Abs(exit.x) >= Mathf.Abs(exit.y)) { _facing = exit.x >= 0f ? 1 : -1; _velocity.x = _facing * Mathf.Max(_tuning.PortalMinimumExitHorizontal, Mathf.Abs(_velocity.x)); }
                else _velocity.y = Mathf.Sign(exit.y) * Mathf.Max(_tuning.PortalMinimumExitVertical, _velocity.magnitude);
                _support = null; _supportTrampolineId = NO_TRAMPOLINE; _attachedGimmick = null; _isActiveJump = true;
                _portalCooldownRemaining = _tuning.PortalCooldown; _motionTime = 0f;
                _peakHeight = _position.y + (_velocity.y < 0f ? _velocity.y * _velocity.y / (2f * _tuning.Gravity) : 0f);
                EventRaised?.Invoke(ClimbEvent.Portal, null); return true;
            }
            return false;
        }

        // 사다리 또는 줄에 실제로 닿으면 입력 없이 자동으로 잡습니다.
        private bool TryAttachment(Vector2 old)
        {
            if (_attachmentCooldown > 0f || _isBellyBouncing || Recovering) return false;
            foreach (ClimbGimmick g in _level.Gimmicks)
            {
                if (g.Kind == GimmickKind.Ladder && SweptBodyOverlaps(g, old) && _position.y < g.Position.y + g.Size.y * 0.5f - _tuning.LadderTopGrabClearance)
                {
                    _attachedGimmick = g; _support = null; _hasLongFall = false; _supportTrampolineId = NO_TRAMPOLINE; _isActiveJump = true; _velocity = Vector2.zero; _position.x = g.Position.x; _motionTime = 0f;
                    EventRaised?.Invoke(ClimbEvent.LadderGrab, null); return true;
                }
                if (g.Kind != GimmickKind.Rope || Grounded) continue;
                Vector2 anchor = g.Position, end = g.RopeEndAt(_level.WorldTime), line = end - anchor;
                Vector2 chest = _position + Vector2.up * _tuning.RopeAttachmentHeight;
                float fraction = Mathf.Clamp01(Vector2.Dot(chest - anchor, line) / Mathf.Max(ROPE_LINE_SQUARED_EPSILON, line.sqrMagnitude));
                Vector2 closest = anchor + line * fraction;
                if (fraction < _tuning.RopeMinimumGrabFraction || Vector2.Distance(chest, closest) > _tuning.RopeGrabRadius) continue;
                _attachedGimmick = g; _support = null; _hasLongFall = false; _supportTrampolineId = NO_TRAMPOLINE; _isActiveJump = false; _effectiveRopeLength = Mathf.Max(_tuning.RopeMinimumLength, Vector2.Distance(anchor, closest));
                _ropeAngle = Mathf.Atan2(closest.x - anchor.x, anchor.y - closest.y);
                Vector2 tangent = new Vector2(Mathf.Cos(_ropeAngle), Mathf.Sin(_ropeAngle));
                float existingAngular = Mathf.Cos(_level.WorldTime * g.SwingSpeed) * g.SwingAmplitude * Mathf.Deg2Rad * g.SwingSpeed;
                float omega = Vector2.Dot(_velocity, tangent) / _effectiveRopeLength + existingAngular;
                _ropeFrequency = Mathf.Sqrt(_tuning.Gravity / _effectiveRopeLength) * Mathf.Max(_tuning.RopeMinimumSwingSpeed, g.SwingSpeed);
                _ropeAmplitude = Mathf.Clamp(Mathf.Sqrt(_ropeAngle * _ropeAngle + omega * omega / (_ropeFrequency * _ropeFrequency)), _tuning.RopeMinimumSwingAmplitude, _tuning.RopeMaximumSwingAmplitude);
                _ropePhase = Mathf.Atan2(_ropeAngle, omega / _ropeFrequency); _ropeElapsed = 0f; _motionTime = 0f;
                EventRaised?.Invoke(ClimbEvent.RopeGrab, null); UpdateRope(0f); return true;
            }
            return false;
        }

        // 줄은 무감쇠 진동을 유지하고 사다리는 위 발판까지 자동 등반합니다.
        private bool TickAttachment(float dt)
        {
            if (OnRope)
            {
                UpdateRope(dt);
                if (_commandIndex < _commands.Count)
                {
                    int travel = Mathf.Abs(_velocity.x) > ROPE_DIRECTION_SPEED_EPSILON ? (_velocity.x > 0f ? 1 : -1) : (Mathf.Cos(_ropeFrequency * _ropeElapsed + _ropePhase) >= 0f ? 1 : -1);
                    Vector2 carry = _velocity; _attachedGimmick = null; _attachmentCooldown = _tuning.RopeExitCooldown; _facing = travel;
                    Launch(_commands[_commandIndex++]); _velocity.x += carry.x * _tuning.RopeCarryHorizontal; _velocity.y += Mathf.Max(0f, carry.y) * _tuning.RopeCarryVertical;
                    EventRaised?.Invoke(ClimbEvent.RopeRelease, null);
                }
                return true;
            }
            if (!OnLadder) return false;
            ClimbPlatform top = FindPlatform(_attachedGimmick.TargetId);
            float targetY = top == null ? _attachedGimmick.Position.y + _attachedGimmick.Size.y * 0.5f : top.TopAt(_attachedGimmick.Position.x);
            _position.x = _attachedGimmick.Position.x; _position.y = Mathf.MoveTowards(_position.y, targetY, _attachedGimmick.ClimbSpeed * dt);
            _velocity = Vector2.up * _attachedGimmick.ClimbSpeed;
            if (Mathf.Abs(_position.y - targetY) < LADDER_ARRIVAL_TOLERANCE)
            {
                _attachedGimmick = null; _attachmentCooldown = _tuning.LadderExitCooldown; _support = top; _supportX = top == null ? 0f : top.X;
                _velocity = Vector2.zero; _isActiveJump = false; _peakHeight = _position.y; _motionTime = 0f;
                EventRaised?.Invoke(ClimbEvent.LadderExit, top); if (top != null) EventRaised?.Invoke(ClimbEvent.Land, top);
            }
            return true;
        }

        // 해석식 진자를 사용해 수치 적분의 에너지 손실 없이 길이별 흔들림을 계산합니다.
        private void UpdateRope(float dt)
        {
            _ropeElapsed += dt; _ropeAngle = _ropeAmplitude * Mathf.Sin(_ropeFrequency * _ropeElapsed + _ropePhase);
            float omega = _ropeAmplitude * _ropeFrequency * Mathf.Cos(_ropeFrequency * _ropeElapsed + _ropePhase);
            _position = _attachedGimmick.Position + new Vector2(Mathf.Sin(_ropeAngle), -Mathf.Cos(_ropeAngle)) * _effectiveRopeLength - Vector2.up * _tuning.RopeAttachmentHeight;
            _velocity = new Vector2(Mathf.Cos(_ropeAngle), Mathf.Sin(_ropeAngle)) * (_effectiveRopeLength * omega);
            _peakHeight = Mathf.Max(_peakHeight, _position.y);
        }

        // 하강 중 가장 높은 실제 표면을 택하며 트램펄린과 일반 발판을 같은 충돌 순서로 처리합니다.
        private void FindLanding(Vector2 old)
        {
            ClimbPlatform candidate = null; ClimbGimmick trampoline = null; float highest = float.NegativeInfinity;
            foreach (ClimbPlatform p in _level.Platforms)
            {
                if (p.Broken || !Contains(p, _position.x)) continue;
                float top = p.TopAt(_position.x), oldTop = p.TopAt(old.x);
                if (old.y >= oldTop - LANDING_CROSS_TOLERANCE && _position.y <= top && top > highest) { candidate = p; highest = top; }
            }
            foreach (ClimbGimmick g in _level.Gimmicks)
            {
                if (g.Kind != GimmickKind.Trampoline || Mathf.Abs(_position.x - g.Position.x) > g.Size.x * 0.5f) continue;
                float top = g.Position.y + g.Size.y * 0.5f;
                if (old.y >= top - LANDING_CROSS_TOLERANCE && _position.y <= top && top >= highest - SURFACE_PRIORITY_TOLERANCE) { trampoline = g; highest = top; }
            }
            if (candidate == null && trampoline == null) return;
            float drop = Mathf.Max(0f, _peakHeight - highest); _position.y = highest;
            if (trampoline != null)
            {
                float height = drop * Mathf.Clamp(trampoline.BounceRatio, 0f, 1f);
                _support = null; _supportTrampolineId = NO_TRAMPOLINE; _isBellyBouncing = false; _contactRemaining = _recoveryRemaining = 0f;
                // [수정됨: 접촉 후 옆으로 미끄러지지 않고 수직으로 반동하며 작은 반동은 안정된 대기로 끝납니다.]
                _velocity.x = 0f;
                if (height <= _tuning.TrampolineSettleHeight)
                {
                    _support = new ClimbPlatform(-trampoline.Id - 1, PlatformKind.Stone, trampoline.Position.x, highest, trampoline.Size.x);
                    _supportTrampolineId = trampoline.Id; _supportX = _support.X; _velocity = Vector2.zero;
                    _isActiveJump = false; _peakHeight = highest; _motionTime = 0f; FinishFall();
                    EventRaised?.Invoke(ClimbEvent.TrampolineRest, _support); return;
                }
                _velocity.y = Mathf.Sqrt(2f * _tuning.Gravity * Mathf.Max(_tuning.TrampolineMinimumBounceHeight, height));
                _peakHeight = highest; _isActiveJump = true; _motionTime = 0f;
                EventRaised?.Invoke(ClimbEvent.Trampoline, null); return;
            }
            _support = candidate; _supportTrampolineId = NO_TRAMPOLINE; _supportX = candidate.X; _velocity.y = 0f;
            if (candidate.Kind == PlatformKind.Crumble && candidate.BreakRemaining < 0f) candidate.BreakRemaining = _tuning.CrumbleBreakSeconds;
            if (candidate.Kind == PlatformKind.Cloud && candidate.BreakRemaining < 0f) candidate.BreakRemaining = _tuning.CloudBreakSeconds;
            if (_isBellyBouncing || drop >= _tuning.BellyThreshold)
            {
                if (!_isBellyBouncing)
                {
                    _bellyBounceCount = 0; _isBellyBouncing = true;
                }
                _reboundHeight = drop * _tuning.BellyHeightRatio; _motionTime = 0f; _isActiveJump = true;
                if (_reboundHeight >= _tuning.MinimumBellyBounce && _bellyBounceCount < _tuning.MaximumBellyBounces)
                {
                    _bellyBounceCount++; _contactRemaining = _tuning.ContactSeconds;
                    _velocity.x = Mathf.Abs(_velocity.x) < _tuning.BellyMinimumHorizontalSpeed ? _facing * _tuning.BellyMinimumForward : _velocity.x * _tuning.BellyHorizontalRetention;
                    EventRaised?.Invoke(ClimbEvent.BellyBounce, candidate);
                }
                else
                {
                    _isBellyBouncing = false; _recoveryRemaining = _tuning.RecoverySeconds; _velocity.y = 0f;
                    EventRaised?.Invoke(ClimbEvent.BellyRest, candidate);
                }
                _peakHeight = highest; return;
            }
            _isActiveJump = false; _peakHeight = highest; _motionTime = 0f; FinishFall();
            EventRaised?.Invoke(ClimbEvent.Land, candidate);
        }

        // 씬의 식별자가 연속 번호가 아니어도 연결한 발판을 찾습니다.
        private ClimbPlatform FindPlatform(int id)
        { foreach (ClimbPlatform p in _level.Platforms) if (p.Id == id) return p; return null; }

        // 중첩된 장치 순회를 분리하되 기존과 같은 목록 순서로 첫 번째 짝 포탈을 찾습니다.
        private ClimbGimmick FindPortal(int id)
        { foreach (ClimbGimmick candidate in _level.Gimmicks) if (candidate.Id == id && candidate.Kind == GimmickKind.Portal) return candidate; return null; }

        // 캐릭터의 몸과 직사각형 기믹의 접촉을 계산합니다.
        private bool BodyOverlaps(ClimbGimmick g)
        { return Mathf.Abs(_position.x - g.Position.x) <= g.Size.x * 0.5f + CollisionHalfWidth && _position.y <= g.Position.y + g.Size.y * 0.5f && _position.y + CollisionHeight >= g.Position.y - g.Size.y * 0.5f; }

        // 한 프레임에 얇은 포탈이나 사다리를 지나쳐도 진입을 놓치지 않습니다.
        private bool SweptBodyOverlaps(ClimbGimmick g, Vector2 old)
        {
            if (BodyOverlaps(g)) return true;
            Vector2 from = old + Vector2.up * (CollisionHeight * 0.5f), to = _position + Vector2.up * (CollisionHeight * 0.5f);
            Vector2 half = g.Size * 0.5f + new Vector2(CollisionHalfWidth, CollisionHeight * 0.5f);
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float delta = to[axis] - from[axis], min = g.Position[axis] - half[axis], max = g.Position[axis] + half[axis];
                bool isParallel = Mathf.Abs(delta) < SWEPT_AXIS_EPSILON;
                if (isParallel && (from[axis] < min || from[axis] > max)) return false;
                if (isParallel) continue;
                float a = (min - from[axis]) / delta, b = (max - from[axis]) / delta;
                if (a > b) { float temp = a; a = b; b = temp; }
                enter = Mathf.Max(enter, a); leave = Mathf.Min(leave, b); if (enter > leave) return false;
            }
            return true;
        }

        // 발 중심이 표면의 수평 범위 안에 있을 때만 지지합니다.
        private static bool Contains(ClimbPlatform p, float x) { return Mathf.Abs(x - p.X) <= p.Width * 0.5f; }
        #endregion
    }
}
