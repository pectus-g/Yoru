using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Writes the WHOLE Unity console (every Debug.Log / warning / error / exception from every script)
/// plus high-rate combat telemetry into a text file inside the project folder:
///
///     <project>/OniLogs/oni_YYYY-MM-DD_HH-mm-ss.log
///
/// Purpose: after a play test the log can be read straight from disk. Nothing to copy, nothing to
/// paste, however long it gets. The folder sits NEXT to Assets (not inside it), so Unity never
/// imports the files and there is no reimport churn while playing.
///
/// Every line is stamped [frame  realSeconds  timeScale] so slow-motion moments (Yoru's aim drops
/// the world clock to 0.1) are visible at a glance, and errors carry the first lines of their stack
/// trace. Telemetry lines (Line) go ONLY to the file — never to the console — so per-frame charge
/// data cannot spam the editor.
///
/// Started by OniBoss.Awake when its Write Log File toggle is on. Keeps the newest few files only.
/// </summary>
public static class OniDebugLogFile
{
    private static StreamWriter writer;
    private static string path;
    private static bool hooked;
    private static int linesWritten;

    public static bool IsOpen => writer != null;
    public static string CurrentPath => path;

    /// <summary>Open a fresh log file for this play session (no-op if one is already open).</summary>
    public static void Begin(string subFolder = "OniLogs", int keepNewest = 8)
    {
        if (writer != null) return;

        try
        {
            // Editor: next to Assets (readable straight from the project folder). Player build: the
            // per-user data folder — never inside the app bundle.
            string root = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                : Application.persistentDataPath;
            string dir = Path.Combine(root, subFolder);
            Directory.CreateDirectory(dir);
            Prune(dir, keepNewest);

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            path = Path.Combine(dir, $"oni_{stamp}.log");
            writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };

            writer.WriteLine($"# Oni combat log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"# Unity {Application.unityVersion} | scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' | platform {Application.platform}");
            writer.WriteLine("# columns: [frame realSeconds timeScale] KIND message      (KIND: LOG WARN ERR EXC TEL)");
            writer.WriteLine("#");

            if (!hooked)
            {
                Application.logMessageReceived += OnLogMessage;
                Application.quitting += End;
                hooked = true;
            }

            Debug.Log($"[OniLog] console + telemetry → {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OniLog] could not open the log file ({e.Message}). Console only this session.");
            writer = null;
        }
    }

    /// <summary>Close the file. Called automatically when play stops; safe to call twice.</summary>
    public static void End()
    {
        if (writer == null) return;
        try
        {
            writer.WriteLine($"# end — {linesWritten} lines");
            writer.Flush();
            writer.Dispose();
        }
        catch { /* nothing sensible to do at shutdown */ }
        writer = null;
    }

    /// <summary>Telemetry line: file only, never the console. Cheap enough to call at 10-20 Hz.</summary>
    public static void Line(string message)
    {
        if (writer == null) return;
        Write("TEL", message);
    }

    /// <summary>A visually distinct separator in the file, e.g. at the start of a charge.</summary>
    public static void Marker(string title)
    {
        if (writer == null) return;
        writer.WriteLine();
        Write("TEL", $"────────── {title} ──────────");
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (writer == null) return;

        string kind = type switch
        {
            LogType.Warning   => "WARN",
            LogType.Error     => "ERR ",
            LogType.Assert    => "ERR ",
            LogType.Exception => "EXC ",
            _                 => "LOG ",
        };
        Write(kind, condition);

        // Errors and exceptions keep the top of their stack so the culprit line is in the file.
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && !string.IsNullOrEmpty(stackTrace))
        {
            string[] frames = stackTrace.Split('\n');
            int shown = 0;
            for (int i = 0; i < frames.Length && shown < 4; i++)
            {
                string f = frames[i].TrimEnd();
                if (f.Length == 0) continue;
                writer.WriteLine("        at " + f);
                shown++;
            }
        }
    }

    private static void Write(string kind, string message)
    {
        try
        {
            writer.WriteLine($"[{Time.frameCount,6} {Time.unscaledTime,8:F3} x{Time.timeScale:F2}] {kind} {message}");
            linesWritten++;
        }
        catch { /* disk full / file gone — never let logging break the game */ }
    }

    private static void Prune(string dir, int keepNewest)
    {
        try
        {
            var files = new DirectoryInfo(dir).GetFiles("oni_*.log");
            if (files.Length < keepNewest) return;
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = keepNewest - 1; i < files.Length; i++)
                files[i].Delete();
        }
        catch { /* best effort */ }
    }
}
