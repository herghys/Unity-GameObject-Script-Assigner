#if UNITY_EDITOR
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.Updater
{
    public static class PackageUpdater
    {
        private static PackageJsonData packageData;
        private static string upmPackagePath = Path.Combine("Packages", "com.herghys.gameobjectscriptassigner", "package.json");
        private static string manifestPath = Path.Combine("Packages", "manifest.json");

        static PackageUpdater()
        {
            LoadPackageJson();
            EditorApplication.update += RunOnce;
        }

        private static void RunOnce()
        {
            EditorApplication.update -= RunOnce;
            _ = CheckForUpdates(true);
        }

        [MenuItem("Tools/Herghys/Script Assigner/Check for Update", false, 1001)]
        private static void ManualCheck()
        {
            _ = CheckForUpdates(false);
        }

        /// <summary>
        /// Load Package JSON (UPM first, fallback to Assets)
        /// </summary>
        private static void LoadPackageJson()
        {
            packageData = null;

            // 1. UPM path
            if (File.Exists(upmPackagePath))
            {
                string json = File.ReadAllText(upmPackagePath);
                packageData = JsonUtility.FromJson<PackageJsonData>(json);
                return;
            }

            // 2. Assets path
            string[] guids = AssetDatabase.FindAssets("package t:TextAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("package.json"))
                {
                    var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (textAsset != null)
                    {
                        packageData = JsonUtility.FromJson<PackageJsonData>(textAsset.text);
                        return;
                    }
                }
            }

            Debug.LogWarning("[PackageUpdater] package.json not found in Packages/ or Assets/");
            packageData = new PackageJsonData(); // fallback
        }

        /// <summary>
        /// Check for updates
        /// </summary>
        private static async Task CheckForUpdates(bool silent)
        {
            if (string.IsNullOrEmpty(packageData.repositoryUrl))
            {
                if (!silent)
                    EditorUtility.DisplayDialog("Update Check", "Repository URL not found in package.json", "OK");
                return;
            }

            // Extract owner + repo
            var match = Regex.Match(packageData.repositoryUrl, @"github\.com/([^/]+)/([^/.]+)");
            if (!match.Success)
            {
                if (!silent)
                    EditorUtility.DisplayDialog("Update Check", "Invalid repository URL", "OK");
                return;
            }

            string owner = match.Groups[1].Value;
            string repo = match.Groups[2].Value;

            string latestVersion = await GetLatestReleaseTag(owner, repo);
            if (string.IsNullOrEmpty(latestVersion))
            {
                if (!silent)
                    EditorUtility.DisplayDialog("Update Check", "Could not fetch latest version.", "OK");
                return;
            }

            if (IsNewerVersion(latestVersion, packageData.version))
            {
                if (EditorUtility.DisplayDialog(
                    "Update Available",
                    $"A new version of {packageData.displayName} is available!\n\n" +
                    $"Current: {packageData.version}\nLatest: {latestVersion}\n\n" +
                    "Do you want to update?",
                    "Update Now", "Later"))
                {
                    if (File.Exists(upmPackagePath))
                    {
                        UpdateManifestVersion(latestVersion);
                    }
                    else
                    {
                        Application.OpenURL(packageData.repositoryUrl);
                    }
                }
            }
            else if (!silent)
            {
                EditorUtility.DisplayDialog("Update Check", $"{packageData.displayName} is up to date (v{packageData.version}).", "OK");
            }
        }

        /// <summary>
        /// Update manifest.json dependency version
        /// </summary>
        private static void UpdateManifestVersion(string newVersion)
        {
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("manifest.json not found, cannot update.");
                return;
            }

            string manifestText = File.ReadAllText(manifestPath);

            // Match the dependency (with or without #tag)
            string pattern = "\"com.herghys.animationbatchhelper\"\\s*:\\s*\"([^\"]+)\"";
            var match = Regex.Match(manifestText, pattern);
            if (!match.Success)
            {
                Debug.LogWarning("[PackageUpdater] Could not find com.herghys.animationbatchhelper in manifest.json");
                return;
            }

            string currentValue = match.Groups[1].Value;
            string newValue;

            if (currentValue.Contains("#"))
            {
                // Replace existing tag
                newValue = Regex.Replace(currentValue, "#.*$", $"#{newVersion}");
            }
            else if (currentValue.EndsWith(".git"))
            {
                // Append tag
                newValue = $"{currentValue}#{newVersion}";
            }
            else
            {
                // Fallback (version only)
                newValue = newVersion;
            }

            string replacement = $"\"com.herghys.animationbatchhelper\": \"{newValue}\"";
            manifestText = Regex.Replace(manifestText, pattern, replacement);

            File.WriteAllText(manifestPath, manifestText);
            Debug.Log($"[PackageUpdater] Updated manifest.json to {newValue}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Get latest GitHub release tag
        /// </summary>
        private static async Task<string> GetLatestReleaseTag(string owner, string repo)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "UnityEditor");
                    string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                    var response = await client.GetStringAsync(url);

                    var match = Regex.Match(response, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (match.Success)
                        return match.Groups[1].Value.TrimStart('v');
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PackageUpdater] Update check failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Compare versions
        /// </summary>
        private static bool IsNewerVersion(string latest, string current)
        {
            if (System.Version.TryParse(latest, out var latestVer) &&
                System.Version.TryParse(current, out var currentVer))
            {
                return latestVer > currentVer;
            }
            return false;
        }
    }
}
#endif