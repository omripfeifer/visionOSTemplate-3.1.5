using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace VisionProAttentionGuide.Editor
{
    public static class VisionOSXcodePostprocessor
    {
        [PostProcessBuild(1000)]
        public static void PatchVisionOSXcodeProject(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.VisionOS)
            {
                return;
            }

            PatchUnityLibrary(pathToBuiltProject);
            PatchAppDelegate(pathToBuiltProject);
            RemoveDuplicateIl2CppOutputs(pathToBuiltProject);
        }

        private static void PatchUnityLibrary(string buildPath)
        {
            string unityLibraryPath = Path.Combine(
                buildPath,
                "Libraries/com.unity.xr.visionos/Runtime/Plugins/visionos/SwiftTrampoline/UnityLibrary.swift");

            if (!File.Exists(unityLibraryPath))
            {
                Debug.LogWarning($"Could not patch UnityLibrary.swift because it was not found at {unityLibraryPath}.");
                return;
            }

            string text = File.ReadAllText(unityLibraryPath);

            if (!text.Contains("private var retainedArgv"))
            {
                text = text.Replace(
                    "    private let unityFramework: UnityFramework\n",
                    "    private let unityFramework: UnityFramework\n    private var retainedArgv: UnsafeMutablePointer<UnsafeMutablePointer<Int8>?>?\n");
            }

            text = text.Replace(
                "        let argv = UnsafeMutablePointer<UnsafeMutablePointer<Int8>?>.allocate(capacity: arguments.count)\n",
                "        let argv = UnsafeMutablePointer<UnsafeMutablePointer<Int8>?>.allocate(capacity: arguments.count + 1)\n        argv.initialize(repeating: nil, count: arguments.count + 1)\n");

            if (!text.Contains("        retainedArgv = argv\n"))
            {
                text = text.Replace(
                    "        unityFramework.runEmbedded(withArgc: Int32(arguments.count), argv: argv, appLaunchOpts: nil)\n",
                    "        argv[arguments.count] = nil\n        retainedArgv = argv\n\n        unityFramework.runEmbedded(withArgc: Int32(arguments.count), argv: argv, appLaunchOpts: nil)\n");
            }

            File.WriteAllText(unityLibraryPath, text);
            Debug.Log("Patched UnityLibrary.swift argv lifetime/null terminator for visionOS.");
        }

        private static void PatchAppDelegate(string buildPath)
        {
            string appDelegatePath = Path.Combine(buildPath, "MainApp/UnityPolySpatialAppDelegate.swift");

            if (!File.Exists(appDelegatePath))
            {
                return;
            }

            string text = File.ReadAllText(appDelegatePath);

            if (!text.Contains("Starting Unity v10 with"))
            {
                text = text.Replace(
                    "        unity.run(arguments: arguments)\n",
                    "        print(\"Starting Unity v10 with \\(arguments.count) launch arguments\")\n        unity.run(arguments: arguments)\n");
                File.WriteAllText(appDelegatePath, text);
            }
        }

        private static void RemoveDuplicateIl2CppOutputs(string buildPath)
        {
            string il2CppOutputPath = Path.Combine(buildPath, "Il2CppOutputProject/Source/il2cppOutput");

            if (!Directory.Exists(il2CppOutputPath))
            {
                return;
            }

            int removedCount = 0;
            foreach (string filePath in Directory.GetFiles(il2CppOutputPath))
            {
                string fileName = Path.GetFileName(filePath);
                bool duplicateGeneratedFile =
                    fileName.EndsWith(" 2.c") ||
                    fileName.EndsWith(" 2.cpp") ||
                    fileName.EndsWith(" 2.h");

                if (!duplicateGeneratedFile)
                {
                    continue;
                }

                File.Delete(filePath);
                removedCount++;
            }

            if (removedCount > 0)
            {
                Debug.Log($"Removed {removedCount} duplicate IL2CPP generated files from the visionOS Xcode output.");
            }
        }
    }
}
