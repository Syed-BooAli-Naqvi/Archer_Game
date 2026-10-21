using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Lets an outside tool start the play-through test in the open editor:
// create Temp/PlayTest/run.trigger and the PlayMode test starts.
// When it finishes, Temp/PlayTest/done.txt is written with the result.
[InitializeOnLoad]
public static class PlayTestRemoteRunner
{
    static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PlayTest"));
    static readonly string Trigger = Path.Combine(Dir, "run.trigger");
    static readonly string Done = Path.Combine(Dir, "done.txt");
    static double nextCheck;

    static PlayTestRemoteRunner()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "runner.ready"), System.DateTime.Now.ToString("O"));
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        if (!File.Exists(Trigger) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // The trigger file may list test names (one per line); otherwise run the basic play-through.
        string[] names = File.ReadAllLines(Trigger).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        if (names.Length == 0) names = new[] { "ArcheryPlayTest.PlayThroughLevel" };
        File.Delete(Trigger);
        if (File.Exists(Done)) File.Delete(Done);

        Debug.Log("[PLAYTEST] Remote trigger received, running: " + string.Join(", ", names));
        ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.PlayMode,
            testNames = names
        }));
    }

    class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Done, $"{result.TestStatus}\n{result.Message}\n{result.StackTrace}");
        }
    }
}
