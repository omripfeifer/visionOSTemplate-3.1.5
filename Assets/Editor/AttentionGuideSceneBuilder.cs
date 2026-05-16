using System.IO;
using TMPro;
using Unity.PolySpatial;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisionProAttentionGuide;

namespace VisionProAttentionGuide.Editor
{
    public static class AttentionGuideSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/AttentionGuideMVP.unity";
        private const string MixamoScenePath = "Assets/Scenes/AttentionGuideMixamo.unity";
        private const string MaterialsFolder = "Assets/Materials";
        private const string MixamoFolder = "Assets/AttentionGuide/Mixamo";
        private const string MixamoAvatarPath = MixamoFolder + "/Megan.fbx";
        private const string MixamoHeadTurnPath = MixamoFolder + "/CockyHeadTurn.fbx";
        private const string MixamoPointingPath = MixamoFolder + "/Pointing.fbx";
        private const string MixamoControllerPath = MixamoFolder + "/MeganAttentionGuide.controller";
        private const string PendingAutoCreatePath = MixamoFolder + "/CreateMixamoScene.flag";
        private const float MixamoAvatarHeightMeters = 1.75f;
        private const float MixamoTargetScale = 0.45f;
        private static readonly Vector3 MixamoPersonPosition = new Vector3(-0.35f, 0.95f, 3.6f);
        private static readonly Vector3 MixamoTargetPosition = new Vector3(0.85f, 1.7f, 3.9f);

        [MenuItem("Vision Pro Assignment/Auto Create Mixamo Scene On Next Refresh")]
        public static void AutoCreateMixamoSceneOnNextRefresh()
        {
            EnsureFolder("Assets", "AttentionGuide");
            EnsureFolder("Assets/AttentionGuide", "Mixamo");
            File.WriteAllText(PendingAutoCreatePath, "Create the Mixamo Attention Guide scene on the next editor refresh.");
            AssetDatabase.Refresh();
            Debug.Log("Mixamo Attention Guide scene will be created after the next Unity script refresh.");
        }

