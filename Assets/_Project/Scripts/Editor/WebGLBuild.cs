using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NW.Editor
{
    public static class WebGLBuild
    {
        // Run: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod NW.Editor.WebGLBuild.Build -buildDir <path>
        public static void Build()
        {
            string dir = "Builds/WebGL";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildDir") dir = args[i + 1];

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Battle.unity" },
                locationPathName = dir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[WebGLBuild] {r.summary.result} size={r.summary.totalSize} errors={r.summary.totalErrors}");
            EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
