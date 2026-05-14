using TMPro;
using UnityEngine;

namespace VisionProAttentionGuide
{
    public class AttentionStatusDisplay : MonoBehaviour
    {
        [SerializeField] private AttentionGuidanceManager attentionManager;
        [SerializeField] private Transform userCamera;
        [SerializeField] private TextMeshPro statusText;
        [SerializeField] private bool faceCamera = true;

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

        private void Update()
        {
            if (attentionManager == null || statusText == null)
            {
                return;
            }

            string lookingText = attentionManager.IsLookingAtTarget ? "YES" : "NO";
            string stageText = GetStageText(attentionManager.CurrentStage);
            string lookAngleText = FormatAngle(attentionManager.CurrentAngleToTarget);
            string thresholdText = FormatAngle(attentionManager.CurrentEffectiveAttentionAngle);
            string headPoseText = attentionManager.HasTrackedHeadPose ? "TRACKED" : "STALE / NO LIVE POSE";
            string turnResponseText = attentionManager.CurrentAngleChangeSpeed >= 5f ? "CHANGING" : "stable";
            string horizontalText = FormatSignedAngle(attentionManager.CurrentHorizontalOffsetToTarget, "left", "right");
            string verticalText = FormatSignedAngle(attentionManager.CurrentVerticalOffsetToTarget, "down", "up");

            statusText.text =
                $"{AttentionGuidanceManager.BuildLabel}\n" +
                $"Look-to-ball angle: {lookAngleText} / {thresholdText}\n" +
                $"Head pose: {headPoseText} | Still: {attentionManager.CurrentPoseStillTime:F1}s\n" +
                $"Turn response: {turnResponseText} ({attentionManager.CurrentAngleChangeSpeed:F1} deg/s)\n" +
                $"Ball offset: {horizontalText}, {verticalText}\n" +
                $"Looking at target: {lookingText}\n" +
                $"Viewport distance: {attentionManager.CurrentViewportCenterDistance:F2}\n" +
                $"Recognition: {attentionManager.CurrentRecognitionSource}\n" +
                $"Stage: {stageText}\n" +
                $"Timer: {attentionManager.NotLookingTimer:F1}s\n\n" +
                $"Events:\n{attentionManager.RecentEventsText}";

            if (faceCamera && userCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - userCamera.position, Vector3.up);
            }
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
