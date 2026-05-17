#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch standalone builds. CPU architectures match Build Settings and <see cref="OSArchitecture"/>:
/// Windows/Linux x64, macOS universal (Intel 64 + Apple Silicon) via <see cref="OSArchitecture.x64ARM64"/>.
/// Architecture is applied with <see cref="EditorUserBuildSettings.SetPlatformSettings"/> (see Unity forums / DesktopStandaloneBuildWindowExtension).
/// </summary>

public static class MultiplatformBuild
{
public const string GameName = "PacManCTF";

public const string GroupPrefix = "A3G";

/// <summary>Scenes passed to <see cref="BuildPipeline.BuildPlayer"/>.</summary>

public static readonly string[] DefaultScenes = { "Assets/Scenes/PacManNormal.unity" };

 [MenuItem("Build/Multiple Platforms...")]

public static void OpenBuildWindow()

 {

var win = EditorWindow.GetWindow<MultiplatformBuildWindow>(true, "Multi-platform build", true);

win.minSize = new Vector2(420, 160);

win.Show();

 }

/// <param name="groupNumber">1–99; used in folder name as <c>A3G{group:00}</c>.</param>

/// <param name="versionFolderSegment">Safe folder segment (no path separators); appears after <c>_v</c>.</param>

public static void RunBuild(int groupNumber, string versionFolderSegment)

 {

string groupTag = $"{GroupPrefix}{groupNumber:D2}";

string buildPath = $"build/{groupTag}_v{versionFolderSegment}/";

BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;

bool allBuildsSucceeded = false;

try

 {

if (!BuildOne($"{buildPath}macOS/{GameName}.app", BuildTarget.StandaloneOSX, OSArchitecture.x64ARM64, DefaultScenes))

return;

if (!BuildOne($"{buildPath}Linux/{GameName}.x86_64", BuildTarget.StandaloneLinux64, OSArchitecture.x64, DefaultScenes))

return;

if (!BuildOne($"{buildPath}Windows/{GameName}.exe", BuildTarget.StandaloneWindows64, OSArchitecture.x64, DefaultScenes))

return;

allBuildsSucceeded = true;

 }

finally

 {

EditorUserBuildSettings.SwitchActiveBuildTarget(

BuildPipeline.GetBuildTargetGroup(previousTarget),

previousTarget);

 }

if (!allBuildsSucceeded)

return;

string projectRoot = Path.GetDirectoryName(Application.dataPath);

if (string.IsNullOrEmpty(projectRoot))

 {

Debug.LogError("Could not resolve project root for zipping.");

return;

 }

if (!TryZipBuildFolderWithSubprocess(projectRoot, buildPath, out string zipPath))

Debug.LogError("Build outputs are in: " + Path.Combine(projectRoot, buildPath.TrimEnd('/', '\\')));

Debug.Log("Multi-platform build finished. Output root: " + buildPath + (zipPath != null ? $" Zip: {zipPath}" : ""));

 }

static bool BuildOne(string locationPathName, BuildTarget target, OSArchitecture architecture, string[] scenes)

 {

string platformName = BuildPipeline.GetBuildTargetName(target);

EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", architecture.ToString());

EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);

var options = new BuildPlayerOptions

 {

scenes = scenes,

locationPathName = locationPathName,

target = target,

options = BuildOptions.None

 };

BuildReport report = BuildPipeline.BuildPlayer(options);

BuildSummary summary = report.summary;

if (summary.result == BuildResult.Succeeded)

 {

Debug.Log($"Build succeeded ({target}, {architecture}): {locationPathName}, size {summary.totalSize} bytes");

return true;

 }

Debug.LogError($"Build failed ({target}, {architecture}): {locationPathName}. Result: {summary.result}. Errors: {summary.totalErrors}");

return false;

 }

/// <summary>

/// Packs the versioned build folder into a sibling .zip next to it (e.g. build/A3_v01-02/A3_v01-02.zip).

