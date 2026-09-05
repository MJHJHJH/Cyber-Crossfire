using UnityEngine;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

namespace CommandoRobot
{
    /// <summary>
    /// 镜头抖动档位。枚举值即优先级：Fire &lt; Kill &lt; Hit &lt; Death。
    /// </summary>
    public enum CameraShakePreset
    {
        /// <summary>无进行中抖动</summary>
        None = -1,
        /// <summary>玩家开火（最低）</summary>
        Fire = 0,
        /// <summary>击杀反馈</summary>
        Kill = 1,
        /// <summary>玩家受击</summary>
        Hit = 2,
        /// <summary>玩家死亡（最高）</summary>
        Death = 3
    }

    /// <summary>单次抖动参数，由调用方传入。振幅/频率用区间，每次播放在区间内随机。</summary>
    public struct ShakeSettings
    {
        public float Duration;
        public float AmplitudeMin;
        public float AmplitudeMax;
        public float FrequencyMin;
        public float FrequencyMax;
        public bool WithZoom;
        public float ZoomFov;
        public float ZoomTransition;
        public float ZoomHold;
        public bool WithRotation;
        public float RotationAmplitude;

        /// <summary>便捷：以中心振幅/频率生成小幅随机区间。</summary>
        public static ShakeSettings Position(float duration, float amplitude, float frequency)
        {
            return Range(duration, amplitude * 0.65f, amplitude, frequency * 0.85f, frequency * 1.15f);
        }

        /// <summary>自定义随机区间（推荐各枪使用）。</summary>
        public static ShakeSettings Range(float duration, float ampMin, float ampMax, float freqMin, float freqMax)
        {
            return new ShakeSettings
            {
                Duration = duration,
                AmplitudeMin = ampMin,
                AmplitudeMax = ampMax,
                FrequencyMin = freqMin,
                FrequencyMax = freqMax
            };
        }
    }

    public class CameraControl : MonoBehaviour
    {
        const float BASE_FOV = 50f;
        const string FOLLOW_RIG_NAME = "CameraFollowRig";

        public Vector3 m_TargetOffset = Vector3.zero;
        public static CameraControl m_Current;
        public Camera m_MyCamera;

        [SerializeField] bool _enableRotationShakeOnDeath = true;

        Transform _followRig;
        MMCameraShaker _positionShaker;
        MMWiggle _wiggle;
        MMCameraZoom _cameraZoom;
        Vector3 _direction;
        float _zoomBusyUntil;

        bool _isShaking;
        CameraShakePreset _currentShakeType = CameraShakePreset.None;
        float _shakeBusyUntil;

        /// <summary>当前是否有抖动占用中。</summary>
        public bool IsShaking => _isShaking && Time.time < _shakeBusyUntil;

        /// <summary>进行中的抖动类型；空闲时为 None。</summary>
        public CameraShakePreset CurrentShakeType => IsShaking ? _currentShakeType : CameraShakePreset.None;

        /// <summary>优先级：开火 &lt; 击杀 &lt; 受击 &lt; 玩家死亡。低优先级拒绝；同级从头重播；更高可抢占。</summary>
        static int GetShakePriority(CameraShakePreset preset) => (int)preset;

        bool TryBeginShake(CameraShakePreset preset, float duration)
        {
            RefreshShakeState();
            // 仅拒绝更低优先级；同级允许从头重播
            if (_isShaking && GetShakePriority(preset) < GetShakePriority(_currentShakeType))
                return false;

            _isShaking = true;
            _currentShakeType = preset;
            _shakeBusyUntil = Time.time + Mathf.Max(0.01f, duration);
            return true;
        }

        void RefreshShakeState()
        {
            if (_isShaking && Time.time >= _shakeBusyUntil)
                ClearShakeState();
        }

        void ClearShakeState()
        {
            _isShaking = false;
            _currentShakeType = CameraShakePreset.None;
            _shakeBusyUntil = 0f;
        }

        void Awake()
        {
            m_Current = this;
            EnsureFollowRigAndShakers();
        }

        void OnDestroy()
        {
            ClearShakeState();
            if (m_Current == this)
                m_Current = null;
        }

