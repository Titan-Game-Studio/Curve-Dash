using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace STG.CurveDash.Editor
{
    public class AutoIncrementVersion : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Tăng version code Android
            int currentVersionCode = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = currentVersionCode + 1;

            // Tùy chọn: Tăng version name (bundleVersion)
            string currentVersionName = PlayerSettings.bundleVersion;
            string[] parts = currentVersionName.Split('.');
            if (parts.Length == 3 && int.TryParse(parts[2], out int patch))
            {
                patch++;
                PlayerSettings.bundleVersion = $"{parts[0]}.{parts[1]}.{patch}";
            }

            Debug.Log($"✅ Auto-incremented version: {PlayerSettings.bundleVersion} (Code: {PlayerSettings.Android.bundleVersionCode})");
        }
    }

}
