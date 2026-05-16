using TMPro;
using UnityEngine;

namespace VisionProAttentionGuide
{
    public class AttentionStatusDisplay : MonoBehaviour
    {
        [SerializeField] private AttentionGuidanceManager attentionManager;
        [SerializeField] private Transform userCamera;
        [SerializeField] private TextMeshPro statusText;
        [SerializeField] private TextMesh fallbackStatusText;
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private bool followCameraHud = true;
        [SerializeField] private Vector3 hudLocalPosition = new Vector3(-0.42f, 0.02f, 1.2f);
        [SerializeField] private Vector3 hudLocalScale = Vector3.one * 0.038f;

        public void Configure(AttentionGuidanceManager manager, Transform cameraTransform, TextMeshPro text)
        {
            attentionManager = manager;
            userCamera = cameraTransform;
            statusText = text;
        }

        private void Reset()
        {
            statusText = GetComponent<TextMeshPro>();

            if (Camera.main != null)
            {
                userCamera = Camera.main.transform;
            }
        }

        private void Awake()
        {
            EnsureFallbackText();
        }

        private void Update()
        {
            if (attentionManager == null)
            {
                return;
            }

            EnsureFallbackText();

            string lookingText = attentionManager.IsLookingAtTarget ? "YES" : "NO";
            string stageText = GetStageText(attentionManager.CurrentStage);
            string lookAngleText = FormatAngle(attentionManager.CurrentAngleToTarget);
            string thresholdText = FormatAngle(attentionManager.CurrentEffectiveAttentionAngle);
            string headPoseText = attentionManager.HasTrackedHeadPose ? "TRACKED" : "STALE / NO LIVE POSE";
            string turnResponseText = attentionManager.CurrentAngleChangeSpeed >= 5f ? "CHANGING" : "stable";
            string horizontalText = FormatSignedAngle(attentionManager.CurrentHorizontalOffsetToTarget, "left", "right");
            string verticalText = FormatSignedAngle(attentionManager.CurrentVerticalOffsetToTarget, "down", "up");

            string displayText =
                $"{AttentionGuidanceManager.BuildLabel}\n" +
                $"Megan state: {stageText} | user looking: {lookingText} | timer {attentionManager.NotLookingTimer:F1}s\n" +
                $"Ball: angle {lookAngleText}/{thresholdText} | offset {horizontalText}, {verticalText}\n" +
                $"Head pose: {headPoseText} | response {turnResponseText} {attentionManager.CurrentAngleChangeSpeed:F1} deg/s\n" +
                $"Recognition: {attentionManager.CurrentRecognitionSource} | viewport {attentionManager.CurrentViewportCenterDistance:F2}\n" +
                $"State log:\n{attentionManager.RecentEventsText}";

            if (statusText != null)
            {
                statusText.text = displayText;
            }

            if (fallbackStatusText != null)
            {
                fallbackStatusText.text = displayText;
            }

            if (followCameraHud && userCamera != null)
            {
                transform.position = userCamera.TransformPoint(hudLocalPosition);
                transform.localScale = hudLocalScale;
            }

            if (faceCamera && userCamera != null)
            {
                transform.rotation = userCamera.rotation;
            }
        }

        private void EnsureFallbackText()
        {
            if (fallbackStatusText != null)
            {
                return;
            }

            Transform existing = transform.Find("VisionOSStatusFallbackText");
            if (existing != null && existing.TryGetComponent(out fallbackStatusText))
            {
                return;
            }

            GameObject fallbackObject = new GameObject("VisionOSStatusFallbackText");
            fallbackObject.transform.SetParent(transform, false);
            fallbackObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fallbackObject.transform.localRotation = Quaternion.identity;
            fallbackObject.transform.localScale = Vector3.one;

            fallbackStatusText = fallbackObject.AddComponent<TextMesh>();
            fallbackStatusText.anchor = TextAnchor.UpperLeft;
            fallbackStatusText.alignment = TextAlignment.Left;
            fallbackStatusText.characterSize = 0.11f;
            fallbackStatusText.fontSize = 64;
            fallbackStatusText.color = new Color(1f, 0.95f, 0.1f, 1f);
        }

        private static string FormatAngle(float angle)
        {
            return float.IsInfinity(angle) || float.IsNaN(angle)
                ? "no tracked pose"
                : $"{angle:F1} deg";
        }

        private static string FormatSignedAngle(float angle, string negativeLabel, string positiveLabel)
        {
            string direction = angle < 0f ? negativeLabel : positiveLabel;
            return $"{Mathf.Abs(angle):F1} deg {direction}";
        }

        private static string GetStageText(AttentionGuidanceManager.GuidanceStage stage)
        {
            switch (stage)
            {
                case AttentionGuidanceManager.GuidanceStage.HeadTurn:
                    return "Head turn";
                case AttentionGuidanceManager.GuidanceStage.Pointing:
                    return "Pointing";
                default:
                    return "Idle";
            }
        }
    }
}