        void Start()
        {
            _direction = transform.forward;
            if (m_MyCamera != null)
                m_MyCamera.fieldOfView = BASE_FOV;

            if (PlayerCharacter.m_Current == null)
                return;

            float distance = 30f;
            _direction = Quaternion.Euler(50f, 0f, 0f) * Vector3.forward;
            Vector3 targetPosition = PlayerCharacter.m_Current.transform.position;
            _followRig.position = targetPosition + -distance * _direction;
            _followRig.rotation = Quaternion.LookRotation(_direction);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            RefreshShakeState();

            if (m_MyCamera == null)
                return;

            if (Time.time < _zoomBusyUntil)
                return;

            m_MyCamera.fieldOfView = Mathf.Lerp(m_MyCamera.fieldOfView, BASE_FOV, 5f * Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (PlayerCharacter.m_Current == null || _followRig == null)
                return;

            m_TargetOffset = Vector3.Lerp(m_TargetOffset, 6f * PlayerCharacter.m_Current.m_CharBody.m_UpperAimBase.forward, Time.deltaTime);
            float distance = 30f;
            _direction = Quaternion.Euler(50f, 0f, 0f) * Vector3.forward;
            Vector3 targetPosition = PlayerCharacter.m_Current.transform.position + m_TargetOffset;

            if (GameControl.m_Current != null && GameControl.m_Current.m_IsBossFight && GameControl.m_Current.m_Boss != null)
            {
                targetPosition = GameControl.m_Current.m_Boss.transform.position + PlayerCharacter.m_Current.transform.position;
                targetPosition *= 0.5f;
            }

            _followRig.position = Vector3.Lerp(_followRig.position, targetPosition + -distance * _direction, 5f * Time.deltaTime);
            _followRig.rotation = Quaternion.LookRotation(Vector3.Lerp(_followRig.forward, _direction, 5f * Time.deltaTime));
        }

        /// <summary>按优先级 + 外部参数播放抖动。同级/低优先级在占用中会被拒绝。</summary>
        public void PlayShake(CameraShakePreset priority, ShakeSettings settings, float intensity = 1f)
        {
            intensity = Mathf.Max(0f, intensity);
            if (!TryBeginShake(priority, settings.Duration))
                return;

            ApplyPositionShake(settings, intensity);

            if (settings.WithZoom)
                ApplyZoom(settings.ZoomFov, settings.ZoomTransition, settings.ZoomHold);

            if (settings.WithRotation && _enableRotationShakeOnDeath)
            {
                float freq = Mathf.Max(settings.FrequencyMin, settings.FrequencyMax);
                if (freq <= 0f)
                    freq = 25f;
                ApplyRotationShake(settings.Duration, settings.RotationAmplitude * intensity, freq);
            }
        }

        /// <summary>兼容旧调用：自行组参数，优先级按幅度粗分。</summary>
        public void StartShake(float duration, float amplitude)
        {
            PlayPositionShake(duration, amplitude, 40f);
        }

        public void PlayPositionShake(float duration, float amplitude, float frequency = 40f)
        {
            CameraShakePreset priority = InferLegacyPriority(duration, amplitude);
            PlayShake(priority, ShakeSettings.Position(duration, amplitude, frequency));
        }

        public void PlayRotationShake(float duration, float amplitude, float frequency = 25f)
        {
            if (!TryBeginShake(CameraShakePreset.Hit, duration))
                return;

            ApplyRotationShake(duration, amplitude, frequency);
        }

        public void PlayZoom(float targetFov, float transitionDuration, float holdDuration)
        {
            ApplyZoom(targetFov, transitionDuration, holdDuration);
        }

        static CameraShakePreset InferLegacyPriority(float duration, float amplitude)
        {
            if (duration >= 0.4f || amplitude >= 0.4f)
                return CameraShakePreset.Death;
            if (amplitude >= 0.25f)
                return CameraShakePreset.Kill;
            if (amplitude >= 0.12f)
                return CameraShakePreset.Hit;
            return CameraShakePreset.Fire;
        }

        void ApplyPositionShake(ShakeSettings settings, float intensity)
        {
            EnsureFollowRigAndShakers();
            if (_wiggle == null)
                return;

            float ampMin = Mathf.Min(settings.AmplitudeMin, settings.AmplitudeMax) * intensity;
            float ampMax = Mathf.Max(settings.AmplitudeMin, settings.AmplitudeMax) * intensity;
            float freqMin = Mathf.Min(settings.FrequencyMin, settings.FrequencyMax);
            float freqMax = Mathf.Max(settings.FrequencyMin, settings.FrequencyMax);
            if (freqMax <= 0f)
            {
                freqMin = 30f;
                freqMax = 40f;
            }

            // Noise：每帧 Perlin 扰动；振幅/频率在区间内随机，避免固定来回抽
            _wiggle.PositionWiggleProperties.WiggleType = WiggleTypes.Noise;
            _wiggle.PositionWiggleProperties.AmplitudeMin = Vector3.one * ampMin;
            _wiggle.PositionWiggleProperties.AmplitudeMax = Vector3.one * ampMax;
            _wiggle.PositionWiggleProperties.FrequencyMin = freqMin;
            _wiggle.PositionWiggleProperties.FrequencyMax = freqMax;
            _wiggle.PositionWiggleProperties.NoiseFrequencyMin = Vector3.one * freqMin;
            _wiggle.PositionWiggleProperties.NoiseFrequencyMax = Vector3.one * freqMax;
            _wiggle.PositionWiggleProperties.UseUnscaledTime = false;
            _wiggle.WigglePosition(settings.Duration);
        }

        void ApplyRotationShake(float duration, float amplitude, float frequency)
        {
            EnsureFollowRigAndShakers();
            if (_wiggle == null)
                return;

            _wiggle.RotationActive = true;
            _wiggle.RotationWiggleProperties.AmplitudeMin = Vector3.one * -amplitude;
            _wiggle.RotationWiggleProperties.AmplitudeMax = Vector3.one * amplitude;
            _wiggle.RotationWiggleProperties.FrequencyMin = frequency;
            _wiggle.RotationWiggleProperties.FrequencyMax = frequency;
            _wiggle.RotationWiggleProperties.NoiseFrequencyMin = frequency * Vector3.one;
            _wiggle.RotationWiggleProperties.NoiseFrequencyMax = frequency * Vector3.one;
            _wiggle.RotationWiggleProperties.WiggleType = WiggleTypes.Noise;
            _wiggle.WiggleRotation(duration);
        }

        void ApplyZoom(float targetFov, float transitionDuration, float holdDuration)
        {
            EnsureFollowRigAndShakers();
            _zoomBusyUntil = Time.time + transitionDuration * 2f + holdDuration + 0.05f;
            MMCameraZoomEvent.Trigger(
                MMCameraZoomModes.For,
                targetFov,
                transitionDuration,
                holdDuration,
                null,
                false,
                false,
                false);
        }

        void EnsureFollowRigAndShakers()
        {
            if (_followRig == null)
            {
                if (transform.parent != null && transform.parent.name == FOLLOW_RIG_NAME)
                {
                    _followRig = transform.parent;
                }
                else
                {
                    var rigGo = new GameObject(FOLLOW_RIG_NAME);
                    _followRig = rigGo.transform;
                    _followRig.SetPositionAndRotation(transform.position, transform.rotation);
                    transform.SetParent(_followRig, true);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                }
            }

            if (m_MyCamera == null)
                m_MyCamera = GetComponent<Camera>();

            if (_cameraZoom == null && m_MyCamera != null)
                m_MyCamera.fieldOfView = BASE_FOV;

            if (_wiggle == null)
            {
                _wiggle = GetComponent<MMWiggle>();
                if (_wiggle == null)
                    _wiggle = gameObject.AddComponent<MMWiggle>();

                if (_wiggle.PositionWiggleProperties == null)
                    _wiggle.PositionWiggleProperties = new WiggleProperties();
                if (_wiggle.RotationWiggleProperties == null)
                    _wiggle.RotationWiggleProperties = new WiggleProperties();
                if (_wiggle.ScaleWiggleProperties == null)
                    _wiggle.ScaleWiggleProperties = new WiggleProperties();

                _wiggle.UpdateMode = MMWiggle.UpdateModes.LateUpdate;
                _wiggle.PositionActive = true;
                _wiggle.PositionWiggleProperties.WiggleType = WiggleTypes.Noise;
                _wiggle.PositionWiggleProperties.WigglePermitted = false;
                _wiggle.PositionWiggleProperties.LimitedTimeResetValue = true;
                _wiggle.RotationWiggleProperties.WiggleType = WiggleTypes.Noise;
                _wiggle.RotationWiggleProperties.WigglePermitted = false;
                _wiggle.RotationWiggleProperties.LimitedTimeResetValue = true;
            }

            if (_positionShaker == null)
            {
                _positionShaker = GetComponent<MMCameraShaker>();
                if (_positionShaker == null)
                    _positionShaker = gameObject.AddComponent<MMCameraShaker>();
            }

            if (_cameraZoom == null)
            {
                _cameraZoom = GetComponent<MMCameraZoom>();
                if (_cameraZoom == null)
                    _cameraZoom = gameObject.AddComponent<MMCameraZoom>();
            }
        }
    }
}
