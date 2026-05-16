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
        [SerializeField] private bool styleMixamoCharacter;
        [SerializeField] private float desiredCharacterHeightMeters = 1.75f;

        [Header("Look Settings")]
        [SerializeField] private float headTurnSpeed = 4f;
        [SerializeField] private float armTurnSpeed = 5f;

        [Header("Visual Debug")]
        [SerializeField] private bool showHeadDirectionIndicator = true;
        [SerializeField] private Transform headDirectionIndicator;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private Transform currentTarget;
        private bool shouldLookAtTarget;
        private bool shouldPointAtTarget;
        private Quaternion initialHeadRotation;
        private Quaternion initialArmRotation;
        private bool wasGuiding;
        private bool appliedCharacterSetup;
        private static readonly int IsLookingHash = Animator.StringToHash("IsLooking");
        private static readonly int IsPointingHash = Animator.StringToHash("IsPointing");

        public void Configure(Transform headTransform, Transform pointingArmTransform, Animator characterAnimator = null, bool showDirectionIndicator = true)
        {
            head = headTransform;
            pointingArm = pointingArmTransform;
            animator = characterAnimator;
            showHeadDirectionIndicator = showDirectionIndicator;
            EnsureHeadDirectionIndicator();

            if (head != null)
            {
                initialHeadRotation = head.rotation;
            }

            if (pointingArm != null)
            {
                initialArmRotation = pointingArm.rotation;
            }
        }

        private void Start()
        {
            ApplyCharacterSetup();
        }

        private void Awake()
        {
            EnsureHeadDirectionIndicator();

            if (head != null)
            {
                initialHeadRotation = head.rotation;
            }

            if (pointingArm != null)
            {
                initialArmRotation = pointingArm.rotation;
            }
        }

        private void ApplyCharacterSetup()
        {
            if (appliedCharacterSetup || animator == null)
            {
                return;
            }

            appliedCharacterSetup = true;

            if (styleMixamoCharacter)
            {
                ApplyMixamoMaterials();
            }

            if (desiredCharacterHeightMeters > 0.1f)
            {
                ResizeCharacterToHeight(desiredCharacterHeightMeters);
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

            SetLookingAnimation(true);
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

            SetLookingAnimation(true);
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
            SetLookingAnimation(false);

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

        private void SetPointingAnimation(bool isPointing)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(IsPointingHash, isPointing);
        }

        private void SetLookingAnimation(bool isLooking)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(IsLookingHash, isLooking);
        }

        private void ApplyMixamoMaterials()
        {
#if !UNITY_EDITOR
            return;
#else
            Material skin = CreateRuntimeMaterial(new Color(0.86f, 0.58f, 0.43f), 0.28f);
            Material hair = CreateRuntimeMaterial(new Color(0.18f, 0.11f, 0.07f), 0.35f);
            Material shirt = CreateRuntimeMaterial(new Color(0.78f, 0.88f, 0.96f), 0.22f);
            Material jeans = CreateRuntimeMaterial(new Color(0.08f, 0.17f, 0.32f), 0.38f);
            Material shoes = CreateRuntimeMaterial(new Color(0.82f, 0.82f, 0.78f), 0.45f);
            Material neutral = CreateRuntimeMaterial(new Color(0.38f, 0.37f, 0.35f), 0.4f);

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = SelectMixamoMaterial(renderer, materials[i], skin, hair, shirt, jeans, shoes, neutral);
                }

                renderer.sharedMaterials = materials;
            }
#endif
        }

        private static Material SelectMixamoMaterial(
            Renderer renderer,
            Material sourceMaterial,
            Material skin,
            Material hair,
            Material shirt,
            Material jeans,
            Material shoes,
            Material neutral)
        {
            string sourceName = sourceMaterial != null ? sourceMaterial.name : string.Empty;
            string key = $"{renderer.name} {renderer.gameObject.name} {sourceName}".ToLowerInvariant();

            if (key.Contains("hair"))
            {
                return hair;
            }

            if (key.Contains("shirt") || key.Contains("top"))
            {
                return shirt;
            }

            if (key.Contains("pant") || key.Contains("jean"))
            {
                return jeans;
            }

            if (key.Contains("shoe") || key.Contains("sneaker"))
            {
                return shoes;
            }

            if (key.Contains("body") || key.Contains("skin") || key.Contains("head") || key.Contains("hand") || key.Contains("face"))
            {
                return skin;
            }

            return neutral;
        }

        private static Material CreateRuntimeMaterial(Color color, float smoothness)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            return material;
        }

        private void ResizeCharacterToHeight(float targetHeightMeters)
        {
            if (!TryGetRendererBounds(out Bounds bounds) || bounds.size.y <= 0.01f)
            {
                return;
            }

            float desiredFeetY = transform.position.y;
            float scaleMultiplier = targetHeightMeters / bounds.size.y;
            transform.localScale *= scaleMultiplier;

            if (TryGetRendererBounds(out Bounds scaledBounds))
            {
                Vector3 position = transform.position;
                position.y += desiredFeetY - scaledBounds.min.y;
                transform.position = position;
            }
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }
}
