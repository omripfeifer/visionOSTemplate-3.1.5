using System.IO;
using TMPro;
using Unity.PolySpatial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisionProAttentionGuide;

namespace VisionProAttentionGuide.Editor
{
    public static class AttentionGuideSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/AttentionGuideMVP.unity";
        private const string MaterialsFolder = "Assets/Materials";
        private const string AvatarAssetPath = "Assets/Avatar/Megan.fbx";
        private const float AvatarTargetHeight = 1.75f;

        [MenuItem("Vision Pro Assignment/Create Attention Guide MVP Scene")]
        public static void CreateAttentionGuideScene()
        {
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AttentionGuideMVP";

            Material targetMaterial = CreateMaterial("M_TargetBlue", new Color(0.1f, 0.45f, 1f));
            Material bodyMaterial = CreateMaterial("M_PersonBody", new Color(0.85f, 0.72f, 0.35f));
            Material headMaterial = CreateMaterial("M_PersonHead", new Color(0.95f, 0.72f, 0.52f));
            Material armMaterial = CreateMaterial("M_PersonArm", new Color(0.9f, 0.62f, 0.38f));

            Camera camera = CreateMainCamera();
            CreateLight();
            CreateVolumeCamera();

            Transform target = CreateTarget(targetMaterial);
            VirtualPersonGuide guide = CreateVirtualPerson(bodyMaterial, headMaterial, armMaterial);
            AttentionGuidanceManager manager = CreateAttentionSystem(camera.transform, target, guide);
            CreateStatusDisplay(manager, camera.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"Created {ScenePath} and set it as the only enabled build scene.");
        }

