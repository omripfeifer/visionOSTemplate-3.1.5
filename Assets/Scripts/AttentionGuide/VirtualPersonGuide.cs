using UnityEngine;

namespace VisionProAttentionGuide
{
    public class VirtualPersonGuide : MonoBehaviour
    {
        [Header("Primitive Character References")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform pointingArm;

        [Header("Optional Character References")]
        [SerializeField] private Animator animator;
        [SerializeField] private bool autoBindAvatarBones = true;

        [Header("Look Settings")]
        [SerializeField] private float headTurnSpeed = 4f;
        [SerializeField] private float armTurnSpeed = 5f;

        [Header("Visual Debug")]
        [SerializeField] private bool showHeadDirectionIndicator;
        [SerializeField] private Transform headDirectionIndicator;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private Transform currentTarget;
        private bool shouldLookAtTarget;
        private bool shouldPointAtTarget;
        private Quaternion initialHeadRotation;
        private Quaternion initialArmRotation;
        private bool wasGuiding;

        public void Configure(Transform headTransform, Transform pointingArmTransform, Animator characterAnimator = null)
        {
            head = headTransform;
            pointingArm = pointingArmTransform;
            animator = characterAnimator;
            AutoBindMissingAvatarReferences();
            EnsureHeadDirectionIndicator();
            CacheInitialRotations();
        }

        public void SetHeadDirectionIndicatorVisible(bool isVisible)
        {
            showHeadDirectionIndicator = isVisible;
            EnsureHeadDirectionIndicator();
        }

        private void Awake()
        {
            AutoBindMissingAvatarReferences();
            EnsureHeadDirectionIndicator();
            CacheInitialRotations();
        }

        private void CacheInitialRotations()
        {
            if (head != null)
            {
                initialHeadRotation = head.rotation;
            }

            if (pointingArm != null)
            {
                initialArmRotation = pointingArm.rotation;
            }
        }

        private void LateUpdate()
        {
            if (shouldLookAtTarget && head != null && currentTarget != null)
            {
                RotateTransformTowardTarget(head, headTurnSpeed);
            }

            if (shouldPointAtTarget && pointingArm != null && currentTarget != null)
            {
                RotateTransformTowardTarget(pointingArm, armTurnSpeed);
            }
        }

        public void LookAtTarget(Transform target)
        {
            currentTarget = target;
            shouldLookAtTarget = true;
            shouldPointAtTarget = false;
            wasGuiding = true;

            SetPointingAnimation(false);

            if (showDebugLogs)
            {
                Debug.Log("Virtual person: looking at target.");
            }
        }

        public void PointAtTarget(Transform target)
        {
            currentTarget = target;
            shouldLookAtTarget = true;
            shouldPointAtTarget = true;
            wasGuiding = true;

            SetPointingAnimation(true);

            if (showDebugLogs)
            {
                Debug.Log("Virtual person: pointing at target.");
            }
        }

        public void ReturnToIdle()
        {
            currentTarget = null;
            shouldLookAtTarget = false;
            shouldPointAtTarget = false;

            if (head != null)
            {
                head.rotation = Quaternion.Slerp(head.rotation, initialHeadRotation, Time.deltaTime * headTurnSpeed);
            }

            if (pointingArm != null)
            {
                pointingArm.rotation = Quaternion.Slerp(pointingArm.rotation, initialArmRotation, Time.deltaTime * armTurnSpeed);
            }

            SetPointingAnimation(false);

            if (wasGuiding && showDebugLogs)
            {
                Debug.Log("Virtual person: idle.");
            }

            wasGuiding = false;
        }

        private void RotateTransformTowardTarget(Transform source, float turnSpeed)
        {
            Vector3 directionToTarget = currentTarget.position - source.position;

            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);
            source.rotation = Quaternion.Slerp(source.rotation, targetRotation, Time.deltaTime * turnSpeed);
        }

        private void EnsureHeadDirectionIndicator()
        {
            if (!showHeadDirectionIndicator && headDirectionIndicator != null)
            {
                headDirectionIndicator.gameObject.SetActive(false);
                return;
            }

            if (!showHeadDirectionIndicator || head == null || headDirectionIndicator != null)
            {
                return;
            }

            Transform existingIndicator = head.Find("HeadDirectionIndicator");

            if (existingIndicator != null)
            {
                headDirectionIndicator = existingIndicator;
                return;
            }

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "HeadDirectionIndicator";
            indicator.transform.SetParent(head, false);
            indicator.transform.localPosition = new Vector3(0f, 0f, 0.85f);
            indicator.transform.localRotation = Quaternion.identity;
            indicator.transform.localScale = new Vector3(0.16f, 0.16f, 0.55f);

            if (indicator.TryGetComponent(out Collider indicatorCollider))
            {
                indicatorCollider.enabled = false;
            }

            if (indicator.TryGetComponent(out Renderer renderer))
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.color = new Color(0.05f, 0.05f, 0.05f);
                renderer.sharedMaterial = material;
            }

            headDirectionIndicator = indicator.transform;
        }

        private void AutoBindMissingAvatarReferences()
        {
            if (!autoBindAvatarBones)
            {
                return;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null && animator.isHuman)
            {
                if (head == null)
                {
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                }

                if (pointingArm == null)
                {
                    pointingArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                }
            }

            if (head == null)
            {
                head = FindDescendantByName("head");
            }

            if (pointingArm == null)
            {
                pointingArm = FindDescendantByName("rightarm")
                    ?? FindDescendantByName("right_arm")
                    ?? FindDescendantByName("rightupperarm")
                    ?? FindDescendantByName("mixamorig:rightarm")
                    ?? FindDescendantByName("mixamorig:rightforearm");
            }
        }

        private Transform FindDescendantByName(string expectedName)
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);

            foreach (Transform descendant in descendants)
            {
                string normalizedName = descendant.name.Replace(" ", string.Empty).Replace("_", string.Empty);
                string normalizedExpectedName = expectedName.Replace(" ", string.Empty).Replace("_", string.Empty);

                if (normalizedName.IndexOf(normalizedExpectedName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return descendant;
                }
            }

            return null;
        }

        private void SetPointingAnimation(bool isPointing)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool("IsPointing", isPointing);
        }
    }
}
