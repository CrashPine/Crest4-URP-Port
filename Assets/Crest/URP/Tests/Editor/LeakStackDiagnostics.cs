// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
static class LeakStackDiagnostics
{
    [InitializeOnLoadMethod] static void EnableWhenRequested()
    {
        if(!Environment.GetCommandLineArgs().Contains("-crestLeakStacks"))return;
        var before=UnsafeUtility.GetLeakDetectionMode();UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
        Directory.CreateDirectory("Evidence");
        string message=$"utc={DateTime.UtcNow:O}; process={System.Diagnostics.Process.GetCurrentProcess().Id}; before={before}; after={UnsafeUtility.GetLeakDetectionMode()}; process-local UnsafeUtility mode; no EditorPrefs writes";
        File.AppendAllText("Evidence/LeakStackDiagnostics.txt",message+"\n");Debug.Log(message);
    }
}
