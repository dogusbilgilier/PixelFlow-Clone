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
            Debug.LogWarning("[iOSPostProcessBuild] Apple Developer Team ID is empty!");
            return;
        }

        // ── 1. PBXProject: set DEVELOPMENT_TEAM for main targets ──
        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);

        string mainTargetGuid      = pbxProject.GetUnityMainTargetGuid();
        string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();

        pbxProject.SetBuildProperty(mainTargetGuid,      "DEVELOPMENT_TEAM", teamId);
        pbxProject.SetBuildProperty(frameworkTargetGuid, "DEVELOPMENT_TEAM", teamId);

        pbxProject.WriteToFile(pbxProjectPath);

        // ── 2. Fix Firebase SPM targets via raw pbxproj regex ──
        string content = File.ReadAllText(pbxProjectPath);
        string pattern = @"(CODE_SIGN_STYLE = Automatic;)(\s*\n(?!\s*DEVELOPMENT_TEAM))";
        string replacement = $"$1\n\t\t\t\tDEVELOPMENT_TEAM = {teamId};$2";
        string updated = Regex.Replace(content, pattern, replacement);

        if (updated != content)
        {
            File.WriteAllText(pbxProjectPath, updated);
            Debug.Log("[iOSPostProcessBuild] Injected DEVELOPMENT_TEAM into Firebase SPM targets.");
        }

        // ── 3. Info.plist: disable export compliance prompt ──
        // Firebase uses standard HTTPS (no custom encryption), so this is exempt.
        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        plist.WriteToFile(plistPath);
        Debug.Log("[iOSPostProcessBuild] Set ITSAppUsesNonExemptEncryption = NO in Info.plist.");

        Debug.Log($"[iOSPostProcessBuild] Done. Team ID: {teamId}");
    }
}
#endif