        [InitializeOnLoadMethod]
        private static void CreatePendingMixamoScene()
        {
            if (!File.Exists(PendingAutoCreatePath))
            {
                return;
            }

            File.Delete(PendingAutoCreatePath);
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                CreateMixamoAttentionGuideScene();
            };
        }

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

        [MenuItem("Vision Pro Assignment/Create Mixamo Attention Guide Scene")]
        public static void CreateMixamoAttentionGuideScene()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "AttentionGuide");
            EnsureFolder("Assets/AttentionGuide", "Mixamo");

            if (!ConfigureMixamoImports())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AttentionGuideMixamo";

            Material targetMaterial = CreateMaterial("M_TargetBlue", new Color(0.1f, 0.45f, 1f));

            Camera camera = CreateMainCamera();
            CreateLight();
            CreateVolumeCamera();

            Transform target = CreateTarget(targetMaterial, MixamoTargetPosition, MixamoTargetScale);
            VirtualPersonGuide guide = CreateMixamoVirtualPerson(target.position);

            if (guide == null)
            {
                Debug.LogError("Could not create the Mixamo virtual person. Check the FBX files in Assets/AttentionGuide/Mixamo.");
                return;
            }

            AttentionGuidanceManager manager = CreateAttentionSystem(camera.transform, target, guide);
            CreateStatusDisplay(manager, camera.transform);

            EditorSceneManager.SaveScene(scene, MixamoScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MixamoScenePath, true)
            };

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MixamoScenePath);
            Debug.Log($"Created {MixamoScenePath} with the Megan Mixamo avatar and attention-guide animations.");
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

            bool isMixamoPerson = person != null && person.GetComponentInChildren<Animator>() != null;

            if (target != null)
            {
                target.transform.position = isMixamoPerson ? MixamoTargetPosition : new Vector3(1.75f, 1.45f, 10.6f);
                target.transform.localScale = Vector3.one * (isMixamoPerson ? MixamoTargetScale : 0.65f);
            }

            if (person != null && isMixamoPerson)
            {
                person.transform.position = MixamoPersonPosition;
                person.transform.rotation = FaceTargetOnGround(MixamoPersonPosition, MixamoTargetPosition);
                ResizeAvatarToHeight(person, MixamoAvatarHeightMeters);
                ApplyMeganMaterials(person);
            }
            else if (person != null)
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
            return CreateTarget(material, new Vector3(1.75f, 1.45f, 10.6f), 0.65f);
        }

        private static Transform CreateTarget(Material material, Vector3 position, float scale)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "TargetObject";
            target.transform.position = position;
            target.transform.localScale = Vector3.one * scale;
            SetMaterial(target, material);

            return target.transform;
        }

        private static VirtualPersonGuide CreateVirtualPerson(Material bodyMaterial, Material headMaterial, Material armMaterial)
        {
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

            return guide;
        }

        private static VirtualPersonGuide CreateMixamoVirtualPerson(Vector3 targetPosition)
        {
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoAvatarPath);

            if (characterPrefab == null)
            {
                Debug.LogError($"Missing Mixamo avatar at {MixamoAvatarPath}.");
                return null;
            }

            GameObject person = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;

            if (person == null)
            {
                person = Object.Instantiate(characterPrefab);
            }

            person.name = "VirtualPerson";
            person.transform.SetPositionAndRotation(MixamoPersonPosition, Quaternion.identity);
            person.transform.localScale = Vector3.one;
            ApplyMeganMaterials(person);
            ResizeAvatarToHeight(person, MixamoAvatarHeightMeters);
            person.transform.rotation = FaceTargetOnGround(MixamoPersonPosition, targetPosition);

            Animator animator = person.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = person.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = CreateOrUpdateMixamoAnimatorController();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head) ?? FindDescendant(person.transform, "Head");
            Transform pointingArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm)
                ?? animator.GetBoneTransform(HumanBodyBones.RightLowerArm)
                ?? FindDescendant(person.transform, "RightArm")
                ?? FindDescendant(person.transform, "RightForeArm");

            VirtualPersonGuide guide = person.AddComponent<VirtualPersonGuide>();
            guide.Configure(head, pointingArm, animator, false);

            if (head == null)
            {
                Debug.LogWarning("Megan avatar was created, but the Head bone was not found. Head-turn guidance will be animation-only.");
            }

            if (pointingArm == null)
            {
                Debug.LogWarning("Megan avatar was created, but the right arm bone was not found. Pointing will use only the Mixamo clip.");
            }

            return guide;
        }

        private static void ApplyMeganMaterials(GameObject person)
        {
            Material skin = CreateCharacterMaterial("M_MeganSkin", new Color(0.86f, 0.58f, 0.43f), 0.28f);
            Material hair = CreateCharacterMaterial("M_MeganHair", new Color(0.18f, 0.11f, 0.07f), 0.35f);
            Material shirt = CreateCharacterMaterial("M_MeganShirt", new Color(0.78f, 0.88f, 0.96f), 0.22f);
            Material jeans = CreateCharacterMaterial("M_MeganJeans", new Color(0.08f, 0.17f, 0.32f), 0.38f);
            Material shoes = CreateCharacterMaterial("M_MeganShoes", new Color(0.82f, 0.82f, 0.78f), 0.45f);
            Material neutral = CreateCharacterMaterial("M_MeganNeutral", new Color(0.38f, 0.37f, 0.35f), 0.4f);

            foreach (Renderer renderer in person.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = SelectMeganMaterial(renderer, materials[i], skin, hair, shirt, jeans, shoes, neutral);
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material SelectMeganMaterial(
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

        private static Material CreateCharacterMaterial(string materialName, Color baseColor, float smoothness)
        {
            string folder = MixamoFolder + "/Materials";
            EnsureFolder(MixamoFolder, "Materials");

            string path = $"{folder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = baseColor;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ResizeAvatarToHeight(GameObject person, float targetHeightMeters)
        {
            if (!TryGetRendererBounds(person, out Bounds bounds) || bounds.size.y <= 0.01f)
            {
                Debug.LogWarning("Could not calculate Megan avatar bounds; keeping the imported scale.");
                return;
            }

            float desiredFeetY = person.transform.position.y;
            float scaleMultiplier = targetHeightMeters / bounds.size.y;
            person.transform.localScale *= scaleMultiplier;

            if (TryGetRendererBounds(person, out Bounds scaledBounds))
            {
                Vector3 position = person.transform.position;
                position.y += desiredFeetY - scaledBounds.min.y;
                person.transform.position = position;
            }
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(root.transform.position, Vector3.zero);
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

        private static Quaternion FaceTargetOnGround(Vector3 personPosition, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - personPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static RuntimeAnimatorController CreateOrUpdateMixamoAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MixamoControllerPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(MixamoControllerPath);
            }

            EnsureBoolParameter(controller, "IsLooking");
            EnsureBoolParameter(controller, "IsPointing");

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorStateMachine stateMachine = layer.stateMachine;
            ClearStateMachine(stateMachine);

            AnimationClip lookClip = LoadFirstAnimationClip(MixamoHeadTurnPath);
            AnimationClip pointingClip = LoadFirstAnimationClip(MixamoPointingPath);

            AnimatorState idle = stateMachine.AddState("Idle");
            AnimatorState looking = stateMachine.AddState("Head Turn");
            AnimatorState pointing = stateMachine.AddState("Pointing");

            looking.motion = lookClip;
            pointing.motion = pointingClip;

            stateMachine.defaultState = idle;

            AddBoolTransition(idle, looking, "IsLooking", true);
            AddBoolTransition(idle, pointing, "IsPointing", true);
            AddBoolTransition(looking, idle, "IsLooking", false);
            AddBoolTransition(looking, pointing, "IsPointing", true);
            AddBoolTransition(pointing, looking, "IsPointing", false);
            AddBoolTransition(pointing, idle, "IsLooking", false);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        private static bool ConfigureMixamoImports()
        {
            AssetDatabase.Refresh();

            if (!File.Exists(MixamoAvatarPath) || !File.Exists(MixamoHeadTurnPath) || !File.Exists(MixamoPointingPath))
            {
                Debug.LogError($"Expected Mixamo files at {MixamoAvatarPath}, {MixamoHeadTurnPath}, and {MixamoPointingPath}.");
                return false;
            }

            ConfigureMixamoModelImport(MixamoAvatarPath, null, null, false);
            Avatar sourceAvatar = LoadAvatar(MixamoAvatarPath);

            ConfigureMixamoModelImport(MixamoHeadTurnPath, "CockyHeadTurn", sourceAvatar, true);
            ConfigureMixamoModelImport(MixamoPointingPath, "Pointing", sourceAvatar, true);
            AssetDatabase.Refresh();

            return true;
        }

        private static void ConfigureMixamoModelImport(string path, string clipName, Avatar sourceAvatar, bool importAnimation)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                Debug.LogWarning($"Could not configure Mixamo import settings for {path}.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = importAnimation;

            if (sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            if (importAnimation && !string.IsNullOrEmpty(clipName))
            {
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

                if (clips.Length > 0)
                {
                    clips[0].name = clipName;
                    clips[0].loopTime = false;
                    clips[0].keepOriginalOrientation = true;
                    clips[0].keepOriginalPositionXZ = true;
                    clips[0].keepOriginalPositionY = true;
                    importer.clipAnimations = clips;
                }
            }

            importer.SaveAndReimport();
        }

        private static Avatar LoadAvatar(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object asset in assets)
            {
                if (asset is Avatar avatar && avatar.isHuman)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static AnimationClip LoadFirstAnimationClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"No animation clip found in {path}.");
            return null;
        }

        private static void EnsureBoolParameter(AnimatorController controller, string parameterName)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return;
                }
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (ChildAnimatorState state in stateMachine.states)
            {
                stateMachine.RemoveState(state.state);
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameterName, bool expectedValue)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterName);
        }

        private static Transform FindDescendant(Transform root, string nameSuffix)
        {
            if (root.name.EndsWith(nameSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindDescendant(child, nameSuffix);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
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
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 3.8f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.92f, 0.12f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = $"{AttentionGuidanceManager.BuildLabel}\nMegan state: booting";

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
