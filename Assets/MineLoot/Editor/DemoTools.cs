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
        // Keep the existing company/application identifiers so installed v0.1 saves remain reachable.
        PlayerSettings.productName = "Mine Loot";
        PlayerSettings.bundleVersion = "0.7.0";
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
        for (int i = 0; i < 11; i++) Check(!game.Mine(), "No premature ticket");
        Check(game.Mine() && game.Progress.tickets == 3 && game.Progress.hits == 0 && game.Progress.coins == 200, "Twelve hits grant one ticket and two coal rewards");
        Check(game.Upgrade() && game.ClickPower == 12 && game.UpgradeCost == 154 && game.Progress.coins == 80, "Upgrade transaction");
        Check(!new MineGame(new MineProgress { coins = 0 }).Upgrade(), "Insufficient coins");
        Check(MineGame.PickPower(0) == 1 && MineGame.PickPower(.54999) == 1 && MineGame.PickPower(.55) == 2 && MineGame.PickPower(.89999) == 2 && MineGame.PickPower(.9) == 4, "Rarity boundaries");
        game = new MineGame(new MineProgress { tickets = 7, coins = 0 });
        for (int i = 0; i < 5; i++)
        {
            var result = game.Roll(() => 0);
            Check(result.reward == (i == 4 ? 430 : 30), "Block, chest and floor rewards");
            foreach (int depth in game.Progress.depths) Check(depth == i + 1, "Depth persists between rolls");
        }
        Check(game.Progress.coins == 550 && game.IsComplete && game.Progress.tickets == 3, "Floor paid once and ticket returned");
        Check(game.CanClaim && game.ClaimContract() && !game.ClaimContract(), "Contract cannot pay twice");
        Check(game.Progress.coins == 650 && game.Progress.tickets == 4 && game.ContractTarget == 50, "Contract advances");
        var next = game.Roll(() => .99);
        Check(next.newStratum && game.Progress.stratum == 2 && game.Progress.boardSeed > 0 && next.reward > 0 && !game.IsComplete, "Next stratum generates a saved risk layout");
        game = new MineGame(new MineProgress { depths = new[] { 5, 0, 0, 0, 0 }, coins = 0 });
        var scrap = game.Roll(() => 0);
        Check(scrap.reward == 24 && scrap.powers[0] == 0 && scrap.chests == 0, "Completed columns never pay consolation coins");
        game = new MineGame(new MineProgress { tickets = 0 });
        Check(game.Roll(() => 0) == null && game.Progress.coins == 80, "Zero tickets cannot spin");
        game = new MineGame(new MineProgress { coins = -2, tickets = -2, hits = 99, upgradeLevel = 500, stratum = -1, depths = null });
        Check(game.Progress.coins == 0 && game.Progress.tickets == 0 && game.Progress.hits == 11 && game.Progress.upgradeLevel == 100 && game.Progress.stratum == 1 && game.Progress.depths.Length == 5, "Save sanitization");
        Check(!game.Upgrade(), "Upgrade cap");
        game = new MineGame(); game.Roll(() => .6); game.Mine();
        var restored = new MineGame(JsonUtility.FromJson<MineProgress>(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.coins == game.Progress.coins && restored.Progress.tickets == game.Progress.tickets && restored.Progress.depths[0] == 2 && restored.Progress.hits == 1, "Save round trip");
        try { game.Roll(() => throw new Exception("Injected RNG failure")); } catch { }
        Check(game.Progress.tickets == restored.Progress.tickets, "Failed draw cannot spend ticket");
        try { game.Roll(() => double.NaN); } catch (ArgumentOutOfRangeException) { }
        Check(game.Progress.tickets == restored.Progress.tickets, "Invalid random sample is atomic");
        game = new MineGame(new MineProgress { stratum = 4, coins = 1000 });
        var hit = game.Roll(() => 0);
        Check(hit.broken == 0 && game.Progress.damage[0] == 1 && game.Hardness == 2, "Hard rock keeps partial damage");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.damage[0] == 1 && restored.Roll(() => 0).broken == 5, "Partial damage survives save reload");
        Check(game.Forge() && game.Progress.forgeLevel == 1 && game.Progress.coins == 820 && game.DiamondChance == 11, "Forge cost and rarity");
        Check(game.Forge() && game.PowerBonus == 1 && MineGame.PickPower(.52, 2) == 2, "Forge changes power and boundaries");
        game = new MineGame(new MineProgress { stratum = 3, depths = new[] { 4, 4, 4, 4, 4 } });
        game.Roll(() => 0);
        Check(game.Relics == 1 && game.ClickPower == 9, "Three floors grant permanent relic");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":1,\"coins\":1240,\"tickets\":3,\"hits\":4,\"upgradeLevel\":2,\"stratum\":4,\"depths\":[1,2,3,4,5]}"));
        Check(legacy.Progress.version == 4 && legacy.Progress.boardSeed == 0 && legacy.Progress.coins == 1240 && legacy.Progress.tickets == 3 && legacy.Progress.upgradeLevel == 2 && legacy.Progress.depths[4] == 5, "v1 migration preserves progress without inserting hazards");
        game = new MineGame();
        for (int i = 0; i < 5; i++) game.Mine();
        Check(game.Progress.oreDamage == 5 && game.Progress.oresMined == 0 && game.Progress.experience == 5, "Ore accumulates damage and click experience");
        game.Mine();
        Check(game.LastHit.broken && game.LastHit.coins == 20 && game.LastHit.experience == 7 && game.Progress.coins == 140 && game.Progress.experience == 12, "Coal break pays exactly once");
        Check(game.Progress.oreDamage == 0 && game.Progress.oresMined == 1, "New block starts intact");
        game = new MineGame(new MineProgress { coins = 0, experience = 39 }); game.Mine();
        Check(game.MinerLevel == 2 && game.Progress.coins == 88 && game.Progress.tickets == 3 && game.UnlockedOres == 2, "Level unlocks iron, grants coins and ticket");
        game.Mine();
        Check(game.Progress.coins == 96 && game.Progress.tickets == 3 && game.LastHit.levels == 0, "Level reward cannot repeat");
        for (int kind = 0; kind < 5; kind++)
        {
            game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(MineGame.OreUnlockLevel(kind)), oreKind = kind });
            Check(game.UnlockedOres == kind + 1 && game.OreKey == MineGame.OreKeyAt(kind), "Ore unlock boundary " + kind);
            if (kind > 0)
            {
                game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(MineGame.OreUnlockLevel(kind)) - 1, oreKind = kind });
                Check(game.UnlockedOres == kind && game.Progress.oreKind < kind, "Locked ore rejected " + kind);
            }
        }
        int[] tiers = { 0, 2, 5, 12, 20 };
        for (int i = 0; i < tiers.Length; i++) Check(new MineGame(new MineProgress { upgradeLevel = tiers[i] }).PickTier == i, "Pick material milestone " + i);
        game = new MineGame(new MineProgress { coins = 10000, upgradeLevel = 4 });
        Check(game.MiningDamage == 1 && game.Upgrade() && game.MiningDamage == 2, "Fifth upgrade increases ore damage");
        for (int i = 0; i < 3; i++) game.Mine();
        Check(game.Progress.oresMined == 1 && game.Progress.oreDamage == 0, "Stronger pick breaks coal in three hits");
        game = new MineGame(new MineProgress { upgradeLevel = 100, experience = long.MaxValue, oreKind = 4, oreDamage = int.MaxValue });
        Check(game.MinerLevel == 50 && game.LevelGoal == 0 && game.Progress.oreDamage == 19, "Level and ore damage caps");
        game.Mine();
        Check(game.Progress.oresMined == 1 && game.LastHit.levels == 0 && game.LastHit.coins == game.ClickPower + 150, "Capped level has no repeated rewards");
        game = new MineGame(new MineProgress { experience = 200, oreKind = 2, oreDamage = 8, oresMined = 17 });
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.oreKind == 2 && restored.Progress.oreDamage == 8 && restored.Progress.experience == 200 && restored.Progress.oresMined == 17, "v3 restores ore damage and experience");
        var old = new MineGame(ProgressStore.Decode("{\"version\":2,\"coins\":543,\"tickets\":8,\"totalClicks\":180,\"upgradeLevel\":6}"));
        Check(old.MinerLevel == 4 && old.Progress.coins == 543 && old.Progress.tickets == 8 && old.Progress.upgradeLevel == 6, "v2 clicks migrate without paying historical rewards");
        var sequenceA = new MineGame(new MineProgress { experience = 1080 });
        var sequenceB = new MineGame(new MineProgress { experience = 1080 });
        bool[] seen = new bool[5];
        for (int i = 0; i < 180; i++)
        {
            sequenceA.Mine(); sequenceB.Mine(); seen[sequenceA.Progress.oreKind] = true;
            if (sequenceA.Progress.oreKind != sequenceB.Progress.oreKind) throw new Exception("Non-deterministic ore cycle");
        }
        Check(Array.TrueForAll(seen, value => value), "Unlocked cycle includes every ore");
        string testKey = "LuckyPickaxe.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(legacy, testKey);
            PlayerPrefs.SetString(testKey, "not-json");
            var backup = ProgressStore.Load(testKey);
            Check(backup.Progress.coins == 1240 && backup.Progress.depths[1] == 2, "Corrupt primary recovers backup");
        }
        finally { PlayerPrefs.DeleteKey(testKey); PlayerPrefs.DeleteKey(testKey + ".backup"); PlayerPrefs.Save(); }
        VerifyRiskRules(Check);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/rules-check.json", "{\"result\":\"PASS\",\"assertions\":" + assertions + ",\"unity\":\"" + Application.unityVersion + "\"}");
        Debug.Log("MINE_LOOT_RULES: PASS (" + assertions + " assertions)");
    }

    private static void VerifyRiskRules(Action<bool, string> check)
    {
        var game = new MineGame(new MineProgress { boardSeed = 236, coins = 0, tickets = 10 });
        for (int c = 0; c < 5; c++)
            for (int row = 1; row < 5; row++) if (game.BlockAt(c, row) == BlockKind.Obsidian) game.Progress.depths[c] = row;
        var miss = game.Roll(() => 0);
        check(miss.reward == 0 && miss.broken == 0 && miss.brokenPicks == 5 && game.Progress.tickets == 9, "All wooden picks can fail and lose the ticket");
        for (int c = 0; c < 5; c++) check(miss.after[c] == miss.before[c] && miss.damageAfter[c] == 0 && !miss.lostAfter[c], "Obsidian rejects wood without damaging or losing the lane");
        game.Progress.forgeLevel = 10;
        var iron = game.Roll(() => .6);
        check(iron.reward == 0 && iron.brokenPicks == 5, "Forged iron cannot bypass obsidian material requirement");
        var diamond = game.Roll(() => .99);
        check(diamond.broken >= 5 && diamond.brokenPicks == 0 && diamond.reward > 0, "Diamond picks break obsidian");
        game = new MineGame(new MineProgress { boardSeed = 1758, coins = 0, tickets = 3 });
        for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Bedrock) game.Progress.depths[c] = r;
        var rock = game.Roll(() => .99);
        check(rock.reward == 0 && rock.lostChests == 5 && rock.completed && !rock.perfect && game.IsComplete, "Even diamonds fail on bedrock and lose all chests");
        check(game.Progress.tickets == 2 && game.Progress.failedFloors == 1 && game.Relics == 0, "Lost floor has no refund, floor bonus or relic");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.IsComplete && restored.Progress.boardSeed == 1758 && Array.TrueForAll(restored.Progress.lostColumns, value => value), "Bedrock losses persist across reload");
        string before = JsonUtility.ToJson(restored.Progress); int draws = 0;
        try { restored.Roll(() => ++draws == 6 ? double.NaN : .6); } catch (ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(restored.Progress) == before, "Invalid next-floor seed cannot mutate completed progress");
        restored.Roll(() => .6);
        check(restored.Progress.stratum == 2 && restored.Progress.failedFloors == 1, "Failed floor advances once without farming relics");
        game = new MineGame(new MineProgress { boardSeed = 236, coins = 42, tickets = 4, stratum = 4 });
        before = JsonUtility.ToJson(game.Progress);
        try { game.Abandon(() => throw new Exception("Injected seed failure")); } catch { }
        check(JsonUtility.ToJson(game.Progress) == before, "Abandon failure is atomic");
        game.Progress.depths[0] = 1; game.Progress.damage[0] = 1;
        check(game.Abandon(() => .5) && game.Progress.tickets == 3 && game.Progress.coins == 42 && game.Progress.stratum == 4 && game.Progress.failedFloors == 0, "Changing shaft costs one ticket and cannot raise reward tier or relics");
        check(Array.TrueForAll(game.Progress.depths, value => value == 0) && Array.TrueForAll(game.Progress.damage, value => value == 0), "Changing shaft resets partial progress");
        check(!new MineGame(new MineProgress { tickets = 0 }).Abandon(() => .5), "No free rerolls without tickets");
        var old = new MineGame(ProgressStore.Decode("{\"version\":3,\"coins\":123,\"tickets\":7,\"stratum\":4,\"depths\":[1,2,3,4,5],\"damage\":[1,0,1,0,0],\"experience\":200,\"oreKind\":2,\"oreDamage\":5}"));
        check(old.Progress.boardSeed == 0 && old.Progress.depths[3] == 4 && old.Progress.damage[2] == 1 && old.Progress.coins == 123 && old.Progress.experience == 200 && old.Progress.oreDamage == 5, "v3 migration preserves damaged shaft and clicker ore");
        for (int c = 0; c < 5; c++) for (int r = 0; r < 5; r++) check(old.BlockAt(c, r) == BlockKind.Ore, "Legacy shaft remains free of inserted hazards");
        game = new MineGame(new MineProgress { boardSeed = 17, stratum = 4, forgeLevel = 0, tickets = 10 });
        int lane = -1, layer = -1;
        for (int c = 0; c < 5 && lane < 0; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Obsidian) { lane = c; layer = r; break; }
        check(lane >= 0, "Partial obsidian fixture exists");
        for (int c = 0; c < 5; c++) game.Progress.depths[c] = c == lane ? layer : 5;
        game.Progress.damage[lane] = 1;
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        var partial = restored.Roll(() => .99);
        check(partial.damageBefore[lane] == 1 && partial.after[lane] > partial.before[lane], "Obsidian partial damage survives and diamond finishes it");
        int bedrock = 0, obsidian = 0, clear = 0;
        for (int seed = 1; seed <= 1000; seed++)
        {
            var board = new MineGame(new MineProgress { boardSeed = seed });
            for (int c = 0; c < 5; c++)
            {
                int special = 0;
                for (int r = 0; r < 5; r++) { var kind = board.BlockAt(c, r); if (kind == BlockKind.Bedrock) { bedrock++; special++; } if (kind == BlockKind.Obsidian) { obsidian++; special++; } }
                if (special > 1) throw new Exception("More than one obstacle per lane");
                if (special == 0) clear++;
            }
        }
        check(bedrock > 750 && bedrock < 1050 && obsidian > 1300 && obsidian < 1700 && clear > 2300 && clear < 2900, "Risk layout distribution stays close to advertised 18/30/52");
    }

    [MenuItem("Mine Loot/Simulate balance")]
    public static void SimulateBalance()
    {
        const int runs = 100;
        var rows = new System.Text.StringBuilder("seed,minute,floor,pick_level,forge_level,miner_level,ores,coins,rolls,blocks,zero_rewards,lost_chests,reroutes\n");
        int[] checkpoints = { 60, 300, 600, 1200 };
        var sums = new double[4, 5];
        int totalMisses = 0, totalLosses = 0, totalReroutes = 0;
        for (int seed = 0; seed < runs; seed++)
        {
            var rng = new System.Random(seed);
            var game = new MineGame(); int wait = 0, emptyStreak = 0, misses = 0, losses = 0, reroutes = 0;
            for (int second = 1; second <= 1200; second++)
            {
                if (wait > 0) wait--;
                else
                {
                    for (int tap = 0; tap < 6; tap++) game.Mine();
                    if (game.CanClaim) game.ClaimContract();
                    if (game.CanForge && game.Progress.forgeLevel < Math.Min(10, 2 * game.Hardness)) game.Forge();
                    else if (game.CanUpgrade) game.Upgrade();
                    if (emptyStreak >= 3 && game.CanAbandon) { game.Abandon(() => rng.NextDouble()); reroutes++; emptyStreak = 0; }
                    else if (game.Progress.tickets > 0)
                    {
                        var roll = game.Roll(() => rng.NextDouble()); wait = 2;
                        if (roll.reward == 0) { misses++; emptyStreak++; } else emptyStreak = 0;
                        losses += roll.lostChests;
                    }
                }
                for (int i = 0; i < checkpoints.Length; i++) if (second == checkpoints[i])
                {
                    rows.AppendLine($"{seed},{second / 60},{game.Progress.stratum},{game.Progress.upgradeLevel},{game.Progress.forgeLevel},{game.MinerLevel},{game.Progress.oresMined},{game.Progress.coins},{game.Progress.totalRolls},{game.Progress.totalBlocks},{misses},{losses},{reroutes}");
                    sums[i, 0] += game.Progress.stratum; sums[i, 1] += game.Progress.upgradeLevel; sums[i, 2] += game.Progress.forgeLevel;
                    sums[i, 3] += game.MinerLevel; sums[i, 4] += game.Progress.oresMined;
                }
            }
            if (game.Progress.stratum < 10 || game.Progress.coins < 0 || game.Progress.tickets < 0 || game.UnlockedOres != 5 || game.Progress.oreDamage >= game.OreHealth) throw new Exception("Balance regression seed " + seed);
            totalMisses += misses; totalLosses += losses; totalReroutes += reroutes;
        }
        if (totalMisses == 0 || totalLosses == 0 || totalReroutes == 0) throw new Exception("Risk simulation has no actual losses or reroutes");
        Directory.CreateDirectory("Logs"); Directory.CreateDirectory("Docs");
        File.WriteAllText("Logs/balance-runs.csv", rows.ToString());
        var doc = new System.Text.StringBuilder("# Проверка темпа прогрессии 0.7\n\n100 воспроизводимых прогонов, seed 0–99, 20 минут каждый. Модель: 6 ударов в секунду вне розыгрыша, розыгрыш занимает 3 секунды. Бот забирает контракты, предпочитает кузницу до уровня min(10, 2 × прочность), затем покупает кирку при наличии монет. После трёх розыгрышей без награды меняет раскладку за жетон, без повышения пласта. Учтены обсидиан, бедрок, потерянные сундуки, отсутствие выплат в пустых проходах и бонус пласта только за пять сундуков. Это проверка экономики, не оценка поведения реальных игроков.\n\n| Минута | Средний пласт | Уровень кирки | Кузница | Уровень шахтёра | Разрушено руд |\n|---|---:|---:|---:|---:|---:|\n");
        for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} | {5:F1} |", checkpoints[i] / 60, sums[i, 0] / runs, sums[i, 1] / runs, sums[i, 2] / runs, sums[i, 3] / runs, sums[i, 4] / runs));
        doc.Append($"\nВсего за 100 прогонов: {totalMisses} розыгрышей без монет, {totalLosses} сундуков потеряно из-за бедрока, {totalReroutes} смен раскладки за жетон.\n");
        doc.Append("\nФормулы и шансы находятся в `Assets/MineLoot/Scripts/MineGame.cs`. Сырые результаты: `Logs/balance-runs.csv` после запуска меню Mine Loot → Simulate balance. Прочность растёт каждые 3 пласта до 4, награды растут до 100-го пласта. Для проверки удержания и удобства нужен плейтест.\n");
        File.WriteAllText("Docs/Balance.md", doc.ToString().Replace("\r\n", "\n"));
        Debug.Log("LUCKY_PICKAXE_BALANCE: PASS (100 seeded runs)");
    }

    [MenuItem("Mine Loot/Build Windows demo")]
    public static void VerifyAndBuild()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new Exception("This demo targets Unity 2022.3.47f1; current: " + Application.unityVersion);
        if (!File.Exists(ScenePath)) Prepare();
        PlayerSettings.bundleVersion = "0.7.0";
        VerifyRules();
        SimulateBalance();
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