/// macOS/Linux: <c>zip</c> in PATH. Windows Editor: <c>tar.exe</c> (included with Windows 10+).

/// </summary>

static bool TryZipBuildFolderWithSubprocess(string projectRoot, string buildPathRelative, out string zipPath)

 {

zipPath = null;

string trimmed = buildPathRelative.TrimEnd('/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

string buildRoot = Path.GetFullPath(Path.Combine(projectRoot, trimmed));

if (!Directory.Exists(buildRoot))

 {

Debug.LogError($"Zip skipped: directory not found: {buildRoot}");

return false;

 }

string folderName = Path.GetFileName(buildRoot);

string parentDir = Path.GetDirectoryName(buildRoot);

if (string.IsNullOrEmpty(parentDir) || string.IsNullOrEmpty(folderName))

 {

Debug.LogError("Zip skipped: invalid build path structure.");

return false;

 }

zipPath = Path.Combine(parentDir, folderName + ".zip");

if (File.Exists(zipPath))

File.Delete(zipPath);

string quotedZip = "\"" + zipPath + "\"";

string quotedFolder = "\"" + folderName + "\"";

string fileName;

string arguments;

if (Application.platform == RuntimePlatform.WindowsEditor)

 {

fileName = "tar.exe";

arguments = $"-a -c -f {quotedZip} {quotedFolder}";

 }

else

 {

fileName = "zip";

arguments = $"-r -q {quotedZip} {quotedFolder}";

 }

const int timeoutMs = 600_000;

try

 {

using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

 {

FileName = fileName,

Arguments = arguments,

WorkingDirectory = parentDir,

UseShellExecute = false,

CreateNoWindow = true,

 });

if (p == null)

 {

Debug.LogError($"Failed to start process: {fileName}");

zipPath = null;

return false;

 }

if (!p.WaitForExit(timeoutMs))

 {

try { p.Kill(); }

catch (Exception) { /* ignore */ }

Debug.LogError($"Zip timed out after {timeoutMs / 1000}s: {fileName} {arguments}");

zipPath = null;

return false;

 }

if (p.ExitCode != 0)

 {

Debug.LogError($"Zip failed (exit {p.ExitCode}): {fileName} {arguments}");

zipPath = null;

return false;

 }

 }

catch (Exception e)

 {

Debug.LogError($"Zip subprocess error: {e.Message}");

zipPath = null;

return false;

 }

if (!File.Exists(zipPath))

 {

Debug.LogError("Zip reported success but archive is missing: " + zipPath);

zipPath = null;

return false;

 }

Debug.Log($"Created archive: {zipPath}");

return true;

 }

/// <summary>Turns user text into a single folder segment (no slashes, no invalid file name chars).</summary>

internal static bool TrySanitizeVersionSegment(string raw, out string sanitized, out string error)

 {

sanitized = null;

error = null;

if (string.IsNullOrWhiteSpace(raw))

 {

error = "Version cannot be empty.";

return false;

 }

string trimmed = raw.Trim();

var invalid = Path.GetInvalidFileNameChars();

var sb = new System.Text.StringBuilder(trimmed.Length);

foreach (char c in trimmed)

 {

if (invalid.Contains(c) || c == '/' || c == '\\')

sb.Append('-');

else

sb.Append(c);

 }

sanitized = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');

if (string.IsNullOrEmpty(sanitized))

 {

error = "Version is only invalid characters.";

return false;

 }

return true;

 }

}

/// <summary>Prompt for group (01–99; default 00 is rejected) and version string before running <see cref="MultiplatformBuild.RunBuild"/>.</summary>

public sealed class MultiplatformBuildWindow : EditorWindow

{

/// <summary>Placeholder default; build is blocked until set to 01–99.</summary>

string _groupInput = "00";

string _version = DateTime.Now.ToString("yy-MM-dd");

void OnEnable()

 {

if (string.IsNullOrEmpty(_version))

_version = DateTime.Now.ToString("yy-MM-dd");

 }

void OnGUI()

