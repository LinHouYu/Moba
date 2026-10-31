using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Moba.Editor
{
    public static class MobaAndroidBuilder
    {
        private const string TriggerFile = "Temp/build_apk_trigger.txt";
        public const string OutputApkPath = "Builds/Android/MobaDemo.apk";
        public const string PackageName = "com.moba.demo";

        [InitializeOnLoadMethod]
        private static void WatchBuildTrigger()
        {
            EditorApplication.delayCall += CheckTrigger;
            EditorApplication.update -= CheckTrigger;
            EditorApplication.update += CheckTrigger;
        }

        private static void CheckTrigger()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(TriggerFile))
            {
                try
                {
                    File.Delete(TriggerFile);
                }
                catch { }

                Debug.Log("[MobaAndroidBuilder] Build trigger detected! Starting Android APK build...");
                BuildApk();
            }
        }

        [MenuItem("Moba/Build Android APK")]
        public static bool BuildApk()
        {
            try
            {
                string fullApkDir = Path.GetDirectoryName(OutputApkPath);
                if (!string.IsNullOrEmpty(fullApkDir) && !Directory.Exists(fullApkDir))
                {
                    Directory.CreateDirectory(fullApkDir);
                }

                // 1. Configure Embedded Tools (Unity bundled SDK/NDK/JDK)
                EditorPrefs.SetBool("SdkUseEmbedded", true);
                EditorPrefs.SetBool("NdkUseEmbedded", true);
                EditorPrefs.SetBool("JdkUseEmbedded", true);
                EditorPrefs.SetBool("GradleUseEmbedded", true);

                // 2. Configure PlayerSettings for Android MOBA
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
                PlayerSettings.productName = "MobaDemo";
                PlayerSettings.companyName = "Moba";

                // Landscape orientation for MOBA
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;

                // Architecture: ARM64 + IL2CPP
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

                // 3. Setup Build Options
                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Game.unity" },
                    locationPathName = OutputApkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                Debug.Log($"[MobaAndroidBuilder] Building APK to: {Path.GetFullPath(OutputApkPath)}...");
                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[MobaAndroidBuilder] APK Build Succeeded! Size: {summary.totalSize / (1024 * 1024)} MB");
                    File.WriteAllText("Temp/build_apk_result.txt", $"SUCCESS:{OutputApkPath}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[MobaAndroidBuilder] APK Build Failed! Result: {summary.result}, Errors: {summary.totalErrors}");
                    File.WriteAllText("Temp/build_apk_result.txt", $"FAILED:{summary.result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                File.WriteAllText("Temp/build_apk_result.txt", $"EXCEPTION:{ex.Message}");
                return false;
            }
        }
    }
}
