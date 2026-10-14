using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Skillprint.SDK.Editor
{
    /// <summary>
    /// Warns when a release (non-development) build would ship a Skillprint partner API key.
    /// An extracted key grants your organization's full Skillprint API access, so shipped games
    /// should use <see cref="SkillprintAuthMode.TokenProvider"/> instead.
    /// </summary>
    public class SkillprintBuildKeyCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0)
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SkillprintConfig"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SkillprintConfig>(path);
                if (config == null)
                {
                    continue;
                }

                bool hasKey =
                    !string.IsNullOrWhiteSpace(config.productionPartnerApiKey)
                    || !string.IsNullOrWhiteSpace(config.stagingPartnerApiKey);
                if (!hasKey)
                {
                    continue;
                }

                string why = config.RequiresPartnerApiKey
                    ? "uses Auth Mode PartnerApiKey"
                    : "is in TokenProvider mode but still has a Partner API Key set";
                Debug.LogWarning(
                    $"[SkillprintSDK] {path} {why}. If this config is included in the build, the key ships "
                        + "with your game and can be extracted. For release builds, set Auth Mode to "
                        + "TokenProvider, clear the key fields, and have your backend mint SDK tokens "
                        + "(POST /partners/api/sdk/token/).",
                    config
                );
            }
        }
    }
}