 {

EditorGUILayout.HelpBox(

$"Group defaults to 00 and cannot be used—set your real group (01–99). "

+ $"Outputs use folder names like {MultiplatformBuild.GroupPrefix}05_v26-04-06 (example).",

MessageType.Info);

EditorGUILayout.LabelField("Group number", EditorStyles.boldLabel);

_groupInput = EditorGUILayout.TextField(" (01–99)", _groupInput);

EditorGUILayout.Space(6);

EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);

_version = EditorGUILayout.TextField(" Folder suffix", _version);

DrawOutputPathPreviews();

EditorGUILayout.Space(10);

if (GUILayout.Button("Build all platforms", GUILayout.Height(28)))

 {

if (!TryValidateInputs(out int group, out string versionSeg))

return;

Close();

MultiplatformBuild.RunBuild(group, versionSeg);

 }

 }

bool TryValidateInputs(out int group, out string versionSeg)

 {

group = 0;

versionSeg = null;

string gs = _groupInput.Trim();

if (!int.TryParse(gs, NumberStyles.Integer, CultureInfo.InvariantCulture, out group))

 {

EditorUtility.DisplayDialog("Multi-platform build", "Group number must be an integer (01–99).", "OK");

return false;

 }

if (group == 0 || group < 1 || group > 99)

 {

EditorUtility.DisplayDialog(

"Multi-platform build",

"Group must be from 01 to 99. The default 00 is not valid—enter your assignment group.",

"OK");

return false;

 }

if (!MultiplatformBuild.TrySanitizeVersionSegment(_version, out versionSeg, out string verErr))

 {

EditorUtility.DisplayDialog("Multi-platform build", verErr, "OK");

return false;

 }

return true;

 }

void DrawOutputPathPreviews()

 {

string projectRoot = Path.GetDirectoryName(Application.dataPath);

EditorGUILayout.Space(8);

EditorGUILayout.LabelField("Output paths (preview)", EditorStyles.boldLabel);

if (string.IsNullOrEmpty(projectRoot))

 {

EditorGUILayout.HelpBox("Could not resolve project root.", MessageType.Error);

return;

 }

string groupTag = GetGroupTagPreview();

string verSeg = GetVersionPreviewSegment();

string folderBase = $"{groupTag}_v{verSeg}";

string buildFolderAbs = Path.GetFullPath(Path.Combine(projectRoot, "build", folderBase));

string zipAbs = Path.GetFullPath(Path.Combine(projectRoot, "build", folderBase + ".zip"));

EditorGUILayout.LabelField("Build folder", EditorStyles.miniBoldLabel);

EditorGUILayout.SelectableLabel(buildFolderAbs, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));

EditorGUILayout.LabelField(

"(macOS, Linux, and Windows builds go in subfolders here.)",

EditorStyles.miniLabel);

EditorGUILayout.Space(4);

EditorGUILayout.LabelField("Zip archive", EditorStyles.miniBoldLabel);

EditorGUILayout.SelectableLabel(zipAbs, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));

bool groupOk = int.TryParse(_groupInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)

&& g >= 1 && g <= 99;

bool verOk = MultiplatformBuild.TrySanitizeVersionSegment(_version, out _, out _);

if (!groupOk || !verOk)

 {

EditorGUILayout.HelpBox(

 (!groupOk ? "Set a valid group (01–99). " : "")

+ (!verOk ? "Set a valid version string. " : "")

+ "Paths marked with ? are incomplete until both are valid.",

MessageType.Warning);

 }

 }

string GetGroupTagPreview()

 {

if (int.TryParse(_groupInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)

&& g >= 1 && g <= 99)

return $"{MultiplatformBuild.GroupPrefix}{g:D2}";

return $"{MultiplatformBuild.GroupPrefix}??";

 }

string GetVersionPreviewSegment()

 {

if (MultiplatformBuild.TrySanitizeVersionSegment(_version, out string s, out _))

return s;

return "?";

 }

}

#endif