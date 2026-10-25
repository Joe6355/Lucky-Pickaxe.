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
        PlayerSettings.bundleVersion = "0.11.0";
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
        Check(legacy.Progress.version == 7 && legacy.Progress.boardSeed == 0 && legacy.Progress.coins == 1240 && legacy.Progress.tickets == 3 && legacy.Progress.upgradeLevel == 2 && legacy.Progress.depths[4] == 5, "v1 migration preserves progress without inserting hazards");
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
        VerifyExchangeRules(Check);
        VerifyUpgradeRules(Check);
        VerifyMilestoneRules(Check);
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

    private static void VerifyExchangeRules(Action<bool, string> check)
    {
        var game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, coins = 1000, totalBlocks = 25, forgeLevel = 2 });
        int draws = 0;
        check(game.BeginRoll(() => ++draws == 2 ? .99 : 0), "Paid draw enters the choice phase");
        check(draws == 5 && game.HasPendingRoll && game.Progress.tickets == 0 && game.Progress.totalRolls == 1, "Draw spends one ticket even when it was the last one");
        check(game.Progress.coins == 1000 && game.Progress.totalBlocks == 25 && game.Progress.experience == 0 && Array.TrueForAll(game.Progress.depths, d => d == 0), "No damage or reward before confirming picks");
        check(game.Progress.pendingTypes[0] == 1 && game.Progress.pendingTypes[1] == 4 && game.PendingPower(0) == 2 && game.PendingPower(1) == 5, "Pending draw stores material and includes the forge bonus");
        string pending = JsonUtility.ToJson(game.Progress);
        check(!game.BeginRoll(() => throw new Exception("Must not draw again")) && game.Roll(null) == null, "A pending paid draw cannot be replaced");
        check(!game.Upgrade() && !game.Forge() && !game.ClaimContract() && !game.Abandon(() => throw new Exception("Must not reroute")), "Other economy actions cannot alter the pending board");
        game.Mine();
        check(JsonUtility.ToJson(game.Progress) == pending, "Mining cannot change a pending transaction");
        check(!game.SwapPicks(-1, 1) && !game.SwapPicks(0, 5) && !game.SwapPicks(0, 0) && !game.SwapPicks(0, 2) && game.Progress.pendingSwapCount == 0, "Invalid and identical picks never consume the exchange");
        var restored = new MineGame(ProgressStore.Decode(pending));
        check(restored.HasPendingRoll && restored.Progress.tickets == 0 && restored.CanSwap && restored.PendingPower(1) == 5, "Reload resumes the same picks without needing another ticket");
        check(restored.SwapPicks(0, 1) && restored.Progress.pendingTypes[0] == 4 && restored.Progress.pendingTypes[1] == 1 && restored.PendingPower(0) == 5, "One exchange moves the material and full power together");
        check(restored.Progress.coins == 1000 && restored.Progress.tickets == 0 && restored.Progress.totalRolls == 1 && !restored.CanSwap, "Exchange is free and is limited to one per draw");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(restored.Progress)));
        check(restored.Progress.pendingSwapCount == 1 && !restored.SwapPicks(0, 2) && restored.Progress.pendingTypes[0] == 4, "Restart cannot undo or renew the used exchange");
        var resolved = restored.ResolveRoll();
        check(resolved != null && resolved.types[0] == 4 && resolved.powers[0] == 5 && resolved.powers[1] == 2 && !restored.HasPendingRoll && restored.Progress.tickets == 0, "Confirm resolves the arranged picks without charging another ticket");
        long coins = restored.Progress.coins;
        check(restored.ResolveRoll() == null && !restored.SwapPicks(0, 1) && restored.Progress.coins == coins, "Confirm and exchange cannot repeat a completed reward");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(restored.Progress)));
        check(!restored.HasPendingRoll && restored.ResolveRoll() == null && restored.Progress.coins == coins, "Reload after reward does not pay twice");

        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 236, tickets = 2, coins = 0 });
        for (int c = 0; c < 5; c++) for (int row = 1; row < 5; row++) if (game.BlockAt(c, row) == BlockKind.Obsidian) game.Progress.depths[c] = row;
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.SwapPicks(0, 1), "Diamond can be assigned to a revealed obsidian lane");
        resolved = game.ResolveRoll();
        check(resolved.blockedBy[0] == BlockKind.Ore && resolved.after[0] > resolved.before[0] && resolved.blockedBy[1] == BlockKind.Obsidian && resolved.brokenPicks == 4, "The arranged material determines obsidian damage and pick breakage");

        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 1758, tickets = 2 });
        game.Progress.depths[0] = 5;
        for (int r = 1; r < 5; r++) if (game.BlockAt(1, r) == BlockKind.Bedrock) game.Progress.depths[1] = r;
        game.Progress.lostColumns[1] = true;
        draws = 0; game.BeginRoll(() => ++draws == 4 ? .99 : 0);
        check(game.Progress.pendingTypes[0] == 0 && game.Progress.pendingTypes[1] == 0 && game.PendingPower(0) == 0 && game.PendingPower(1) == 0, "Closed and lost lanes have no exchangeable pick");
        check(!game.SwapPicks(0, 3) && !game.SwapPicks(1, 3) && game.SwapPicks(2, 3), "Exchange is restricted to two active lanes");
        resolved = game.ResolveRoll();
        check(resolved.powers[0] == 0 && resolved.powers[1] == 0 && resolved.reward >= 0 && game.Progress.lostColumns[1], "Exchanging cannot restore a bedrock chest");
        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 1758 });
        for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Bedrock) game.Progress.depths[c] = r;
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0); game.SwapPicks(0, 1); resolved = game.ResolveRoll();
        check(resolved.lostChests == 5 && resolved.reward == 0 && !resolved.perfect, "Exchanged diamonds still cannot break bedrock");

        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1 }); game.BeginRoll(() => 0);
        check(!game.CanSwap && game.ResolveRoll().broken == 5, "Identical picks can be confirmed without an exchange");
        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, depths = new[] { 5, 5, 5, 5, 0 } }); game.BeginRoll(() => .99);
        check(!game.CanSwap && game.PendingPower(4) == 4 && game.ResolveRoll().after[4] == 4, "Single remaining lane remains playable");
        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, depths = new[] { 5, 5, 5, 5, 5 } });
        pending = JsonUtility.ToJson(game.Progress); draws = 0;
        try { game.BeginRoll(() => ++draws == 6 ? double.NaN : .6); } catch (ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(game.Progress) == pending, "Failed next-floor draw is atomic before the choice phase");
        game.BeginRoll(() => .6); int seed = game.Progress.boardSeed;
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.stratum == 2 && restored.Progress.pendingNewStratum && restored.Progress.boardSeed == seed && restored.Progress.tickets == 0, "New-floor seed and spent last ticket survive pending reload");
        check(restored.ResolveRoll().newStratum && restored.Progress.stratum == 2, "Restoring a pending new floor cannot advance twice");
        var v4 = new MineGame(ProgressStore.Decode("{\"version\":4,\"boardSeed\":17,\"tickets\":7,\"coins\":123,\"depths\":[1,2,2,3,4],\"damage\":[0,0,0,1,0],\"lostColumns\":[false,false,true,false,false]}"));
        check(v4.Progress.version == 7 && !v4.HasPendingRoll && v4.Progress.boardSeed == 17 && v4.Progress.lostColumns[2] && v4.Progress.damage[3] == 1 && v4.Progress.coins == 123 && v4.Progress.tickets == 7, "v4 migration preserves hazards, losses and partial damage");
        string key = "LuckyPickaxe.Exchange.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1 }); draws = 0;
            game.BeginRoll(() => ++draws == 2 ? .99 : 0); ProgressStore.Save(game, key);
            restored = ProgressStore.Load(key);
            check(restored.HasPendingRoll && restored.Progress.tickets == 0 && restored.Progress.pendingTypes[1] == 4, "PlayerPrefs restores the paid choice phase");
            restored.SwapPicks(0, 1); ProgressStore.Save(restored, key);
            restored = ProgressStore.Load(key);
            check(restored.Progress.pendingSwapCount == 1 && restored.Progress.pendingTypes[0] == 4 && !restored.SwapPicks(0, 2), "PlayerPrefs persists the used exchange");
            game.Progress.pendingTypes[0] = 3; PlayerPrefs.SetString(key, JsonUtility.ToJson(game.Progress));
            restored = ProgressStore.Load(key);
            check(restored.Progress.pendingTypes[0] == 4 && restored.Progress.pendingSwapCount == 1, "Malformed pending primary recovers the fixed draw from backup");
            restored.ResolveRoll(); coins = restored.Progress.coins; ProgressStore.Save(restored, key);
            restored = ProgressStore.Load(key);
            check(!restored.HasPendingRoll && restored.ResolveRoll() == null && restored.Progress.coins == coins, "PlayerPrefs persists the final reward once");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyUpgradeRules(Action<bool, string> check)
    {
        var game = new MineGame(); int draws = 0;
        game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.Progress.swapLevel == 0 && game.Progress.pendingSwapLimit == 0 && !game.CanSwap && !game.SwapPicks(0, 1), "New players cannot exchange picks before buying the upgrade");
        check(game.ResolveRoll().broken > 0 && !game.HasPendingRoll, "Locked exchange does not prevent normal mining");
        game = new MineGame(new MineProgress { coins = 10000 });
        check(!game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 10000, "Exchange purchase requires miner level three");
        game.Progress.experience = MineGame.ExperienceForLevel(3);
        check(game.UpgradePrice(UpgradeKind.Swap) == 600 && game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 9400 && game.Progress.swapLevel == 1, "First exchange is a permanent purchase for 600 coins");
        check(!game.BuyUpgrade(UpgradeKind.Swap) && game.UpgradeUnlock(UpgradeKind.Swap) == 8, "Second exchange has its own level gate");
        game.Progress.experience = MineGame.ExperienceForLevel(8);
        check(game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 7000 && game.Progress.swapLevel == 2, "Second rank costs 2400 and grants two exchanges");
        game.Progress.experience = MineGame.ExperienceForLevel(15); game.Progress.coins = 10000;
        check(game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 400 && !game.BuyUpgrade(UpgradeKind.Swap), "Third exchange costs 9600 and is the cap");
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.Progress.pendingSwapLimit == 3 && game.SwapPicks(0, 1) && game.SwapsRemaining == 2, "Draw fixes the purchased three-exchange budget");
        check(game.SwapPicks(0, 2) && game.SwapsRemaining == 1, "Second exchange consumes one budget unit");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.pendingSwapCount == 2 && restored.SwapsRemaining == 1 && restored.SwapPicks(2, 3) && !restored.SwapPicks(3, 4), "Reload preserves spent exchanges and blocks a fourth one");
        restored.Progress.coins = 1000000; restored.Progress.experience = MineGame.ExperienceForLevel(50);
        foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) check(!restored.BuyUpgrade(kind), "No upgrade can be bought during a pending draw: " + kind);
        restored.ResolveRoll();
        check(!restored.HasPendingRoll && restored.Progress.pendingSwapCount == 0 && restored.Progress.swapLevel == 3, "Resolution clears only the per-draw budget");

        foreach (bool used in new[] { false, true })
        {
            var legacy = new MineGame(ProgressStore.Decode("{\"version\":5,\"coins\":125,\"tickets\":1,\"rollPending\":true,\"pendingTypes\":[1,4,1,1,1],\"pendingSwapUsed\":" + (used ? "true" : "false") + "}"));
            check(legacy.Progress.version == 7 && legacy.Progress.swapLevel == 0 && legacy.Progress.coins == 125 && legacy.Progress.tickets == 1 && legacy.SwapsRemaining == (used ? 0 : 1), "v5 paid draw keeps only its previously available exchange");
            legacy = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(legacy.Progress)));
            check(legacy.SwapsRemaining == (used ? 0 : 1), "Repeated migration load cannot renew spent exchanges");
            legacy.ResolveRoll(); legacy.BeginRoll(() => .6);
            check(legacy.Progress.pendingSwapLimit == 0 && !legacy.CanSwap, "Next draw requires the purchased upgrade after v5 migration");
        }
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(2) });
        check(game.BuyUpgrade(UpgradeKind.Efficiency) && game.Progress.coins == 700 && game.MiningDamage == 2, "Sharpness buys extra ore damage");
        for (int i = 0; i < 3; i++) game.Mine();
        check(game.Progress.oresMined == 1, "Sharpness reduces the hits needed to break coal");
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(4), hits = 11 });
        int tickets = game.Progress.tickets;
        check(game.BuyUpgrade(UpgradeKind.Press) && game.HitsRequired == 11 && game.Progress.hits == 0 && game.Progress.tickets == tickets + 1 && game.Progress.coins == 450, "Press purchase preserves and converts earned ticket progress");
        for (int i = 0; i < 10; i++) game.Mine();
        check(game.Progress.tickets == tickets + 1 && game.Progress.hits == 10, "Press cannot pay a ticket prematurely");
        game.Mine();
        check(game.Progress.tickets == tickets + 2 && game.Progress.hits == 0, "Press ticket repeats at the purchased interval");
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(6), depths = new[] { 4, 4, 4, 4, 4 } });
        check(game.BuyUpgrade(UpgradeKind.Treasure) && game.Progress.coins == 200 && game.ChestReward == 69, "Treasure rank adds fifteen percent to chest rewards");
        var reward = game.Roll(() => 0);
        check(reward.reward == 475 && reward.chests == 5 && reward.perfect, "Chest upgrade is applied once to each opened chest");
        game = new MineGame(new MineProgress { coins = 1000000000, experience = MineGame.ExperienceForLevel(50) });
        foreach (var kind in new[] { UpgradeKind.Swap, UpgradeKind.Efficiency, UpgradeKind.Press, UpgradeKind.Treasure })
        {
            for (int i = 0; i < MineGame.UpgradeMax(kind); i++) check(game.BuyUpgrade(kind), "Affordable rank purchase: " + kind + " " + i);
            long before = game.Progress.coins;
            check(!game.BuyUpgrade(kind) && game.Progress.coins == before && game.UpgradeLevel(kind) == MineGame.UpgradeMax(kind), "Rank cap prevents duplicate spending: " + kind);
        }
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.swapLevel == 3 && restored.MiningDamage == 11 && restored.HitsRequired == 6 && restored.ChestReward == 150 && restored.Progress.coins == game.Progress.coins, "All purchased branches survive save reload");
        game = new MineGame(new MineProgress { swapLevel = 99, efficiencyLevel = 99, pressLevel = 99, treasureLevel = 99, hits = 99 });
        check(game.Progress.swapLevel == 3 && game.MiningDamage == 11 && game.HitsRequired == 6 && game.Progress.hits == 5 && game.ChestReward == 150, "Save clamps every new upgrade and the ticket counter");
        game = new MineGame(new MineProgress { swapLevel = 1 }); draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        game.Progress.pendingSwapCount = 2; bool invalid = false;
        try { new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress))); } catch (FormatException) { invalid = true; }
        check(invalid, "Malformed exchange counters are rejected for backup recovery");
    }

    private static void VerifyMilestoneRules(Action<bool, string> check)
    {
        var game = new MineGame();
        check(game.ClaimedMilestones == 0 && game.ReadyMilestones == 0 && !game.ClaimMilestone(0), "Fresh goals cannot pay before completion");
        check(!game.ClaimMilestone(-1) && !game.ClaimMilestone(MineGame.MilestoneCount), "Invalid goal indices never change progress");
        for (int i = 0; i < 29; i++) game.Mine();
        check(game.MilestoneValue(0) == 29 && !game.CanClaimMilestone(0), "Thirty-hit goal has an exact completion threshold");
        game.Mine(); long before = game.Progress.coins; int tickets = game.Progress.tickets; long experience = game.Progress.experience;
        check(game.CanClaimMilestone(0) && game.ClaimMilestone(0) && game.Progress.coins == before + 120 && game.Progress.tickets == tickets + 1, "Goal pays its listed reward only when explicitly claimed");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.MilestoneClaimed(0) && !restored.ClaimMilestone(0) && restored.Progress.coins == game.Progress.coins && restored.Progress.experience == experience, "Claim survives reload without duplicate coins, tickets or experience");
        game = new MineGame(new MineProgress { coins = 0, tickets = 1, totalClicks = 30, totalBlocks = 50 });
        game.BeginRoll(() => .6); int claims = game.Progress.milestoneClaims;
        check(!game.ClaimMilestone(0) && !game.ClaimMilestone(4) && game.ReadyMilestones == 0 && game.Progress.coins == 0 && game.Progress.milestoneClaims == claims, "Goals cannot pay during a pending draw");
        game.ResolveRoll();
        check(game.CanClaimMilestone(0) && game.CanClaimMilestone(4) && game.Progress.milestoneClaims == 0, "Resolving a draw restores claim availability without automatic rewards");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":6,\"coins\":745,\"tickets\":4,\"totalClicks\":36,\"swapLevel\":2,\"efficiencyLevel\":3,\"pressLevel\":2,\"treasureLevel\":4,\"hits\":8,\"experience\":1000}"));
        check(legacy.Progress.version == 7 && legacy.Progress.coins == 745 && legacy.Progress.tickets == 4 && legacy.Progress.hits == 8 && legacy.Progress.swapLevel == 2 && legacy.Progress.efficiencyLevel == 3 && legacy.Progress.pressLevel == 2 && legacy.Progress.treasureLevel == 4, "v6 migration preserves the wallet and every purchased branch");
        check(legacy.ClaimedMilestones == 0 && legacy.CanClaimMilestone(0) && legacy.MilestoneValue(0) == 30, "Existing lifetime progress counts toward unclaimed goals without automatic payouts");
        legacy = new MineGame(ProgressStore.Decode("{\"version\":6,\"coins\":125,\"tickets\":0,\"totalClicks\":30,\"swapLevel\":2,\"rollPending\":true,\"pendingTypes\":[4,1,2,1,1],\"pendingSwapLimit\":2,\"pendingSwapCount\":1}"));
        check(legacy.HasPendingRoll && legacy.Progress.version == 7 && legacy.Progress.coins == 125 && legacy.Progress.tickets == 0 && legacy.Progress.swapLevel == 2 && legacy.SwapsRemaining == 1 && legacy.Progress.pendingTypes[0] == 4 && !legacy.ClaimMilestone(0), "v6 paid draw keeps its fixed picks, spent last ticket and remaining purchased exchange");
        legacy = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(legacy.Progress)));
        check(legacy.SwapsRemaining == 1 && legacy.ClaimedMilestones == 0 && legacy.ResolveRoll() != null && legacy.ResolveRoll() == null && legacy.CanClaimMilestone(0), "Repeated v7 reload cannot renew the migrated exchange budget or resolve its draw twice");
        game = new MineGame(new MineProgress { coins = 0, tickets = 0, totalClicks = 30, oresMined = 10, totalRolls = 5, upgradeLevel = 20, totalBlocks = 50, experience = MineGame.ExperienceForLevel(50), swapLevel = 1, efficiencyLevel = 3, stratum = 10, forgeLevel = 6, pressLevel = 3, treasureLevel = 5 });
        check(game.ReadyMilestones == 16 && game.Relics == 3, "Lifetime and workshop milestones all use existing progression counters");
        for (int i = 0; i < MineGame.MilestoneCount; i++)
        {
            var goal = MineGame.MilestoneAt(i);
            check(game.MilestoneValue(i) == goal.target && game.ClaimMilestone(i), "Each completed goal can be claimed: " + i);
            game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
            check(game.MilestoneClaimed(i) && !game.CanClaimMilestone(i) && !game.ClaimMilestone(i), "Each claim remains spent through reload: " + i);
        }
        check(game.ClaimedMilestones == 16 && game.ReadyMilestones == 0 && game.Progress.coins == 14140 && game.Progress.tickets == 34, "Complete book has a bounded lifetime reward of 14140 coins and 34 tickets");
        string key = "LuckyPickaxe.Milestones.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(game, key); PlayerPrefs.SetString(key, "not-json"); restored = ProgressStore.Load(key);
            check(restored.Progress.milestoneClaims == 65535 && restored.Progress.coins == 14140 && restored.Progress.tickets == 34 && !restored.ClaimMilestone(15), "Backup recovery preserves paid goal claims and prevents double rewards");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
        check(new MineGame(new MineProgress { milestoneClaims = -1 }).ClaimedMilestones == 0 && new MineGame(new MineProgress { milestoneClaims = (1 << 23) | 1 }).Progress.milestoneClaims == 1, "Save validation removes negative claims and unknown future bits");
        game = new MineGame(new MineProgress { coins = MineGame.CoinLimit, tickets = 1000000, totalClicks = long.MaxValue });
        check(game.MilestoneValue(0) == 30 && game.ClaimMilestone(0) && game.Progress.coins == MineGame.CoinLimit && game.Progress.tickets == 1000000, "Goal progress display and rewards respect existing currency caps");
    }

    [MenuItem("Mine Loot/Simulate balance")]
    public static void SimulateBalance()
    {
        const int runs = 100;
        var rows = new System.Text.StringBuilder("strategy,seed,minute,floor,pick_level,forge_level,miner_level,ores,coins,rolls,blocks,zero_rewards,lost_chests,reroutes,exchanges,swap_level,efficiency_level,press_level,treasure_level,milestones\n");
        int[] checkpoints = { 60, 300, 600, 1200 };
        var sums = new double[2, 4, 10];
        var totalMisses = new int[2]; var totalLosses = new int[2]; var totalReroutes = new int[2]; var totalExchanges = new int[2];
        for (int run = 0; run < runs * 2; run++)
        {
            int strategy = run / runs, seed = run % runs;
            var rng = new System.Random(seed);
            var game = new MineGame(); int wait = 0, emptyStreak = 0, misses = 0, losses = 0, reroutes = 0, exchanges = 0;
            for (int second = 1; second <= 1200; second++)
            {
                if (wait > 0) wait--;
                else
                {
                    for (int tap = 0; tap < 6; tap++) game.Mine();
                    if (game.CanClaim) game.ClaimContract();
                    for (int goal = 0; goal < MineGame.MilestoneCount; goal++) game.ClaimMilestone(goal);
                    if (strategy == 0 || !BuyAdditionalUpgrade(game))
                    {
                        if (game.CanForge && game.Progress.forgeLevel < Math.Min(10, 2 * game.Hardness)) game.Forge();
                        else if (game.CanUpgrade) game.Upgrade();
                    }
                    if (emptyStreak >= 3 && game.CanAbandon) { game.Abandon(() => rng.NextDouble()); reroutes++; emptyStreak = 0; }
                    else if (game.Progress.tickets > 0)
                    {
                        game.BeginRoll(() => rng.NextDouble());
                        while (strategy == 1 && ExchangeForVisibleObsidian(game)) exchanges++;
                        var roll = game.ResolveRoll(); wait = 2;
                        if (roll.reward == 0) { misses++; emptyStreak++; } else emptyStreak = 0;
                        losses += roll.lostChests;
                    }
                }
                for (int i = 0; i < checkpoints.Length; i++) if (second == checkpoints[i])
                {
                    rows.AppendLine($"{strategy},{seed},{second / 60},{game.Progress.stratum},{game.Progress.upgradeLevel},{game.Progress.forgeLevel},{game.MinerLevel},{game.Progress.oresMined},{game.Progress.coins},{game.Progress.totalRolls},{game.Progress.totalBlocks},{misses},{losses},{reroutes},{exchanges},{game.Progress.swapLevel},{game.Progress.efficiencyLevel},{game.Progress.pressLevel},{game.Progress.treasureLevel},{game.ClaimedMilestones}");
                    sums[strategy, i, 0] += game.Progress.stratum; sums[strategy, i, 1] += game.Progress.upgradeLevel; sums[strategy, i, 2] += game.Progress.forgeLevel;
                    sums[strategy, i, 3] += game.MinerLevel; sums[strategy, i, 4] += game.Progress.oresMined;
                    sums[strategy, i, 5] += game.Progress.swapLevel; sums[strategy, i, 6] += game.Progress.efficiencyLevel;
                    sums[strategy, i, 7] += game.Progress.pressLevel; sums[strategy, i, 8] += game.Progress.treasureLevel;
                    sums[strategy, i, 9] += game.ClaimedMilestones;
                }
            }
            if (game.Progress.stratum < 10 || game.Progress.coins < 0 || game.Progress.tickets < 0 || game.UnlockedOres != 5 || game.Progress.oreDamage >= game.OreHealth || game.HasPendingRoll) throw new Exception("Balance regression strategy " + strategy + ", seed " + seed);
            totalMisses[strategy] += misses; totalLosses[strategy] += losses; totalReroutes[strategy] += reroutes; totalExchanges[strategy] += exchanges;
        }
        for (int s = 0; s < 2; s++) if (totalMisses[s] == 0 || totalLosses[s] == 0 || totalReroutes[s] == 0 || s == 1 && totalExchanges[s] == 0) throw new Exception("Risk simulation has no actual losses or exchanges");
        Directory.CreateDirectory("Logs"); Directory.CreateDirectory("Docs");
        File.WriteAllText("Logs/balance-runs.csv", rows.ToString());
        var doc = new System.Text.StringBuilder("# Проверка темпа прогрессии 0.11\n\nПо 100 воспроизводимых прогонов, seed 0–99, 20 минут на каждую стратегию. Модель: 6 ударов в секунду вне розыгрыша, розыгрыш занимает 3 секунды. Время на размышление игрока не учитывается. Обе стратегии забирают контракты и готовые разовые награды книги целей (не более 14140 монет и 34 жетонов за всё прохождение). Базовый приоритет покупок: кузница до уровня min(10, 2 × прочность), затем кирка при наличии монет. После трёх розыгрышей без награды бот меняет раскладку за жетон, без повышения пласта. Учтены обсидиан, бедрок, потерянные сундуки, отсутствие выплат в пустых проходах и бонус пласта только за пять сундуков. Это проверка экономики, не оценка поведения реальных игроков.\n\nСтратегия 0 покупает только кирку и кузницу, обмен закрыт. Стратегия 1 покупает новые ветки за монеты с соблюдением уровней: перестановку до 3, остроту до min(10, уровень/4), пресс до min(6, уровень/5), сокровища до min(10, уровень/5). Если новая покупка недоступна, действует базовый приоритет кузницы и кирки. Использует до купленного лимита обменов, передавая алмаз на раскрытый обсидиан. Скрытые слои бот не читает.\n");
        for (int s = 0; s < 2; s++)
        {
            doc.Append(s == 0 ? "\n## Без обмена\n" : "\n## Все ветки прокачки\n");
            doc.Append("\n| Минута | Средний пласт | Уровень кирки | Кузница | Уровень шахтёра | Разрушено руд | Наград за цели |\n|---|---:|---:|---:|---:|---:|---:|\n");
            for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} | {5:F1} | {6:F1} |", checkpoints[i] / 60, sums[s, i, 0] / runs, sums[s, i, 1] / runs, sums[s, i, 2] / runs, sums[s, i, 3] / runs, sums[s, i, 4] / runs, sums[s, i, 9] / runs));
            doc.Append($"\nВсего за 100 прогонов: {totalMisses[s]} розыгрышей без монет, {totalLosses[s]} сундуков потеряно из-за бедрока, {totalReroutes[s]} смен раскладки за жетон, {totalExchanges[s]} обменов кирок.\n");
            if (s == 1)
            {
                doc.Append("\n| Минута | Перестановка | Острота | Пресс | Сокровища |\n|---|---:|---:|---:|---:|\n");
                for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} |", checkpoints[i] / 60, sums[s, i, 5] / runs, sums[s, i, 6] / runs, sums[s, i, 7] / runs, sums[s, i, 8] / runs));
            }
        }
        doc.Append("\nФормулы и шансы находятся в `Assets/MineLoot/Scripts/MineGame.cs`. Сырые результаты: `Logs/balance-runs.csv` после запуска меню Mine Loot → Simulate balance. Прочность растёт каждые 3 пласта до 4, награды растут до 100-го пласта. Для проверки удержания и удобства нужен плейтест.\n");
        File.WriteAllText("Docs/Balance.md", doc.ToString().Replace("\r\n", "\n"));
        Debug.Log("LUCKY_PICKAXE_BALANCE: PASS (200 seeded runs, two exchange strategies)");
    }

    private static bool BuyAdditionalUpgrade(MineGame game)
    {
        foreach (var kind in new[] { UpgradeKind.Swap, UpgradeKind.Efficiency, UpgradeKind.Press, UpgradeKind.Treasure })
        {
            int desired = kind == UpgradeKind.Swap ? 3 : kind == UpgradeKind.Efficiency ? Math.Min(10, game.MinerLevel / 4) : kind == UpgradeKind.Press ? Math.Min(6, game.MinerLevel / 5) : Math.Min(10, game.MinerLevel / 5);
            if (game.UpgradeLevel(kind) < desired && game.BuyUpgrade(kind)) return true;
        }
        return false;
    }

    private static bool ExchangeForVisibleObsidian(MineGame game)
    {
        if (!game.CanSwap) return false;
        for (int target = 0; target < MineGame.Columns; target++)
        {
            if (!game.IsActiveColumn(target) || game.Progress.pendingTypes[target] == 4 || game.BlockAt(target, game.Progress.depths[target]) != BlockKind.Obsidian) continue;
            for (int donor = 0; donor < MineGame.Columns; donor++)
                if (game.CanSwapPicks(target, donor) && game.Progress.pendingTypes[donor] == 4 && game.BlockAt(donor, game.Progress.depths[donor]) == BlockKind.Ore) return game.SwapPicks(target, donor);
        }
        return false;
    }

    [MenuItem("Mine Loot/Build Windows demo")]
    public static void VerifyAndBuild()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new Exception("This demo targets Unity 2022.3.47f1; current: " + Application.unityVersion);
        if (!File.Exists(ScenePath)) Prepare();
        PlayerSettings.bundleVersion = "0.11.0";
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