        [MenuItem("Vision Pro Assignment/Apply Device Demo Layout")]
        public static void ApplyDeviceDemoLayout()
        {
            GameObject floor = GameObject.Find("Floor");
            GameObject target = GameObject.Find("TargetObject");
            GameObject person = GameObject.Find("VirtualPerson");
            GameObject statusDisplay = GameObject.Find("AttentionStatusDisplay");
            Camera camera = Camera.main;
            VolumeCamera volumeCamera = Object.FindFirstObjectByType<VolumeCamera>();
            AttentionGuidanceManager manager = Object.FindFirstObjectByType<AttentionGuidanceManager>();

            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.6f, 0f);
                camera.transform.rotation = Quaternion.identity;
            }

            if (target != null)
            {
                target.transform.position = new Vector3(1.75f, 1.45f, 10.6f);
                target.transform.localScale = Vector3.one * 0.65f;
            }

            if (person != null)
            {
                person.transform.position = new Vector3(-1.75f, 0f, 9.7f);
                person.transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            }

            if (statusDisplay != null)
            {
                statusDisplay.transform.position = new Vector3(0f, 2.35f, 3.15f);
                statusDisplay.transform.localScale = Vector3.one * 0.06f;

                if (statusDisplay.TryGetComponent(out TextMeshPro text))
                {
                    text.fontSize = 3.4f;
                }
            }

            if (floor != null)
            {
                Object.DestroyImmediate(floor);
            }

            if (volumeCamera != null)
            {
                volumeCamera.transform.position = new Vector3(0f, 1.5f, 6f);
                volumeCamera.Dimensions = new Vector3(8f, 5f, 14f);
            }

            if (manager != null)
            {
                manager.SetAttentionSettings(12f, 0f, 3f);
                manager.SetRecognitionSettings(12f, 0.24f, 0.35f, 4f);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Applied device demo layout: farther objects, tighter cluster, stricter attention angle.");
        }

        [MenuItem("Vision Pro Assignment/Add Status Display To Current Scene")]
        public static void AddStatusDisplayToCurrentScene()
        {
            AttentionGuidanceManager manager = Object.FindFirstObjectByType<AttentionGuidanceManager>();
            Camera camera = Camera.main;

            if (manager == null)
            {
                Debug.LogError("Could not find AttentionGuidanceManager in the current scene.");
                return;
            }

            if (camera == null)
            {
                Debug.LogError("Could not find a Main Camera in the current scene.");
                return;
            }

            CreateStatusDisplay(manager, camera.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Added AttentionStatusDisplay to the current scene.");
        }

        [MenuItem("Vision Pro Assignment/Replace Virtual Person With Mixamo Avatar")]
        public static void ReplaceVirtualPersonWithMixamoAvatar()
        {
            VirtualPersonGuide guide = CreateMixamoAvatarPerson();

            if (guide == null)
            {
                Debug.LogError($"Could not load avatar at {AvatarAssetPath}. Put the Mixamo FBX there and let Unity import it first.");
                return;
            }

            AttentionGuidanceManager manager = Object.FindFirstObjectByType<AttentionGuidanceManager>();
            Transform target = GameObject.Find("TargetObject")?.transform;
            Camera camera = Camera.main;

            if (manager != null && target != null && camera != null)
            {
                manager.Configure(camera.transform, target, guide);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Replaced primitive virtual person with {AvatarAssetPath}.");
        }

        private static Camera CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.6f, 0f);
            cameraObject.transform.rotation = Quaternion.identity;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            AudioListener audioListener = cameraObject.AddComponent<AudioListener>();
            audioListener.enabled = true;

            return camera;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
        }

        private static void CreateVolumeCamera()
        {
            GameObject volumeCameraObject = new GameObject("Volume Camera");
            volumeCameraObject.transform.position = new Vector3(0f, 1.5f, 6f);

            VolumeCamera volumeCamera = volumeCameraObject.AddComponent<VolumeCamera>();
            volumeCamera.Dimensions = new Vector3(8f, 5f, 14f);

            VolumeCameraWindowConfiguration configuration =
                AssetDatabase.LoadAssetAtPath<VolumeCameraWindowConfiguration>("Assets/Resources/VolumeCamera_Unbounded.asset")
                ?? AssetDatabase.LoadAssetAtPath<VolumeCameraWindowConfiguration>("Assets/Resources/VolumeCamera_Bounded.asset");

            if (configuration != null)
            {
                volumeCamera.WindowConfiguration = configuration;
            }
        }

        private static Transform CreateTarget(Material material)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "TargetObject";
            target.transform.position = new Vector3(1.75f, 1.45f, 10.6f);
            target.transform.localScale = Vector3.one * 0.65f;
            SetMaterial(target, material);

            return target.transform;
        }

        private static VirtualPersonGuide CreateVirtualPerson(Material bodyMaterial, Material headMaterial, Material armMaterial)
        {
            VirtualPersonGuide avatarGuide = CreateMixamoAvatarPerson();

            if (avatarGuide != null)
            {
                return avatarGuide;
            }

            GameObject person = new GameObject("VirtualPerson");
            person.transform.position = new Vector3(-1.75f, 0f, 9.7f);
            person.transform.rotation = Quaternion.Euler(0f, 12f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(person.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.8f, 0.45f);
            SetMaterial(body, bodyMaterial);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(person.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            head.transform.localScale = Vector3.one * 0.38f;
            SetMaterial(head, headMaterial);

            GameObject leftArm = CreateArm("LeftArm", new Vector3(-0.45f, 1.25f, 0f), Quaternion.Euler(0f, 0f, 25f), armMaterial);
            leftArm.transform.SetParent(person.transform, false);

            GameObject rightArm = CreateArm("RightArm_PointingArm", new Vector3(0.45f, 1.25f, 0f), Quaternion.Euler(0f, 0f, -25f), armMaterial);
            rightArm.transform.SetParent(person.transform, false);

            VirtualPersonGuide guide = person.AddComponent<VirtualPersonGuide>();
            guide.Configure(head.transform, rightArm.transform);
            guide.SetHeadDirectionIndicatorVisible(true);

            return guide;
        }

        private static VirtualPersonGuide CreateMixamoAvatarPerson()
        {
            GameObject avatarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarAssetPath);

            if (avatarAsset == null)
            {
                return null;
            }

            GameObject existingPerson = GameObject.Find("VirtualPerson");

            if (existingPerson != null)
            {
                Object.DestroyImmediate(existingPerson);
            }

            GameObject person = PrefabUtility.InstantiatePrefab(avatarAsset) as GameObject;

            if (person == null)
            {
                person = Object.Instantiate(avatarAsset);
            }

            person.name = "VirtualPerson";
            person.transform.position = new Vector3(-1.75f, 0f, 9.7f);
            person.transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            NormalizeAvatarHeight(person);

            Animator animator = person.GetComponentInChildren<Animator>();
            VirtualPersonGuide guide = person.GetComponent<VirtualPersonGuide>() ?? person.AddComponent<VirtualPersonGuide>();
            guide.Configure(null, null, animator);
            guide.SetHeadDirectionIndicatorVisible(false);

            return guide;
        }

        private static void NormalizeAvatarHeight(GameObject avatar)
        {
            Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y <= 0.001f)
            {
                return;
            }

            float scaleMultiplier = AvatarTargetHeight / bounds.size.y;
            avatar.transform.localScale *= scaleMultiplier;
        }

        private static GameObject CreateArm(string name, Vector3 localPosition, Quaternion localRotation, Material material)
        {
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = name;
            arm.transform.localPosition = localPosition;
            arm.transform.localRotation = localRotation;
            arm.transform.localScale = new Vector3(0.16f, 0.16f, 0.75f);
            SetMaterial(arm, material);

            return arm;
        }

        private static AttentionGuidanceManager CreateAttentionSystem(Transform cameraTransform, Transform target, VirtualPersonGuide guide)
        {
            GameObject attentionSystem = new GameObject("AttentionSystem");
            AttentionGuidanceManager manager = attentionSystem.AddComponent<AttentionGuidanceManager>();
            manager.Configure(cameraTransform, target, guide);
            manager.SetAttentionSettings(12f, 0f, 3f);
            manager.SetRecognitionSettings(12f, 0.24f, 0.35f, 4f);

            return manager;
        }

        private static void CreateStatusDisplay(AttentionGuidanceManager manager, Transform cameraTransform)
        {
            GameObject existingDisplay = GameObject.Find("AttentionStatusDisplay");

            if (existingDisplay != null)
            {
                Object.DestroyImmediate(existingDisplay);
            }

            GameObject display = new GameObject("AttentionStatusDisplay");
            display.transform.position = new Vector3(0f, 2.35f, 3.15f);
            display.transform.localScale = Vector3.one * 0.06f;

            TextMeshPro text = display.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 3.4f;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.text = "Attention status";

            AttentionStatusDisplay statusDisplay = display.AddComponent<AttentionStatusDisplay>();
            statusDisplay.Configure(manager, cameraTransform, text);
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);

            return material;
        }

        private static void SetMaterial(GameObject gameObject, Material material)
        {
            if (gameObject.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = Path.Combine(parent, folderName);

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
