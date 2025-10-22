#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;


namespace AD.Editor
{
    [InitializeOnLoad]
    public static class AdSdkAutoDefines
    {
        private static readonly Dictionary<string, string> Checks = new()
        {
            { "LEVEL_PLAY_SDK", "pkg:com.unity.services.levelplay" },
            { "CLEVER_SDK", "pkg:com.cleversolutions.ads.unity" },

        };
        private static ListRequest _packageListRequest;


        static AdSdkAutoDefines()
        {
            foreach (KeyValuePair<string, string> kv in Checks)
            {
                if (!kv.Value.StartsWith("pkg:"))
                    UpdateDefineForAllTargets(kv.Key, Directory.Exists(kv.Value));
            }

            if (NeedsPackageCheck())
            {
                _packageListRequest = Client.List(true); 
                EditorApplication.update += OnPackageListProgress;
            }
        }

        private static bool NeedsPackageCheck()
        {
            foreach (var kv in Checks)
            {
                if (kv.Value.StartsWith("pkg:"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnPackageListProgress()
        {
            if (!_packageListRequest.IsCompleted)
            {
                return;
            }

            if (_packageListRequest.Status == StatusCode.Success)
            {
                foreach (var kv in Checks)
                {
                    if (!kv.Value.StartsWith("pkg:")) continue;

                    string packageName = kv.Value.Substring(4);
                    bool installed = false;

                    foreach (var pkg in _packageListRequest.Result)
                    {
                        if (pkg.name == packageName)
                        {
                            installed = true;
                            break;
                        }
                    }

                    UpdateDefineForAllTargets(kv.Key, installed);
                }
            }

            EditorApplication.update -= OnPackageListProgress;
        }

        private static void UpdateDefineForAllTargets(string define, bool shouldHave)
        {
            foreach (BuildTargetGroup group in (BuildTargetGroup[]) System.Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown)
                    continue;

                try
                {
#if UNITY_2021_2_OR_NEWER
                    var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
                    var defines = PlayerSettings.GetScriptingDefineSymbols(named);
#else
                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
                    var list = new List<string>(defines.Split(';'));
                    bool changed = false;

                    if (shouldHave && !list.Contains(define))
                    {
                        list.Add(define);
                        changed = true;
                    }
                    else if (!shouldHave && list.Contains(define))
                    {
                        list.Remove(define);
                        changed = true;
                    }

                    if (changed)
                    {
#if UNITY_2021_2_OR_NEWER
                        PlayerSettings.SetScriptingDefineSymbols(named, string.Join(";", list));
#else
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
#endif
                    }
                }
                catch (System.ArgumentException)
                {
                }
            }
        }
    }
}

#endif