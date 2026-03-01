using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class WebGLOptimizer : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.WebGL)
        {
            Debug.Log("[WebGLOptimizer] Applying optimal WebGL settings...");

            // Strip Engine Code
            PlayerSettings.stripEngineCode = true;

            // Decompression Fallback
            PlayerSettings.WebGL.decompressionFallback = true;

            // Texture Compression (ASTC/ETC2 context conceptually, standardizing WebGL defaults)
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        }
    }
}
