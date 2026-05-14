using System;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace VisionProAttentionGuide
{
    public class AttentionGuidanceManager : MonoBehaviour
    {
        public const string BuildLabel = "MVP v11 - accept tracked still pose";

        [Header("References")]
        [SerializeField] private Transform userCamera;
        [SerializeField] private Transform targetObject;
        [SerializeField] private VirtualPersonGuide virtualPerson;

        [Header("Attention Settings")]
        [SerializeField] private float attentionAngle = 12f;
        [SerializeField] private float viewportCenterRadius = 0.24f;
        [SerializeField] private float recognitionHoldTime = 0.35f;
        [SerializeField] private float targetAngularPadding = 4f;
        [SerializeField] private float timeBeforeHeadTurn = 2f;
        [SerializeField] private float timeBeforePointing = 5f;

        [Header("Fallbacks")]
        [SerializeField] private bool autoConfigureHeadTracking = true;
        [SerializeField] private bool allowLegacyXRNodeFallback;
        [SerializeField] private bool allowCameraFallbackWithoutHeadPose;
        [SerializeField] private bool allowEditorCameraFallback = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private float diagnosticsLogInterval = 0.5f;
        [SerializeField] private float minLivePoseAngleChange = 0.15f;
        [SerializeField] private float minLivePosePositionChange = 0.002f;
        [SerializeField] private int maxVisibleEvents = 4;

        private float notLookingTimer;
        private float nextDiagnosticsLogTime;
        private float previousAngleToTarget;
        private float previousAngleSampleTime;
        private GuidanceStage currentStage = GuidanceStage.None;
        private bool wasLookingAtTarget = true;
        private bool hasPreviousAngleSample;
        private bool hasPreviousRawPoseSample;
        private Vector3 previousRawPosePosition;
        private Vector3 previousRawPoseForward;
        private string previousRawPoseSource = string.Empty;
        private float lastLivePoseChangeTime;
        private float lastRecognizedLookingTime = float.NegativeInfinity;
        private bool attemptedRuntimeHeadTrackingSetup;
        private readonly string[] recentEvents = new string[8];
        private int recentEventCount;

        private static readonly XRNode[] ViewNodes =
        {
            XRNode.CenterEye,
            XRNode.Head
        };

        private static readonly List<XRNodeState> NodeStates = new List<XRNodeState>();

        public enum GuidanceStage
        {
            None,
            HeadTurn,
            Pointing
        }

        public bool IsLookingAtTarget { get; private set; }
        public float CurrentAngleToTarget { get; private set; }
        public Vector3 CurrentTargetViewportPosition { get; private set; }
        public float CurrentViewportCenterDistance { get; private set; }
        public string CurrentRecognitionSource { get; private set; } = "None";
        public float CurrentEffectiveAttentionAngle { get; private set; }
        public float CurrentTargetAngularRadius { get; private set; }
        public float CurrentDotToTarget { get; private set; }
        public Vector3 CurrentRecognitionPosition { get; private set; }
        public Vector3 CurrentRecognitionForward { get; private set; }
        public float CurrentHorizontalOffsetToTarget { get; private set; }
        public float CurrentVerticalOffsetToTarget { get; private set; }
        public float CurrentAngleChangeSpeed { get; private set; }
        public float CurrentPoseStillTime { get; private set; }
        public bool HasTrackedHeadPose { get; private set; }
        public float NotLookingTimer => notLookingTimer;
        public GuidanceStage CurrentStage => currentStage;
        public string RecentEventsText { get; private set; } = "No events yet";

        public void Configure(Transform userCameraTransform, Transform targetTransform, VirtualPersonGuide guide)
        {
            userCamera = userCameraTransform;
            targetObject = targetTransform;
            virtualPerson = guide;
        }

        public void SetAttentionSettings(float angle, float headTurnDelay, float pointingDelay)
        {
            attentionAngle = angle;
            timeBeforeHeadTurn = headTurnDelay;
            timeBeforePointing = pointingDelay;
        }

        public void SetViewportCenterRadius(float radius)
        {
            viewportCenterRadius = radius;
        }

        public void SetRecognitionSettings(float angle, float viewportRadius, float holdTime, float angularPadding)
        {
            attentionAngle = angle;
            viewportCenterRadius = viewportRadius;
            recognitionHoldTime = holdTime;
            targetAngularPadding = angularPadding;
        }

        private void Reset()
        {
            if (Camera.main != null)
            {
                userCamera = Camera.main.transform;
            }
        }

        private void Awake()
        {
            EnsureRuntimeHeadTrackingSetup();
        }

        private void Update()
        {
            EnsureRuntimeHeadTrackingSetup();

            if (userCamera == null || targetObject == null || virtualPerson == null)
            {
                return;
            }

            IsLookingAtTarget = CalculateIsUserLookingAtTarget();
            UpdateHeadTurnResponse();
            LogAttentionDiagnostics();

            if (IsLookingAtTarget)
            {
                if (!wasLookingAtTarget)
                {
                    AddEvent("Looking restored");
                }

                wasLookingAtTarget = true;
                ResetGuidance();
                return;
            }

            if (wasLookingAtTarget)
            {
                AddEvent("Looking lost");
            }

            wasLookingAtTarget = false;

            notLookingTimer += Time.deltaTime;

            if (notLookingTimer >= timeBeforePointing)
            {
                EnterPointingStage();
            }
            else if (notLookingTimer >= timeBeforeHeadTurn)
            {
                EnterHeadTurnStage();
            }
        }

        private bool CalculateIsUserLookingAtTarget()
        {
            IsTargetNearViewportCenter();

            if (IsTargetNearAnyHeadForward())
            {
                lastRecognizedLookingTime = Time.time;
                return true;
            }

            if (Time.time - lastRecognizedLookingTime <= recognitionHoldTime)
            {
                CurrentRecognitionSource = "Hold";
                return true;
            }

            return false;
        }

        private bool IsTargetNearViewportCenter()
        {
            Camera cameraComponent = userCamera.GetComponent<Camera>();

            if (cameraComponent == null)
            {
                CurrentTargetViewportPosition = Vector3.zero;
                CurrentViewportCenterDistance = float.PositiveInfinity;
                return false;
            }

            CurrentTargetViewportPosition = cameraComponent.WorldToViewportPoint(GetTargetCenter());
            Vector2 targetViewportCenter = new Vector2(
                CurrentTargetViewportPosition.x - 0.5f,
                CurrentTargetViewportPosition.y - 0.5f
            );
            CurrentViewportCenterDistance = targetViewportCenter.magnitude;

            bool isInFront = CurrentTargetViewportPosition.z > 0f;
            bool isNearCenter = CurrentViewportCenterDistance <= viewportCenterRadius;

            return isInFront && isNearCenter;
        }

        private bool IsTargetNearAnyHeadForward()
        {
            if (TryGetInputSystemWorldViewPose(out Vector3 inputPosition, out Quaternion inputRotation, out string inputSource))
            {
                return IsTargetWithinAttentionCone(inputPosition, inputRotation * Vector3.forward, inputSource);
            }

            if (IsCameraDrivenByTrackedPoseDriver())
            {
                return IsTargetWithinAttentionCone(userCamera.position, userCamera.forward, "Tracked camera pose");
            }

            if (allowLegacyXRNodeFallback
                && TryGetLegacyWorldViewPose(out Vector3 legacyPosition, out Quaternion legacyRotation, out string legacySource))
            {
                return IsTargetWithinAttentionCone(legacyPosition, legacyRotation * Vector3.forward, legacySource);
            }

            if (ShouldUseCameraFallback())
            {
                return IsTargetWithinAttentionCone(userCamera.position, userCamera.forward, "Camera angle fallback");
            }

            SetNoTrackedHeadPoseRecognitionState();
            return false;
        }

        private bool IsCameraDrivenByTrackedPoseDriver()
        {
#if ENABLE_INPUT_SYSTEM
            return userCamera != null && userCamera.GetComponent<TrackedPoseDriver>() != null;
#else
            return false;
#endif
        }

        private void EnsureRuntimeHeadTrackingSetup()
        {
            if (attemptedRuntimeHeadTrackingSetup || !autoConfigureHeadTracking || userCamera == null)
            {
                return;
            }

            attemptedRuntimeHeadTrackingSetup = true;
            EnsureARSession();
            EnsureTrackedPoseDriver();
        }

        private static void EnsureARSession()
        {
            if (FindFirstObjectByType<ARSession>() != null)
            {
                return;
            }

            GameObject arSessionObject = new GameObject("AR Session");
            arSessionObject.AddComponent<ARSession>();
            arSessionObject.AddComponent<ARInputManager>();
        }

        private void EnsureTrackedPoseDriver()
        {
#if ENABLE_INPUT_SYSTEM
            if (!userCamera.TryGetComponent(out TrackedPoseDriver trackedPoseDriver))
            {
                trackedPoseDriver = userCamera.gameObject.AddComponent<TrackedPoseDriver>();
            }

            trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            trackedPoseDriver.ignoreTrackingState = false;
            trackedPoseDriver.positionInput = new InputActionProperty(CreateHeadPositionAction());
            trackedPoseDriver.rotationInput = new InputActionProperty(CreateHeadRotationAction());
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static InputAction CreateHeadPositionAction()
        {
            InputAction action = new InputAction("Head Position", InputActionType.Value, expectedControlType: "Vector3");
            action.AddBinding("<XRHMD>/devicePosition");
            action.AddBinding("<XRHMD>/centerEyePosition");
            action.AddBinding("<PolySpatialXRHMD>/devicePosition");
            action.AddBinding("<PolySpatialXRHMD>/centerEyePosition");
            action.AddBinding("<HandheldARInputDevice>/devicePosition");
            return action;
        }

        private static InputAction CreateHeadRotationAction()
        {
            InputAction action = new InputAction("Head Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            action.AddBinding("<XRHMD>/deviceRotation");
            action.AddBinding("<XRHMD>/centerEyeRotation");
            action.AddBinding("<PolySpatialXRHMD>/deviceRotation");
            action.AddBinding("<PolySpatialXRHMD>/centerEyeRotation");
            action.AddBinding("<HandheldARInputDevice>/deviceRotation");
            return action;
        }

#endif

        private bool ShouldUseCameraFallback()
        {
            return allowCameraFallbackWithoutHeadPose
                || (allowEditorCameraFallback && Application.isEditor);
        }

        private bool IsTargetWithinAttentionCone(Vector3 viewPosition, Vector3 viewForward, string source)
        {
            Vector3 directionToTarget = GetTargetCenter() - viewPosition;

            if (viewForward.sqrMagnitude > 0.001f)
            {
                viewForward.Normalize();
            }

            CurrentRecognitionPosition = viewPosition;
            CurrentRecognitionForward = viewForward;
            CurrentAngleToTarget = CalculateAngleFromPose(viewPosition, viewForward);
            CurrentRecognitionSource = source;
            bool isFallbackPose = source.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0;
            HasTrackedHeadPose = !isFallbackPose;

            if (HasTrackedHeadPose)
            {
                UpdatePoseStillTime(viewPosition, viewForward, source);
            }
            else
            {
                CurrentPoseStillTime = 0f;
            }

            CurrentTargetAngularRadius = CalculateTargetAngularRadius(viewPosition);
            CurrentEffectiveAttentionAngle = attentionAngle + CurrentTargetAngularRadius + targetAngularPadding;
            CurrentDotToTarget = directionToTarget.sqrMagnitude < 0.001f
                ? 1f
                : Vector3.Dot(viewForward, directionToTarget.normalized);
            UpdateTargetOffsets(directionToTarget, viewForward);

            if (!HasTrackedHeadPose && !ShouldUseCameraFallback())
            {
                CurrentRecognitionSource = $"{source} (fallback disabled)";
                return false;
            }

            return CurrentDotToTarget > 0f
                && CurrentAngleToTarget <= CurrentEffectiveAttentionAngle;
        }

        private void UpdatePoseStillTime(Vector3 viewPosition, Vector3 viewForward, string source)
        {
            if (!hasPreviousRawPoseSample || previousRawPoseSource != source)
            {
                hasPreviousRawPoseSample = true;
                previousRawPosePosition = viewPosition;
                previousRawPoseForward = viewForward;
                previousRawPoseSource = source;
                lastLivePoseChangeTime = Time.time;
                CurrentPoseStillTime = 0f;
                return;
            }

            float forwardChange = Vector3.Angle(previousRawPoseForward, viewForward);
            float positionChange = Vector3.Distance(previousRawPosePosition, viewPosition);

            if (forwardChange >= minLivePoseAngleChange || positionChange >= minLivePosePositionChange)
            {
                previousRawPosePosition = viewPosition;
                previousRawPoseForward = viewForward;
                lastLivePoseChangeTime = Time.time;
                CurrentPoseStillTime = 0f;
                return;
            }

            CurrentPoseStillTime = Time.time - lastLivePoseChangeTime;
        }

        private void SetNoTrackedHeadPoseRecognitionState()
        {
            CurrentRecognitionPosition = userCamera.position;
            CurrentRecognitionForward = userCamera.forward;
            CurrentRecognitionSource = "No tracked head pose";
            CurrentAngleToTarget = float.PositiveInfinity;
            CurrentTargetAngularRadius = CalculateTargetAngularRadius(userCamera.position);
            CurrentEffectiveAttentionAngle = attentionAngle + CurrentTargetAngularRadius + targetAngularPadding;
            CurrentDotToTarget = 0f;
            CurrentHorizontalOffsetToTarget = 0f;
            CurrentVerticalOffsetToTarget = 0f;
            HasTrackedHeadPose = false;
            CurrentPoseStillTime = 0f;
            hasPreviousRawPoseSample = false;
        }

        private void UpdateTargetOffsets(Vector3 directionToTarget, Vector3 viewForward)
        {
            if (directionToTarget.sqrMagnitude < 0.001f || viewForward.sqrMagnitude < 0.001f)
            {
                CurrentHorizontalOffsetToTarget = 0f;
                CurrentVerticalOffsetToTarget = 0f;
                return;
            }

            Quaternion viewRotation = Quaternion.LookRotation(viewForward.normalized, Vector3.up);
            Vector3 localDirectionToTarget = Quaternion.Inverse(viewRotation) * directionToTarget.normalized;

            CurrentHorizontalOffsetToTarget = Mathf.Atan2(localDirectionToTarget.x, localDirectionToTarget.z) * Mathf.Rad2Deg;
            CurrentVerticalOffsetToTarget = Mathf.Atan2(
                localDirectionToTarget.y,
                new Vector2(localDirectionToTarget.x, localDirectionToTarget.z).magnitude) * Mathf.Rad2Deg;
        }

        private void UpdateHeadTurnResponse()
        {
            if (!HasTrackedHeadPose || float.IsInfinity(CurrentAngleToTarget) || float.IsNaN(CurrentAngleToTarget))
            {
                CurrentAngleChangeSpeed = 0f;
                hasPreviousAngleSample = false;
                return;
            }

            if (!hasPreviousAngleSample)
            {
                previousAngleToTarget = CurrentAngleToTarget;
                previousAngleSampleTime = Time.time;
                CurrentAngleChangeSpeed = 0f;
                hasPreviousAngleSample = true;
                return;
            }

            float deltaTime = Time.time - previousAngleSampleTime;

            if (deltaTime > 0.001f)
            {
                CurrentAngleChangeSpeed = Mathf.Abs(CurrentAngleToTarget - previousAngleToTarget) / deltaTime;
            }

            previousAngleToTarget = CurrentAngleToTarget;
            previousAngleSampleTime = Time.time;
        }

        private float CalculateAngleFromPose(Vector3 viewPosition, Vector3 viewForward)
        {
            Vector3 directionToTarget = GetTargetCenter() - viewPosition;

            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                return 0f;
            }

            return Vector3.Angle(viewForward, directionToTarget.normalized);
        }

        private float CalculateTargetAngularRadius(Vector3 viewPosition)
        {
            Bounds bounds = GetTargetBounds();
            float distance = Vector3.Distance(viewPosition, bounds.center);

            if (distance < 0.001f)
            {
                return 180f;
            }

            float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            return Mathf.Asin(Mathf.Clamp(targetRadius / distance, 0f, 1f)) * Mathf.Rad2Deg;
        }

        private Vector3 GetTargetCenter()
        {
            return GetTargetBounds().center;
        }

        private Bounds GetTargetBounds()
        {
            Renderer renderer = targetObject.GetComponentInChildren<Renderer>();

            if (renderer != null)
            {
                return renderer.bounds;
            }

            return new Bounds(targetObject.position, Vector3.one * 0.25f);
        }

        private bool TryGetInputSystemWorldViewPose(out Vector3 worldPosition, out Quaternion worldRotation, out string source)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            source = string.Empty;

#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemPose("HMD", "devicePosition", "deviceRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose("HandheldARInputDevice", "devicePosition", "deviceRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose("AR", "devicePosition", "deviceRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose("Head", "devicePosition", "deviceRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose(string.Empty, "devicePosition", "deviceRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose("HMD", "centerEyePosition", "centerEyeRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose("Head", "centerEyePosition", "centerEyeRotation", out worldPosition, out worldRotation, out source)
                || TryGetInputSystemPose(string.Empty, "centerEyePosition", "centerEyeRotation", out worldPosition, out worldRotation, out source))
            {
                return true;
            }
#endif

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryGetInputSystemPose(
            string preferredDeviceText,
            string positionControlName,
            string rotationControlName,
            out Vector3 worldPosition,
            out Quaternion worldRotation,
            out string source)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            source = string.Empty;

            foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
            {
                if (!device.enabled || !DeviceMatches(device, preferredDeviceText))
                {
                    continue;
                }

                ButtonControl isTrackedControl = device.TryGetChildControl("isTracked") as ButtonControl;

                if (isTrackedControl != null && !isTrackedControl.isPressed)
                {
                    continue;
                }

                Vector3Control positionControl = device.TryGetChildControl(positionControlName) as Vector3Control;
                QuaternionControl rotationControl = device.TryGetChildControl(rotationControlName) as QuaternionControl;

                if (positionControl == null || rotationControl == null)
                {
                    continue;
                }

                Quaternion localRotation = rotationControl.ReadValue();

                if (!IsValidRotation(localRotation))
                {
                    continue;
                }

                ConvertTrackingPoseToWorld(positionControl.ReadValue(), localRotation, out worldPosition, out worldRotation);
                source = $"{device.displayName} {rotationControlName}";
                return true;
            }

            return false;
        }

        private static bool DeviceMatches(UnityEngine.InputSystem.InputDevice device, string preferredDeviceText)
        {
            if (string.IsNullOrEmpty(preferredDeviceText))
            {
                return true;
            }

            return ContainsIgnoreCase(device.layout, preferredDeviceText)
                || ContainsIgnoreCase(device.name, preferredDeviceText)
                || ContainsIgnoreCase(device.displayName, preferredDeviceText)
                || ContainsIgnoreCase(device.description.product, preferredDeviceText)
                || ContainsIgnoreCase(device.description.interfaceName, preferredDeviceText);
        }

        private static bool ContainsIgnoreCase(string value, string expectedText)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif

        private bool TryGetLegacyWorldViewPose(out Vector3 worldPosition, out Quaternion worldRotation, out string source)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            source = string.Empty;

            Vector3 bestPosition = Vector3.zero;
            Quaternion bestRotation = Quaternion.identity;
            float bestAngle = float.PositiveInfinity;
            string bestSource = "XR angle";
            bool foundTrackedPose = false;

            foreach (XRNode node in ViewNodes)
            {
                if (!TryGetLegacyWorldViewPose(node, out Vector3 position, out Quaternion rotation))
                {
                    continue;
                }

                float angle = CalculateAngleFromPose(position, rotation * Vector3.forward);

                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    bestPosition = position;
                    bestRotation = rotation;
                    bestSource = $"{node} angle";
                    foundTrackedPose = true;
                }
            }

            if (!foundTrackedPose)
            {
                return false;
            }

            worldPosition = bestPosition;
            worldRotation = bestRotation;
            source = bestSource;
            return true;
        }

        private bool TryGetLegacyWorldViewPose(XRNode node, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;

            NodeStates.Clear();
            InputTracking.GetNodeStates(NodeStates);

            XRNodeState nodeState = default;
            bool foundNode = false;

            foreach (XRNodeState state in NodeStates)
            {
                if (state.nodeType != node)
                {
                    continue;
                }

                nodeState = state;
                foundNode = true;
                break;
            }

            if (!foundNode ||
                !nodeState.TryGetPosition(out Vector3 localPosition) ||
                !nodeState.TryGetRotation(out Quaternion localRotation))
            {
                return false;
            }

            if (!IsValidRotation(localRotation))
            {
                return false;
            }

            ConvertTrackingPoseToWorld(localPosition, localRotation, out worldPosition, out worldRotation);

            return true;
        }

        private void ConvertTrackingPoseToWorld(Vector3 localPosition, Quaternion localRotation, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            Transform trackingRoot = userCamera.parent;

            if (trackingRoot != null)
            {
                worldPosition = trackingRoot.TransformPoint(localPosition);
                worldRotation = trackingRoot.rotation * localRotation;
                return;
            }

            worldPosition = userCamera.position;
            worldRotation = localRotation;
        }

        private static bool IsValidRotation(Quaternion rotation)
        {
            float magnitude = rotation.x * rotation.x
                + rotation.y * rotation.y
                + rotation.z * rotation.z
                + rotation.w * rotation.w;

            return magnitude > 0.5f && magnitude < 1.5f;
        }

        private void LogAttentionDiagnostics()
        {
            if (!showDebugLogs || diagnosticsLogInterval <= 0f || Time.time < nextDiagnosticsLogTime)
            {
                return;
            }

            nextDiagnosticsLogTime = Time.time + diagnosticsLogInterval;

            Debug.Log(
                $"Attention diagnostics | looking={IsLookingAtTarget} | source={CurrentRecognitionSource} | " +
                $"angle={CurrentAngleToTarget:F1} deg | threshold={CurrentEffectiveAttentionAngle:F1} deg " +
                $"(base={attentionAngle:F1}, target={CurrentTargetAngularRadius:F1}, padding={targetAngularPadding:F1}) | " +
                $"offsetH={CurrentHorizontalOffsetToTarget:F1} deg | offsetV={CurrentVerticalOffsetToTarget:F1} deg | " +
                $"angleSpeed={CurrentAngleChangeSpeed:F1} deg/s | poseStill={CurrentPoseStillTime:F1}s | dot={CurrentDotToTarget:F3} | viewport={CurrentViewportCenterDistance:F2} z={CurrentTargetViewportPosition.z:F2} | " +
                $"posePos={FormatVector(CurrentRecognitionPosition)} | poseForward={FormatVector(CurrentRecognitionForward)} | " +
                $"target={FormatVector(GetTargetCenter())} | devices={BuildPoseDeviceSummary()}");
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        private string BuildPoseDeviceSummary()
        {
#if ENABLE_INPUT_SYSTEM
            List<string> summaries = new List<string>();

            foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
            {
                bool hasDevicePose = device.TryGetChildControl("devicePosition") != null
                    && device.TryGetChildControl("deviceRotation") != null;
                bool hasCenterEyePose = device.TryGetChildControl("centerEyePosition") != null
                    && device.TryGetChildControl("centerEyeRotation") != null;

                if (!hasDevicePose && !hasCenterEyePose)
                {
                    continue;
                }

                ButtonControl isTrackedControl = device.TryGetChildControl("isTracked") as ButtonControl;
                string trackedText = isTrackedControl == null ? "tracked=unknown" : $"tracked={isTrackedControl.isPressed}";
                string poseText = hasDevicePose && hasCenterEyePose
                    ? "device+centerEye"
                    : hasDevicePose ? "device" : "centerEye";
                string forwardText = GetInputDeviceForwardSummary(device, hasCenterEyePose, hasDevicePose);

                summaries.Add($"{device.layout}/{device.displayName}:{poseText},{trackedText},enabled={device.enabled},{forwardText}");
            }

            return summaries.Count > 0 ? string.Join("; ", summaries) : "no Input System pose devices";
#else
            return "Input System disabled";
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private string GetInputDeviceForwardSummary(UnityEngine.InputSystem.InputDevice device, bool hasCenterEyePose, bool hasDevicePose)
        {
            List<string> forwards = new List<string>();

            if (hasCenterEyePose)
            {
                forwards.Add(GetInputDeviceForwardSummary(device, "centerEyeRotation", "centerForward"));
            }

            if (hasDevicePose)
            {
                forwards.Add(GetInputDeviceForwardSummary(device, "deviceRotation", "deviceForward"));
            }

            return forwards.Count > 0 ? string.Join(",", forwards) : "forward=unavailable";
        }

        private string GetInputDeviceForwardSummary(UnityEngine.InputSystem.InputDevice device, string rotationControlName, string label)
        {
            QuaternionControl rotationControl = device.TryGetChildControl(rotationControlName) as QuaternionControl;

            if (rotationControl == null)
            {
                return $"{label}=unavailable";
            }

            Quaternion rotation = rotationControl.ReadValue();

            if (!IsValidRotation(rotation))
            {
                return $"{label}=invalid";
            }

            return $"{label}={FormatVector(rotation * Vector3.forward)}";
        }
#endif

        private void EnterHeadTurnStage()
        {
            if (currentStage == GuidanceStage.HeadTurn)
            {
                return;
            }

            currentStage = GuidanceStage.HeadTurn;

            if (showDebugLogs)
            {
                Debug.Log("Stage 1: virtual person looks at target.");
            }

            AddEvent("Head turn started");
            virtualPerson.LookAtTarget(targetObject);
        }

        private void EnterPointingStage()
        {
            if (currentStage == GuidanceStage.Pointing)
            {
                return;
            }

            currentStage = GuidanceStage.Pointing;

            if (showDebugLogs)
            {
                Debug.Log("Stage 2: virtual person points at target.");
            }

            AddEvent("Pointing started");
            virtualPerson.PointAtTarget(targetObject);
        }

        private void ResetGuidance()
        {
            if (currentStage != GuidanceStage.None && showDebugLogs)
            {
                Debug.Log("User is looking at target. Return to idle.");
            }

            notLookingTimer = 0f;
            currentStage = GuidanceStage.None;
            virtualPerson.ReturnToIdle();
        }

        private void AddEvent(string eventText)
        {
            string message =
                $"{Time.time:F1}s | {eventText} | " +
                $"angle {CurrentAngleToTarget:F1} deg | " +
                $"viewport {CurrentViewportCenterDistance:F2} | " +
                CurrentRecognitionSource;

            if (showDebugLogs)
            {
                Debug.Log(message);
            }

            int index = recentEventCount % recentEvents.Length;
            recentEvents[index] = message;
            recentEventCount++;
            RebuildRecentEventsText();
        }

        private void RebuildRecentEventsText()
        {
            int availableEvents = Mathf.Min(recentEventCount, recentEvents.Length);
            int visibleEvents = Mathf.Min(availableEvents, maxVisibleEvents);

            if (visibleEvents == 0)
            {
                RecentEventsText = "No events yet";
                return;
            }

            RecentEventsText = string.Empty;

            for (int i = visibleEvents - 1; i >= 0; i--)
            {
                int eventIndex = recentEventCount - 1 - i;
                string eventLine = recentEvents[eventIndex % recentEvents.Length];

                if (!string.IsNullOrEmpty(RecentEventsText))
                {
                    RecentEventsText += "\n";
                }

                RecentEventsText += eventLine;
            }
        }
    }
}
