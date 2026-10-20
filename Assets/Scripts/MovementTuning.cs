using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 점프·반사·배 반동·장치의 물리 조절값을 한곳에서 관리합니다.
    /// PauseGame의 점프와 반사 물리 항목에서 설정하며 별도로 연결할 오브젝트는 없습니다.
    /// </summary>
    [Serializable]
    public sealed class MovementTuning
    {
        #region Variables
        private const float MINIMUM_COLLISION_SIZE = 0.01f;
        // [수정됨: 충돌 크기 공유] 실제 이동과 궤도 예측이 같은 Inspector 크기를 사용합니다.
        [Header("캐릭터 실제 충돌 크기")]
        [SerializeField, Min(MINIMUM_COLLISION_SIZE), Tooltip("캐릭터 몸의 가로 반폭입니다. 벽 반사와 포탈·사다리·바람의 실제 접촉 범위에 함께 반영됩니다.")] private float _collisionHalfWidth = 0.22f;
        [SerializeField, Min(MINIMUM_COLLISION_SIZE), Tooltip("발에서 머리까지의 실제 충돌 높이입니다. 벽·장치 판정과 예상 궤도에서 같은 값을 사용합니다.")] private float _collisionHeight = 0.85f;
        [UnityEngine.Serialization.FormerlySerializedAs("minimumJump")]
        [Header("점프 세기 · 걷기와 공중 조작 없음")]
        [SerializeField, Tooltip("짧게 눌렀을 때의 수직 속도입니다.")] private float _minimumJump = 6f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximumJump")]
        [SerializeField, Tooltip("최대로 눌렀을 때의 수직 속도입니다.")] private float _maximumJump = 12.5f;
        [UnityEngine.Serialization.FormerlySerializedAs("chargeSeconds")]
        [SerializeField, Tooltip("최대 세기까지 스페이스를 누르는 실제 시간입니다.")] private float _chargeSeconds = 0.85f;
        [UnityEngine.Serialization.FormerlySerializedAs("minimumForward")]
        [SerializeField, Tooltip("짧은 점프의 수평 속도입니다. 바라보는 방향으로만 발사합니다.")] private float _minimumForward = 3.2f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximumForward")]
        [SerializeField, Tooltip("최대 점프의 수평 속도입니다. 공중에서는 바꿀 수 없습니다.")] private float _maximumForward = 6.4f;
        [UnityEngine.Serialization.FormerlySerializedAs("gravity")]
        [Header("벽 반사와 착지 관성")]
        [SerializeField, Tooltip("중력입니다. 점프가 그리는 포물선의 높이와 체공 시간을 정합니다.")] private float _gravity = 22f;
        [UnityEngine.Serialization.FormerlySerializedAs("wallBounce")]
        [SerializeField, Range(0.1f, 1f), Tooltip("벽 충돌 후 남는 수평 속도의 비율입니다. 충돌할 때만 바라보는 방향이 바뀝니다.")] private float _wallBounce = 0.82f;
        [UnityEngine.Serialization.FormerlySerializedAs("friction")]
        [SerializeField, Tooltip("일반 발판에서 착지 속도를 줄이는 힘입니다.")] private float _friction = 32f;
        [UnityEngine.Serialization.FormerlySerializedAs("iceFriction")]
        [SerializeField, Tooltip("얼음의 마찰력입니다. 작을수록 오래 미끄러지며 경사와 벽 반사의 영향을 받습니다.")] private float _iceFriction = 0.65f;
        [UnityEngine.Serialization.FormerlySerializedAs("bellyThreshold")]
        [Header("높은 낙하 · 배 반동과 회복")]
        [SerializeField, Min(0.1f), Tooltip("확장 코스의 발판 두 칸 약 3.46m를 기준으로 한 배 반동 시작 높이입니다. 낙하 정점과 접촉면의 실제 높이 차이를 사용합니다.")] private float _bellyThreshold = 3.5f;
        [UnityEngine.Serialization.FormerlySerializedAs("bellyHeightRatio")]
        [SerializeField, Range(0.1f, 0.9f), Tooltip("배가 바닥에 닿기 전 낙하 높이 중 다시 오르는 높이의 비율입니다.")] private float _bellyHeightRatio = 0.55f;
        [UnityEngine.Serialization.FormerlySerializedAs("minimumBellyBounce")]
        [SerializeField, Min(0.05f), Tooltip("다음 반동 높이가 이 값보다 작으면 엎드렸다가 일어납니다.")] private float _minimumBellyBounce = 0.4f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximumBellyBounces")]
        [SerializeField, Range(2, 12), Tooltip("배 반동이 무한 반복되지 않도록 제한하는 최대 횟수입니다.")] private int _maximumBellyBounces = 6;
        [UnityEngine.Serialization.FormerlySerializedAs("recoverySeconds")]
        [SerializeField, Min(0.1f), Tooltip("마지막 반동 후 엎드렸다가 일어나는 시간입니다. 이 동안 다음 예약 점프를 소비하지 않습니다.")] private float _recoverySeconds = 1.55f;
        [UnityEngine.Serialization.FormerlySerializedAs("contactSeconds")]
        [SerializeField, Range(0.03f, 0.4f), Tooltip("배가 눌려 있는 짧은 접촉 시간입니다.")] private float _contactSeconds = 0.12f;
        [UnityEngine.Serialization.FormerlySerializedAs("windOnIce")]
        [Header("특수 기믹")]
        [SerializeField, Min(1f), Tooltip("얼음 위에서 바람이 수평 가속도에 더해지는 배율입니다.")] private float _windOnIce = 1.8f;
        [UnityEngine.Serialization.FormerlySerializedAs("portalCooldown")]
        [SerializeField, Min(0.05f), Tooltip("포탈을 나온 직후 재진입을 막는 실제 플레이 시간입니다. 정지 중에는 줄어들지 않습니다.")] private float _portalCooldown = 0.4f;
        // [수정됨: 물리 조절값 노출] 시뮬레이션 내부의 기존 수치를 Inspector로 옮겼으며 기본 동작은 유지합니다.
        [Header("로프 · 잡는 범위와 매달림")]
        [SerializeField, Min(0f), Tooltip("몸의 잡는 지점과 줄 사이의 거리가 이 값 이하면 자동으로 잡습니다.")] private float _ropeGrabRadius = 0.4f;
        [SerializeField, Range(0f, 1f), Tooltip("위 고정점으로부터 이 비율보다 아래에 있는 줄만 잡을 수 있습니다.")] private float _ropeMinimumGrabFraction = 0.12f;
        [SerializeField, Min(0.01f), Tooltip("고정점 가까이에서 잡아도 유지하는 최소 매달림 길이입니다.")] private float _ropeMinimumLength = 0.6f;
        [SerializeField, Min(0f), Tooltip("줄을 잡은 뒤 유지하는 최소 흔들림 진폭입니다. 단위는 라디안입니다.")] private float _ropeMinimumSwingAmplitude = 0.16f;
        [SerializeField, Min(0f), Tooltip("빠르게 줄을 잡아도 제한하는 최대 흔들림 진폭입니다. 단위는 라디안입니다.")] private float _ropeMaximumSwingAmplitude = 1.4f;
        [SerializeField, Min(0.01f), Tooltip("장치 흔들림 속도가 작아도 진자 계산에 사용하는 최소 속도 배율입니다.")] private float _ropeMinimumSwingSpeed = 0.2f;
        [SerializeField, Min(0f), Tooltip("캐릭터 발에서 줄을 잡는 지점까지의 높이입니다. 손과 줄의 접촉 위치에 영향을 줍니다.")] private float _ropeAttachmentHeight = 0.55f;
        [Header("로프 · 뛰어내린 뒤 관성")]
        [SerializeField, Min(0f), Tooltip("줄에서 뛰어내린 직후 다시 줄이나 사다리를 잡지 않는 시간입니다.")] private float _ropeExitCooldown = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("줄에서 뛰어내릴 때 기존 수평 진자 속도를 점프에 더하는 비율입니다.")] private float _ropeCarryHorizontal = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("줄에서 뛰어내릴 때 기존 상승 속도를 점프에 더하는 비율입니다.")] private float _ropeCarryVertical = 0.25f;
        [Header("사다리 · 접촉과 출구")]
        [SerializeField, Min(0f), Tooltip("사다리 위에 도착한 직후 줄이나 사다리를 다시 잡지 않는 시간입니다.")] private float _ladderExitCooldown = 0.6f;
        [SerializeField, Min(0f), Tooltip("사다리 맨 위에서 이 거리만큼 떨어진 아래쪽까지만 새로 잡습니다.")] private float _ladderTopGrabClearance = 0.1f;
        [Header("포탈 · 출구 위치와 최소 속도")]
        [SerializeField, Min(0f), Tooltip("출구 판정과 캐릭터 크기를 합한 거리에서 추가로 띄우는 여유 공간입니다.")] private float _portalExitPadding = 0.12f;
        [SerializeField, Min(0f), Tooltip("좌우 출구를 나왔을 때 유지하는 최소 수평 속도입니다.")] private float _portalMinimumExitHorizontal = 0.5f;
        [SerializeField, Min(0f), Tooltip("위아래 출구를 나왔을 때 유지하는 최소 수직 속도입니다.")] private float _portalMinimumExitVertical = 2f;
        [Header("배 반동 · 수평 속도")]
        [SerializeField, Min(0f), Tooltip("배 반동에서 수평 속도가 이 값보다 느리면 최소 전진 속도를 적용합니다.")] private float _bellyMinimumHorizontalSpeed = 0.4f;
        [SerializeField, Min(0f), Tooltip("거의 멈춘 상태의 배 반동에 현재 바라보는 방향으로 주는 전진 속도입니다.")] private float _bellyMinimumForward = 1.4f;
        [SerializeField, Range(0f, 1f), Tooltip("이미 움직이는 배 반동에서 다음 반동으로 남기는 수평 속도 비율입니다.")] private float _bellyHorizontalRetention = 0.8f;
        [Header("트램펄린과 발판 소멸")]
        [SerializeField, Min(0f), Tooltip("낙하가 매우 작아도 트램펄린에서 되튀는 최소 높이입니다.")] private float _trampolineMinimumBounceHeight = 0.03f;
        [SerializeField, Min(0f), Tooltip("붕괴 발판을 밟은 뒤 사라지기까지의 시간입니다. 일시정지 중에도 줄어듭니다.")] private float _crumbleBreakSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("구름 발판을 밟은 뒤 사라지기까지의 월드 시간입니다. 일시정지 중에는 멈춥니다.")] private float _cloudBreakSeconds = 0.3f;
        [Header("예상 궤도 계산")]
        [SerializeField, Min(1), Tooltip("한 번의 예상 궤도를 계산할 최대 고정 시간 단계 수입니다. 늘리면 긴 예약을 끝까지 보지만 계산량도 늘어납니다.")] private int _predictionMaxSteps = 1800;
        [SerializeField, Min(1), Tooltip("몇 단계마다 예상 경로 점을 저장할지 정합니다. 값이 작으면 선이 촘촘해지고 점 개수가 늘어납니다.")] private int _predictionSampleStride = 3;
        #endregion
        #region Property
        public float CollisionHalfWidth => Mathf.Max(MINIMUM_COLLISION_SIZE, _collisionHalfWidth);
        public float CollisionHeight => Mathf.Max(MINIMUM_COLLISION_SIZE, _collisionHeight);
        public float MinimumJump => Mathf.Max(1f, _minimumJump);
        public float MaximumJump => Mathf.Max(MinimumJump, _maximumJump);
        public float ChargeSeconds => Mathf.Max(0.1f, _chargeSeconds);
        public float MinimumForward => Mathf.Max(0.1f, _minimumForward);
        public float MaximumForward => Mathf.Max(MinimumForward, _maximumForward);
        public float Gravity => Mathf.Max(1f, _gravity);
        public float WallBounce => Mathf.Clamp01(_wallBounce);
        public float Friction => Mathf.Max(0f, _friction);
        public float IceFriction => Mathf.Max(0f, _iceFriction);
        public float BellyThreshold => Mathf.Max(0.1f, _bellyThreshold);
        public float BellyHeightRatio => Mathf.Clamp(_bellyHeightRatio, 0.1f, 0.9f);
        public float MinimumBellyBounce => Mathf.Max(0.05f, _minimumBellyBounce);
        public int MaximumBellyBounces => Mathf.Clamp(_maximumBellyBounces, 2, 12);
        public float RecoverySeconds => Mathf.Max(0.1f, _recoverySeconds);
        public float ContactSeconds => Mathf.Max(0.03f, _contactSeconds);
        public float WindOnIce => Mathf.Max(1f, _windOnIce);
        public float PortalCooldown => Mathf.Max(0.05f, _portalCooldown);
        // [TUNE] 실제 이동과 예상 궤도가 모두 아래의 같은 읽기 전용 조절값을 사용합니다.
        public float RopeGrabRadius => Mathf.Max(0f, _ropeGrabRadius);
        public float RopeMinimumGrabFraction => Mathf.Clamp01(_ropeMinimumGrabFraction);
        public float RopeMinimumLength => Mathf.Max(Mathf.Epsilon, _ropeMinimumLength);
        public float RopeMinimumSwingAmplitude => Mathf.Max(0f, _ropeMinimumSwingAmplitude);
        public float RopeMaximumSwingAmplitude => Mathf.Max(RopeMinimumSwingAmplitude, _ropeMaximumSwingAmplitude);
        public float RopeMinimumSwingSpeed => Mathf.Max(Mathf.Epsilon, _ropeMinimumSwingSpeed);
        public float RopeAttachmentHeight => Mathf.Max(0f, _ropeAttachmentHeight);
        public float RopeExitCooldown => Mathf.Max(0f, _ropeExitCooldown);
        public float RopeCarryHorizontal => Mathf.Clamp01(_ropeCarryHorizontal);
        public float RopeCarryVertical => Mathf.Clamp01(_ropeCarryVertical);
        public float LadderExitCooldown => Mathf.Max(0f, _ladderExitCooldown);
        public float LadderTopGrabClearance => Mathf.Max(0f, _ladderTopGrabClearance);
        public float PortalExitPadding => Mathf.Max(0f, _portalExitPadding);
        public float PortalMinimumExitHorizontal => Mathf.Max(0f, _portalMinimumExitHorizontal);
        public float PortalMinimumExitVertical => Mathf.Max(0f, _portalMinimumExitVertical);
        public float BellyMinimumHorizontalSpeed => Mathf.Max(0f, _bellyMinimumHorizontalSpeed);
        public float BellyMinimumForward => Mathf.Max(0f, _bellyMinimumForward);
        public float BellyHorizontalRetention => Mathf.Clamp01(_bellyHorizontalRetention);
        public float TrampolineMinimumBounceHeight => Mathf.Max(0f, _trampolineMinimumBounceHeight);
        public float CrumbleBreakSeconds => Mathf.Max(0f, _crumbleBreakSeconds);
        public float CloudBreakSeconds => Mathf.Max(0f, _cloudBreakSeconds);
        public int PredictionMaxSteps => Mathf.Max(1, _predictionMaxSteps);
        public int PredictionSampleStride => Mathf.Max(1, _predictionSampleStride);
        #endregion
    }

}
