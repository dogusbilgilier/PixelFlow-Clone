#if UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Text.RegularExpressions;

public class iOSPostProcessBuild
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string teamId = PlayerSettings.iOS.appleDeveloperTeamID;

        if (string.IsNullOrEmpty(teamId))
        {
            Debug.LogWarning("[iOSPostProcessBuild] Apple Developer Team ID is empty! " +
                "Set it in Player Settings > iOS > Other Settings > Apple Developer Team ID.");
            return;
        }

        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);

        // Set DEVELOPMENT_TEAM for main Unity targets
        string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();
        string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();

        pbxProject.SetBuildProperty(mainTargetGuid, "DEVELOPMENT_TEAM", teamId);
        pbxProject.SetBuildProperty(frameworkTargetGuid, "DEVELOPMENT_TEAM", teamId);

        pbxProject.WriteToFile(pbxProjectPath);

        // Fix Firebase Swift Package Manager targets by editing raw pbxproj
        // SPM targets (Firebase etc.) are not accessible via public PBXProject API
        // so we inject DEVELOPMENT_TEAM directly into the file where it's missing
        string content = File.ReadAllText(pbxProjectPath);

        // Find CODE_SIGN_STYLE = Automatic; entries that are NOT followed by DEVELOPMENT_TEAM
        // and inject the team ID right after them
        string pattern = @"(CODE_SIGN_STYLE = Automatic;)(\s*\n(?!\s*DEVELOPMENT_TEAM))";
        string replacement = $"$1\n\t\t\t\tDEVELOPMENT_TEAM = {teamId};$2";

        string updated = Regex.Replace(content, pattern, replacement);

        if (updated != content)
        {
            File.WriteAllText(pbxProjectPath, updated);
            Debug.Log($"[iOSPostProcessBuild] Injected DEVELOPMENT_TEAM = {teamId} " +
                "into Firebase SPM targets via raw pbxproj edit.");
        }
        else
        {
            Debug.Log("[iOSPostProcessBuild] No additional SPM targets needed patching.");
        }

        Debug.Log($"[iOSPostProcessBuild] Done. Team ID: {teamId}");
    }
}
#endif
