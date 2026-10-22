using System;
using System.IO;
using MineLoot;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoTools
{
    private const string ScenePath = "Assets/MineLoot/Scenes/Demo.unity";

    [MenuItem("Mine Loot/Prepare demo scene")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory("Assets/MineLoot/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        var cam = camera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.067f, .094f, .14f);
        cam.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        new GameObject("Mine Loot Demo", typeof(DemoController));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "MineLootDemo";
        PlayerSettings.productName = "Mine Loot";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultScreenWidth = 480; PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.mineloot.demo");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Mine Loot/Verify game rules")]
    public static void VerifyRules()
    {
        int assertions = 0;
        void Check(bool condition, string description)
        {
            assertions++;
            if (!condition) throw new Exception("Rule check failed: " + description);
        }
        var game = new MineGame();
        for (int i = 0; i < 4; i++) Check(!game.Mine(), "No premature ticket");
        Check(game.Mine() && game.Progress.tickets == 4 && game.Progress.hits == 0 && game.Progress.coins == 1365, "Five hits grant one ticket");
        Check(game.Upgrade() && game.ClickPower == 35 && game.UpgradeCost == 560 && game.Progress.coins == 1015, "Upgrade transaction");
        Check(!new MineGame(new MineProgress { coins = 0 }).Upgrade(), "Insufficient coins");
        Check(MineGame.PickPower(0) == 1 && MineGame.PickPower(.54999) == 1 && MineGame.PickPower(.55) == 2 && MineGame.PickPower(.89999) == 2 && MineGame.PickPower(.9) == 4, "Rarity boundaries");
        game = new MineGame(new MineProgress { tickets = 7, coins = 0 });
        for (int i = 0; i < 5; i++)
        {
            var result = game.Roll(() => 0);
            Check(result.reward == (i == 4 ? 1300 : 50), "Block and chest rewards");
            foreach (int depth in game.Progress.depths) Check(depth == i + 1, "Depth persists between rolls");
        }
        Check(game.Progress.coins == 1500 && game.IsComplete, "Five chests rewarded exactly once");
        var next = game.Roll(() => .99);
        Check(next.newStratum && game.Progress.stratum == 2 && next.reward == 200 && !game.IsComplete, "Next stratum resets before mining");
        game = new MineGame(new MineProgress { depths = new[] { 5, 0, 0, 0, 0 }, coins = 0 });
        Check(game.Roll(() => 0).reward == 40, "Completed columns do not pay twice");
        game = new MineGame(new MineProgress { tickets = 0 });
        Check(game.Roll(() => 0) == null && game.Progress.coins == 1240, "Zero tickets cannot spin");
        game = new MineGame(new MineProgress { coins = -2, tickets = -2, hits = 99, upgradeLevel = 500, stratum = -1, depths = null });
        Check(game.Progress.coins == 0 && game.Progress.tickets == 0 && game.Progress.hits == 4 && game.Progress.upgradeLevel == 100 && game.Progress.stratum == 1 && game.Progress.depths.Length == 5, "Save sanitization");
        Check(!game.Upgrade(), "Upgrade cap");
        game = new MineGame(); game.Roll(() => .6); game.Mine();
        var restored = new MineGame(JsonUtility.FromJson<MineProgress>(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.coins == game.Progress.coins && restored.Progress.tickets == game.Progress.tickets && restored.Progress.depths[0] == 2 && restored.Progress.hits == 1, "Save round trip");
        try { game.Roll(() => throw new Exception("Injected RNG failure")); } catch { }
        Check(game.Progress.tickets == restored.Progress.tickets, "Failed draw cannot spend ticket");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/rules-check.json", "{\"result\":\"PASS\",\"assertions\":" + assertions + ",\"unity\":\"" + Application.unityVersion + "\"}");
        Debug.Log("MINE_LOOT_RULES: PASS (" + assertions + " assertions)");
    }

    [MenuItem("Mine Loot/Build Windows demo")]
    public static void VerifyAndBuild()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new Exception("This demo targets Unity 2022.3.47f1; current: " + Application.unityVersion);
        if (!File.Exists(ScenePath)) Prepare();
        VerifyRules();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/MineLoot.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        var summary = report.summary;
        File.WriteAllText("Logs/build-summary.json", "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors + ",\"warnings\":" + summary.totalWarnings + ",\"bytes\":" + summary.totalSize + ",\"unity\":\"" + Application.unityVersion + "\"}");
        if (summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + summary.result);
        Debug.Log("MINE_LOOT_BUILD: PASS " + summary.outputPath);
    }
}
