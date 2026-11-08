using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Race1.Editor
{
    /// <summary>
    /// One-click project setup and Android APK build. Also runs headless:
    ///   Unity -batchmode -nographics -quit -projectPath . -executeMethod Race1.Editor.Race1BuildTool.BuildApkBatch
    /// The game needs no hand-made scene: the runtime bootstrap builds everything on start. A minimal scene
    /// is created so Unity has something to build.
    /// </summary>
    public static class Race1BuildTool
    {
        private const string ScenePath = "Assets/Race1/Scenes/Main.unity";
        private const string ConfigPath = "Assets/Race1/Resources/RaceConfig.asset";
        private const string ApkPath = "Builds/Android/HorizonDrive.apk";
        private const string PackageId = "com.race1.horizondrive";

        [MenuItem("Race1/Setup Project")]
        public static void SetupProject()
        {
            EnsureFolders();
            EnsureScene();
            EnsureConfigAsset();
            Race1VisualSetup.CreateProfileIfMissing();
            ConfigureAndroidPlayer();
            ConfigureAndroidTools();
            AssignUrpAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Race1: project setup complete. Check the console for warnings above.");
        }

        [MenuItem("Race1/Build Android APK")]
        public static void BuildApk()
        {
            SetupProject();
            BuildAndroid(development: false);
        }

        /// <summary>Entry point for command-line builds. Exit code 0 on success, 1 on failure.</summary>
        public static void BuildApkBatch()
        {
            try
            {
                SetupProject();
                string result = BuildAndroid(development: false);
                Console.WriteLine("Race1 build result: " + result);
                EditorApplication.Exit(result == null ? 0 : 1);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Race1 build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { "Assets/Race1/Scenes", "Assets/Race1/Resources", "Builds/Android" })
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Creates RaceConfig through its type name, so this assembly does not reference the game code.</summary>
        private static void EnsureConfigAsset()
        {
            if (File.Exists(ConfigPath)) return;
            ScriptableObject config = ScriptableObject.CreateInstance("Race1.Game.RaceConfig");
            if (config == null)
            {
                Debug.LogWarning("Race1: RaceConfig type not found yet. Code defaults will be used at runtime.");
                return;
            }
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        private static void ConfigureAndroidPlayer()
        {
            PlayerSettings.companyName = "Race1";
            PlayerSettings.productName = "Horizon Drive";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.Android.bundleVersionCode = 2;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.renderOutsideSafeArea = false;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            Debug.Log("Race1: Android player settings applied. Set Active Input Handling to 'Both' " +
                      "(Project Settings > Player > Other Settings) so touch, gamepad and tilt are all enabled.");
        }

        /// <summary>
        /// Points the editor at the Android SDK, NDK and JDK installed on this machine. Values are editor
        /// preferences, so they stay per machine and are not committed to the repository.
        /// </summary>
        private static void ConfigureAndroidTools()
        {
            string sdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "/opt/android-sdk";
            string ndk = Path.Combine(sdk, "ndk", "23.1.7779620");
            string jdk = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrEmpty(jdk)) jdk = "/usr/lib/jvm/java-17-openjdk-amd64";

            if (Directory.Exists(sdk)) EditorPrefs.SetString("AndroidSdkRoot", sdk);
            if (Directory.Exists(ndk))
            {
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                EditorPrefs.SetString("AndroidNdkRootR23b", ndk);
            }
            else
            {
                Debug.LogWarning("Race1: NDK not found at " + ndk + ". Install NDK r23b or set the path in Preferences > External Tools.");
            }
            if (Directory.Exists(jdk)) EditorPrefs.SetString("JdkPath", jdk);
        }

        private static void AssignUrpAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
            {
                Debug.LogWarning("Race1: no URP pipeline asset found. Create one via Assets > Create > Rendering > " +
                                 "URP Asset (with Universal Renderer) and run Setup again.");
                return;
            }
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            GraphicsSettings.defaultRenderPipeline = asset as UnityEngine.Rendering.RenderPipelineAsset;
            QualitySettings.renderPipeline = asset as UnityEngine.Rendering.RenderPipelineAsset;
        }

        /// <summary>Builds the APK and returns null on success, or an error message.</summary>
        private static string BuildAndroid(bool development)
        {
            EditorUserBuildSettings.buildAppBundle = false;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"Race1: APK written to {Path.GetFullPath(ApkPath)} ({report.summary.totalSize / (1024 * 1024)} MB)");
                return null;
            }
            string error = $"build {report.summary.result}, {report.summary.totalErrors} error(s)";
            Debug.LogError("Race1: " + error);
            return error;
        }
    }
}
