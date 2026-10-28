#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PaperSoccer.Native.Editor
{
    public static class PaperSoccerBuild
    {
        private const string PackageId = "com.kudzho.papersoccer";
        private const string ScenePath = "Assets/Scenes/PaperSoccer.unity";

        [MenuItem("Paper Soccer/Build Android APK (test)")]
        public static void BuildTestApk() => Build("Builds/PaperSoccer-Test.apk", false, true);
        [MenuItem("Paper Soccer/Build Android APK (release)")]
        public static void BuildReleaseApk() => Build("Builds/PaperSoccer.apk", false, false);
        [MenuItem("Paper Soccer/Build Android App Bundle (AAB)")]
        public static void BuildAab() => Build("Builds/PaperSoccer.aab", true, false);

        [MenuItem("Paper Soccer/Apply Android Store Settings")]
        public static void ApplySettings()
        {
            ApplyPlayerSettingsWithoutDialog();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EnsureSceneInBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("Paper Soccer: Android settings applied. Package: " + PackageId + "; target API: 36; architecture: ARM64.");
            EditorUtility.DisplayDialog("Настройки применены", "Параметры Android сохранены. Для тестовой установки выбери Paper Soccer → Build Android APK (test). Для публикации настрой личный ключ подписи.", "ОК");
        }

        private static void Build(string relativeOutput, bool appBundle, bool development)
        {
            ApplyPlayerSettingsWithoutDialog();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Не удалось переключить платформу", "Открой File → Build Profiles и установи Android активной платформой. В Unity Hub должен быть установлен Android Build Support с SDK, NDK и OpenJDK.", "ОК");
                return;
            }
            EnsureSceneInBuild();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativeOutput));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.development = development;
            BuildOptions options = development ? BuildOptions.Development : BuildOptions.None;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var buildOptions = new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = BuildTarget.Android, options = options };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Paper Soccer build finished: " + output + " (" + report.summary.totalSize + " bytes)");
                EditorUtility.RevealInFinder(output);
                EditorUtility.DisplayDialog("Сборка готова", "Файл создан:\n" + output + "\n\nAPK для проверки не предназначен для публикации. Для магазина используй релизную сборку с собственным ключом подписи.", "ОК");
            }
            else
            {
                Debug.LogError("Paper Soccer build failed: " + report.summary.result + ". See Console for full errors.");
                EditorUtility.DisplayDialog("Сборка не завершена", "Unity сообщила об ошибке. Открой Window → General → Console и посмотри первую красную ошибку.", "ОК");
            }
        }

        private static void ApplyPlayerSettingsWithoutDialog()
        {
            PlayerSettings.companyName = "KuzDzho";
            PlayerSettings.productName = "Бумажный футбол";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EnsureSceneInBuild();
        }

        private static void EnsureSceneInBuild()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("Required scene is missing: " + ScenePath);
                return;
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
