using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MineLoot
{
    // Invoked only with -demoSmokeTest; never reads or writes the player's save.
    public sealed class DemoSmokeCheck : MonoBehaviour
    {
        private int errors;
        private string output;
        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-demoOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            var demo = GetComponent<DemoController>();
            Screen.SetResolution(480, 960, false);
            yield return new WaitForSecondsRealtime(.8f);
            Canvas.ForceUpdateCanvases();
            yield return VerifyPlatform(demo);
            yield return VerifyGuide(demo);
            demo.RestoreForSmoke(new MineProgress());
            yield return new WaitForSecondsRealtime(.8f);
            Check(Resources.Load<Texture2D>("Art/mine-cavern-v3") == null && Resources.Load<Texture2D>("Art/ore-hero-v3") == null && Resources.Load<Texture2D>("Art/items-atlas-v3") == null, "Painted assets excluded");
            Check(Resources.Load<Font>("Fonts/Tiny5-Regular").HasCharacter('Д') && Resources.Load<Font>("Fonts/Tiny5-Regular").HasCharacter('Я'), "Pixel Cyrillic font packaged");
            var stage = FindObjectOfType<VoxelStage>();
            VoxelStage shaft = null;
            foreach (var candidate in GetComponentsInChildren<VoxelStage>(true)) if (candidate.IsExpedition) shaft = candidate;
            Check(shaft != null && !shaft.gameObject.activeSelf && shaft.VisibleBlocks == 25, "Voxel shaft is prepared and sleeps on mining page");
            Vector3 dimensions = shaft.BlockDimensions;
            Check(Mathf.Abs(dimensions.x - dimensions.y) < .001f && Mathf.Abs(dimensions.y - dimensions.z) < .001f, "Ore geometry is cubic without compressed texture axes");
            Check(shaft.Target.width == 432 && shaft.Target.height == 417 && shaft.Target.filterMode == FilterMode.Point, "Shaft renders at native UI pixels with point filtering");
            Check(stage != null && stage.Target != null && stage.Target.IsCreated() && stage.StaticBatches < 24 && stage.OreVertices == 24, "Voxel scene renders in material batches");
            stage.RenderNow();
            var previousTarget = RenderTexture.active;
            RenderTexture.active = stage.Target;
            var scenePixels = new Texture2D(stage.Target.width,stage.Target.height,TextureFormat.RGB24,false);
            scenePixels.ReadPixels(new Rect(0,0,stage.Target.width,stage.Target.height),0,0); scenePixels.Apply();
            int greenPixels=0, neutralPixels=0;
            for(int y=0;y<scenePixels.height;y+=8) for(int x=0;x<scenePixels.width;x+=8)
            {
                Color c=scenePixels.GetPixel(x,y);
                if(c.g>c.r*1.2f && c.g>c.b*1.15f) greenPixels++;
                if(Mathf.Abs(c.r-c.g)<.08f && Mathf.Abs(c.g-c.b)<.08f && c.grayscale>.15f) neutralPixels++;
            }
            Check(greenPixels>200 && neutralPixels>50, "Rendered terrain and ore are visible");
            Destroy(scenePixels); RenderTexture.active=previousTarget;
            CheckLayout();
            Capture("unity-clicker.png");
            yield return new WaitForEndOfFrame();
            Check(demo.Game.Progress.coins == 80, "Initial wallet");
            for (int i = 0; i < 12; i++)
            {
                Click("Mine rock");
                if (i == 5) Check(stage.IsReplacing && demo.Game.LastHit.broken, "Destruction triggers replacement animation");
                yield return new WaitForSecondsRealtime(.085f);
                if (i == 4)
                {
                    Check(stage.CrackVisible, "Damage appears on the 3D block");
                    int rewards = 0;
                    foreach (var text in GetComponentsInChildren<Text>(false)) if (text.name == "Mining rewards") rewards++;
                    Check(rewards == 1, "Fast clicks aggregate into one readable reward");
                    Capture("unity-ore-damage.png");
                }
                if (i == 5) Capture("unity-ore-break.png");
            }
            Check(demo.Game.Progress.coins == 200 && demo.Game.Progress.tickets == 3 && demo.Game.Progress.oresMined == 2, "Clicker, ore rewards and ticket");
            Click("Upgrade pickaxe");
            Check(demo.Game.ClickPower == 12 && demo.Game.Progress.coins == 80, "Upgrade button");
            for (int i = 0; i < 12; i++) { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            Click("Upgrade forge");
            Check(demo.Game.Progress.forgeLevel == 1 && demo.Game.Progress.coins == 148, "Forge button");
            Check(demo.Game.MinerLevel == 2 && demo.Game.UnlockedOres == 2 && demo.Game.Progress.tickets == 5, "Level unlock and reward through UI clicks");
            yield return new WaitForSecondsRealtime(.3f);
            Check(stage.DisplayedOre == "tex-iron", "Newly unlocked iron appears after block replacement");
            Capture("unity-level-up.png");
            Button rockButton = null;
            foreach (var button in GetComponentsInChildren<Button>()) if (button.name == "Mine rock") rockButton = button;
            var raycastPointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rockButton.GetComponent<RectTransform>().position) };
            var raycastHits = new List<RaycastResult>(); EventSystem.current.RaycastAll(raycastPointer, raycastHits);
            Check(raycastHits.Count > 0 && raycastHits[0].gameObject == rockButton.gameObject, "Visible ore receives pointer input");
            long heldStart = demo.Game.Progress.totalClicks;
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -7, position = raycastPointer.position };
            // Isolate synthetic pointer events from the desktop cursor while running in a hidden window.
            var inputModule = EventSystem.current.currentInputModule; bool inputEnabled = inputModule.enabled;
            inputModule.enabled = false;
            float holdStarted = Time.realtimeSinceStartup;
            ExecuteEvents.Execute(rockButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            while (demo.Game.Progress.totalClicks - heldStart < 3 && Time.realtimeSinceStartup - holdStarted < 2) yield return null;
            ExecuteEvents.Execute(rockButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            long beforeRelease = demo.Game.Progress.totalClicks;
            rockButton.onClick.Invoke();
            inputModule.enabled = inputEnabled;
            long heldCount = demo.Game.Progress.totalClicks - heldStart;
            Check(heldCount == 3 && beforeRelease == demo.Game.Progress.totalClicks && Time.realtimeSinceStartup - holdStarted >= .5f, "Hold repeats at a controlled rate without release bonus: " + heldCount);
            Click("Toggle sound"); Check(GetComponent<GameAudio>().Muted, "Mute sound");
            Click("РУЛЕТКА ШАХТЁРА");
            Check(!stage.gameObject.activeSelf, "Voxel camera stops on roulette screen");
            yield return null;
            Check(shaft.gameObject.activeSelf && shaft.Target.IsCreated(), "Dedicated shaft camera starts on roulette page");
            CheckLayout();
            Click("Toggle speed"); Check(demo.FastRoll, "Fast animation mode");
            yield return null;
            Capture("unity-roulette.png");
            yield return new WaitForEndOfFrame();
            int ticketsBeforeSpin = demo.Game.Progress.tickets;
            long coinsBeforeSpin = demo.Game.Progress.coins;
            Click("ЗАПУСТИТЬ КИРКИ");
            Check(demo.IsBusy && demo.Game.Progress.tickets == ticketsBeforeSpin - 1, "Spin starts once");
            demo.Spin();
            Check(demo.Game.Progress.tickets == ticketsBeforeSpin - 1, "Double spin blocked");
            float deadline = Time.realtimeSinceStartup + 8;
            int settled = 0;
            while (demo.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                Check(demo.SettledReels >= settled, "Reels stop in one direction"); settled = demo.SettledReels;
                Check(!demo.AwaitingPickChoice, "Unpurchased exchange never opens the choice phase");
                if (demo.RollPhase == "reels") Check(demo.Game.Progress.coins == coinsBeforeSpin && demo.Game.Progress.depths[0] == 0, "Locked draw still waits for reels before committing damage");
                yield return null;
            }
            int[] committed = (int[])demo.Game.Progress.depths.Clone();
            Check(!demo.Game.HasPendingRoll && demo.Game.Progress.swapLevel == 0 && demo.Game.Progress.tickets == ticketsBeforeSpin - 1, "Unpurchased exchange resolves automatically with one ticket");
            Check(!demo.IsBusy, "Animation completes");
            Check(demo.SettledReels == 5 && demo.RollPhase == "result", "All five reels settle before final reward");
            int remaining = 25; foreach (int depth in committed) remaining -= depth;
            Check(shaft.VisibleBlocks == remaining, "3D blocks match committed depths");
            yield return new WaitForSecondsRealtime(.2f);
            for (int i = 0; i < 5; i++) Check(demo.Game.Progress.depths[i] == committed[i] && committed[i] > 0, "Depth retained");
            Click("ШАНСЫ И ПРАВИЛА");
            yield return null;
            Check(GameObject.Find("Rules modal") != null, "Rules modal");
            CheckLayout();
            Capture("unity-rules.png");
            Click("ПОНЯТНО");
            yield return null;
            Capture("unity-roll-result.png");
            demo.Game.Progress.totalBlocks = 25;
            // Refresh via changing the sound setting, then test the ready contract action.
            Click("Toggle sound"); long previousCoins = demo.Game.Progress.coins;
            Click("Claim contract");
            Check(demo.Game.Progress.claimedContracts == 1 && demo.Game.Progress.coins == previousCoins + 100, "Claim once");
            yield return new WaitForSecondsRealtime(.3f);
            yield return VerifyWorkshop(demo, stage, shaft);
            yield return VerifyProduction(demo, stage);
            yield return VerifyOffline(demo);
            yield return VerifyRewardedOffline(demo);
            yield return VerifyResponsiveLayout(demo, stage, shaft);
            if (Array.IndexOf(args, "-demoStoreCapture") >= 0) yield return RecordDesktopDemo(demo);
            yield return VerifyPrestige(demo);
            yield return VerifyMilestones(demo);
            // Exercise a later biome and partial block damage in the same built player.
            demo.Game.Progress.stratum = 4;
            Array.Clear(demo.Game.Progress.depths, 0, 5); Array.Clear(demo.Game.Progress.damage, 0, 5);
            Array.Clear(demo.Game.Progress.lostColumns, 0, 5);
            demo.Spin();
            yield return FinishRoll(demo);
            yield return new WaitForSecondsRealtime(.25f);
            Capture("unity-crystal-shaft.png");
            demo.Game.Progress.stratum = 7; demo.Game.Progress.tickets = 1;
            Array.Clear(demo.Game.Progress.depths, 0, 5); Array.Clear(demo.Game.Progress.damage, 0, 5);
            Array.Clear(demo.Game.Progress.lostColumns, 0, 5);
            demo.Spin();
            yield return FinishRoll(demo);
            yield return new WaitForSecondsRealtime(.25f);
            Capture("unity-ember-shaft.png");
            // Record a complete full-speed expedition, including the last chest and the next-floor action.
            demo.Game.Progress.stratum = 1; demo.Game.Progress.tickets = 2;
            demo.Game.Progress.boardSeed = 0; demo.Game.Progress.failedFloors = 0;
            int previousForge = demo.Game.Progress.forgeLevel; demo.Game.Progress.forgeLevel = 10;
            Array.Clear(demo.Game.Progress.depths, 0, 5); Array.Clear(demo.Game.Progress.damage, 0, 5);
            Array.Clear(demo.Game.Progress.lostColumns, 0, 5);
            Click("Toggle speed"); Check(!demo.FastRoll, "Normal-speed expedition");
            demo.Spin();
            int rollFrame = 0; var rollTimes = new System.Text.StringBuilder();
            deadline = Time.realtimeSinceStartup + 10;
            while ((demo.IsBusy || rollFrame < 8) && Time.realtimeSinceStartup < deadline)
            {
                if (demo.AwaitingPickChoice) demo.Spin();
                rollTimes.AppendLine(Time.realtimeSinceStartup.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                Capture("unity-roulette-animation-" + (rollFrame++).ToString("000") + ".png");
                yield return new WaitForSecondsRealtime(.04f);
            }
            File.WriteAllText(Path.Combine(output, "roulette-animation-times.txt"), rollTimes.ToString());
            Check(!demo.IsBusy && demo.Game.IsComplete && shaft.VisibleBlocks == 0 && shaft.OpenChests == 5, "Five 3D chests open and floor completes");
            yield return new WaitForSecondsRealtime(.4f);
            CheckLayout(); Capture("unity-roulette-complete.png");
            demo.Game.Progress.forgeLevel = previousForge;
            demo.Spin(); yield return FinishRoll(demo);
            Check(demo.Game.Progress.stratum == 2 && shaft.VisibleBlocks > 0 && shaft.OpenChests < 5, "Next floor restores shaft geometry and closes chests");
            // Controlled inputs exercise a genuine loss through the same model and presentation.
            demo.Game.Progress.stratum = 1; demo.Game.Progress.boardSeed = 236; demo.Game.Progress.tickets = 4;
            demo.Game.Progress.forgeLevel = 0; demo.Game.Progress.failedFloors = 0;
            Array.Clear(demo.Game.Progress.damage, 0, 5); Array.Clear(demo.Game.Progress.lostColumns, 0, 5);
            for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (demo.Game.BlockAt(c, r) == BlockKind.Obsidian) demo.Game.Progress.depths[c] = r;
            previousCoins = demo.Game.Progress.coins;
            demo.SpinForSmoke(() => 0);
            yield return RecordRoll(demo, "roulette-loss");
            Check(demo.Game.Progress.coins == previousCoins && demo.Game.Progress.tickets == 3 && !demo.Game.IsComplete, "Obsidian loses the ticket without money or free progress");
            CheckLayout(); Capture("unity-roulette-loss.png");
            Click("Abandon floor");
            Check(demo.Game.Progress.stratum == 1 && demo.Game.Progress.tickets == 2 && shaft.VisibleBlocks == 25 && demo.Game.Progress.coins == previousCoins, "Changing shaft costs a ticket and keeps reward tier unchanged");
            demo.Game.Progress.boardSeed = 1758; demo.Game.Progress.stratum = 1;
            Array.Clear(demo.Game.Progress.damage, 0, 5); Array.Clear(demo.Game.Progress.lostColumns, 0, 5);
            for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (demo.Game.BlockAt(c, r) == BlockKind.Bedrock) demo.Game.Progress.depths[c] = r;
            demo.SpinForSmoke(() => .99);
            yield return RecordRoll(demo, "roulette-bedrock");
            Check(demo.Game.IsComplete && !demo.Game.IsPerfect && shaft.OpenChests == 0 && shaft.VisibleBlocks == 5 && demo.Game.Progress.tickets == 1, "Bedrock stops diamonds and loses all chests without ticket refund");
            CheckLayout(); Capture("unity-roulette-bedrock.png");
            // A controlled last-ticket draw tests real pointer input, exchange animation and reload.
            demo.RestoreForSmoke(new MineProgress { swapLevel = 1, boardSeed = 17, tickets = 1, coins = 0, depths = new[] { 0, 0, 0, 3, 0 } });
            demo.ShowPage(true); int exchangeDraws = 0;
            demo.SpinForSmoke(() => ++exchangeDraws == 1 ? .99 : 0);
            yield return RecordRoll(demo, "roulette-exchange", true);
            Check(!demo.Game.HasPendingRoll && demo.Game.Progress.tickets == 0 && demo.Game.Progress.depths[3] == 5 && demo.Game.Progress.depths[0] == 1 && shaft.OpenChests == 1, "Exchanged diamond opens the obsidian chest and the wooden donor mines one block");
            Capture("unity-roulette-exchange-result.png");
            demo.ShowPage(false);
            demo.Game.Progress.experience = MineGame.ExperienceForLevel(10);
            demo.Game.Progress.upgradeLevel = 12;
            demo.Game.Progress.oreKind = 4; demo.Game.Progress.oreDamage = 10;
            demo.ToggleSound();
            yield return new WaitForSecondsRealtime(2.9f);
            Check(stage.DisplayedOre == "tex-emerald" && stage.CrackVisible && demo.Game.PickTier == 3 && demo.Game.MiningDamage == 3, "Emerald and golden pick are presented with damage");
            CheckLayout(); Capture("unity-progression.png");
            long minedBefore = demo.Game.Progress.oresMined;
            var frameTimes = new System.Text.StringBuilder();
            for (int frame = 0; frame < 36; frame++)
            {
                if (frame == 2 || frame == 6 || frame == 10 || frame == 14) Click("Mine rock");
                frameTimes.AppendLine(Time.realtimeSinceStartup.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                Capture("unity-animation-" + frame.ToString("00") + ".png");
                yield return new WaitForSecondsRealtime(.04f);
            }
            File.WriteAllText(Path.Combine(output, "animation-times.txt"), frameTimes.ToString());
            Check(demo.Game.Progress.oresMined == minedBefore + 1, "Upgraded pick breaks the damaged emerald");
            demo.Game.Progress.upgradeLevel = 19; demo.Game.Progress.coins = demo.Game.UpgradeCost;
            demo.ToggleSound(); Click("Upgrade pickaxe");
            Check(demo.Game.PickTier == 4 && demo.Game.MiningDamage == 5, "Twentieth upgrade unlocks diamond pick and damage milestone");
            yield return new WaitForSecondsRealtime(.3f);
            Capture("unity-diamond-pick.png");
            yield return VerifyDevelopment(demo, stage);
            demo.Game.Progress.totalBlocks = 25000000; demo.Game.Progress.claimedContracts = 999999;
            demo.ToggleSound();
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.4f);
                CheckLayout();
                Capture("unity-" + size.x + "x" + size.y + ".png");
                demo.ShowPage(true); yield return null; CheckLayout();
                demo.ShowWorkshop(); yield return null; CheckLayout(); Capture("unity-workshop-" + size.x + "x" + size.y + ".png");
                Click("Production section"); yield return null; CheckLayout(); Capture("unity-production-" + size.x + "x" + size.y + ".png");
                Click("Equipment section"); yield return null;
                Click("Open milestones"); yield return null;
                for (int page = 0; page < 4; page++)
                {
                    CheckLayout();
                    if (page < 3) Click("Milestones next");
                }
                Capture("unity-milestones-" + size.x + "x" + size.y + ".png");
                Click("Close milestones"); yield return null;
                demo.ShowPage(false); yield return null;
            }
            demo.RestoreForSmoke(new MineProgress { swapLevel = 1, tickets = 1 });
            demo.ShowPage(true); int layoutDraws = 0;
            demo.SpinForSmoke(() => ++layoutDraws == 2 ? .99 : 0);
            deadline = Time.realtimeSinceStartup + 5;
            while (!demo.AwaitingPickChoice && Time.realtimeSinceStartup < deadline) yield return null;
            Check(demo.AwaitingPickChoice, "Choice phase available for viewport checks");
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.4f);
                CheckLayout();
                Click("Reel 0");
                Check(demo.SelectedPick == 0 && demo.AwaitingPickChoice, "Choice selection works at " + size);
                Click("Reel 0");
                Check(demo.SelectedPick == -1 && demo.Game.Progress.pendingSwapCount == 0, "Deselect keeps the exchange available at " + size);
                Capture("unity-choice-" + size.x + "x" + size.y + ".png");
            }
            demo.Spin(); yield return FinishRoll(demo); demo.ShowPage(false);
            Screen.SetResolution(960, 540, false);
            yield return new WaitForSecondsRealtime(.6f);
            Capture("unity-landscape.png");
            yield return new WaitForSecondsRealtime(.4f);
            File.WriteAllText(Path.Combine(output, "runtime-check.json"), "{\"result\":\"" + (errors == 0 ? "PASS" : "FAIL") + "\",\"errors\":" + errors + ",\"unity\":\"" + Application.unityVersion + "\"}");
            Debug.Log("MINE_LOOT_RUNTIME_CHECK: " + (errors == 0 ? "PASS" : "FAIL"));
            Application.Quit(errors == 0 ? 0 : 1);
        }

        private IEnumerator VerifyProduction(DemoController demo, VoxelStage stage)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            demo.RestoreForSmoke(new MineProgress()); demo.ShowWorkshop(); Click("Production section");
            Check(!FindButton("Buy Worker").interactable && !FindButton("Buy Drill").interactable && FindButton("Buy Pickaxe") == null, "Production reuses workshop slots and respects initial level locks");
            CheckLayout(); Capture("unity-production-locked.png");
            demo.RestoreForSmoke(new MineProgress { coins = 10000, experience = MineGame.ExperienceForLevel(8) });
            demo.ShowWorkshop(); Click("Production section");
            Check(FindButton("Buy Worker").interactable && !FindButton("Buy Drill").interactable, "Worker is available while dependent machinery is locked");
            Click("Buy Worker"); Click("Buy Drill"); Click("Buy Cart"); Click("Buy Rails");
            Check(demo.Game.Progress.coins == 5600 && demo.Game.ProductionMilli == 15680 && stage.VisibleWorkers == 1 && stage.DrillVisible, "Real buttons buy the production chain at its listed prices and reveal machinery");
            int tickets = demo.Game.Progress.tickets; long experience = demo.Game.Progress.experience, clicks = demo.Game.Progress.totalClicks;
            demo.AdvanceForSmoke(10, false);
            Check(demo.Game.Progress.coins == 5756 && demo.Game.Progress.totalProduced == 156 && demo.Game.Progress.tickets == tickets && demo.Game.Progress.experience == experience && demo.Game.Progress.totalClicks == clicks, "Clock credits passive coins without generating tickets or mining progress");
            CheckLayout(); Capture("unity-production.png");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); demo.ShowWorkshop(); Click("Production section");
            Check(Math.Abs(demo.Game.Progress.productionCarry - .8) < .000001 && demo.Game.Progress.coins == 5756 && demo.Game.Progress.workerCount == 1 && demo.Game.Progress.railLevel == 1, "Workshop reload restores purchased machines and the saved fractional coin");
            demo.ShowPage(false); float drillBefore = stage.DrillAngle;
            yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(Mathf.DeltaAngle(stage.DrillAngle, drillBefore)) > 1, "Purchased drill spindle animates in the mining world");
            CheckLayout(); Capture("unity-production-world.png");
            demo.RestoreForSmoke(new MineProgress { coins = 0, workerCount = 1 });
            demo.UseLiveClockForSmoke(true); yield return new WaitForSecondsRealtime(1.2f); demo.UseLiveClockForSmoke(false);
            Check(demo.Game.Progress.coins >= 2 && demo.Game.Progress.coins <= 5 && demo.Game.Progress.totalClicks == 0 && demo.Game.Progress.tickets == 2, "Player Update drives passive income without synthetic clicks");
            demo.RestoreForSmoke(new MineProgress { coins = 1000, oreDamage = 5, veinCharge = 23 });
            yield return new WaitForSecondsRealtime(.1f); Click("Mine rock");
            Check(demo.Game.VeinOffered && FindButton("Activate rich vein") != null && stage.VeinVisible, "Breaking the qualifying ore reveals the rich vein button and world glints");
            yield return new WaitForSecondsRealtime(.3f); CheckLayout(); Capture("unity-rich-vein-offer.png");
            demo.AdvanceForSmoke(3, true); demo.ShowWorkshop(); demo.AdvanceForSmoke(5, false);
            Check(demo.Game.Progress.veinOfferLeft == 17, "Workshop time does not consume a manual mining opportunity");
            demo.ShowPage(false); Click("Activate rich vein");
            Check(demo.Game.VeinActive && !demo.Game.VeinOffered && FindButton("Activate rich vein") == null && stage.VeinVisible, "Claiming the vein replaces its button with a live boost timer");
            long coins = demo.Game.Progress.coins;
            yield return new WaitForSecondsRealtime(.1f); Click("Mine rock");
            Check(demo.Game.LastHit.rich && demo.Game.LastHit.coins == 24 && demo.Game.Progress.coins == coins + 24, "Real mining input applies the manual coin multiplier");
            demo.AdvanceForSmoke(4.5, true);
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
            Check(demo.Game.Progress.veinBoostLeft == 7.5 && demo.Game.ManualMultiplier == 3, "Reload keeps exact remaining boost time");
            yield return new WaitForSecondsRealtime(.2f);
            CheckLayout(); Capture("unity-rich-vein-active.png");
            demo.AdvanceForSmoke(7.5, true);
            Check(!demo.Game.VeinActive && !stage.VeinVisible && demo.Game.ManualMultiplier == 1, "Expired boost hides its effects and restores normal manual income");
            demo.RestoreForSmoke(new MineProgress { coins = 0, workerCount = 1, swapLevel = 1, veinOfferLeft = 9 }); demo.ShowPage(true);
            int draw = 0; demo.SpinForSmoke(() => ++draw == 2 ? .99 : 0); yield return WaitForChoice(demo);
            coins = demo.Game.Progress.coins; demo.AdvanceForSmoke(3, true);
            Check(demo.Game.Progress.coins == coins + 6 && demo.Game.Progress.veinOfferLeft == 9 && demo.Game.HasPendingRoll && demo.Game.Progress.pendingTypes[1] == 4, "Passive clock works during pick choice while preserving the draw and freezing the manual event");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
            Check(demo.Game.Progress.coins == coins + 6 && demo.AwaitingPickChoice && demo.Game.SwapsRemaining == 1, "Paid choice reload retains passive income and the exchange allowance");
            demo.Spin(); yield return FinishRoll(demo);
            demo.RestoreForSmoke(new MineProgress { coins = 9000, experience = MineGame.ExperienceForLevel(50), workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10 });
            demo.ShowWorkshop(); Click("Production section");
            foreach (var kind in new[] { UpgradeKind.Worker, UpgradeKind.Drill, UpgradeKind.Cart, UpgradeKind.Rails }) Check(!FindButton("Buy " + kind).interactable, "Capped production is disabled: " + kind);
            CheckLayout(); Capture("unity-production-max.png");
            demo.ShowPage(false);
            int meshes = stage.OwnedMeshCount;
            for (int i = 0; i < 100; i++) stage.SetProduction(i % 11, i % 3, i % 2, i % 2 == 0);
            Check(stage.OwnedMeshCount == meshes, "Production toggles reuse prepared worker, drill and glint meshes");
            stage.SetProduction(10, 3, 2, true);
            var times = new System.Text.StringBuilder(); float started = Time.realtimeSinceStartup;
            for (int frame = 0; frame < 32; frame++)
            {
                times.AppendLine((Time.realtimeSinceStartup - started).ToString(System.Globalization.CultureInfo.InvariantCulture));
                Capture("unity-production-animation-" + frame.ToString("00") + ".png"); yield return new WaitForSecondsRealtime(.05f);
            }
            File.WriteAllText(Path.Combine(output, "production-animation-times.txt"), times.ToString());
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator VerifyOffline(DemoController demo)
        {
            const long epoch = 1700000000;
            string original = JsonUtility.ToJson(demo.Game.Progress);
            demo.RestoreForSmoke(new MineProgress()); demo.ShowWorkshop(); Click("Production section");
            Check(!FindButton("Buy Warehouse").interactable, "Warehouse expansion is visible but initially locked");
            Click("Open warehouse"); yield return null;
            Check(GameObject.Find("Offline reward modal") != null && !FindButton("Claim offline reward").interactable, "Empty warehouse explains offline income without a free reward");
            CheckLayout(); Capture("unity-offline-empty.png"); Click("Close warehouse"); yield return null;
            demo.RestoreForSmoke(new MineProgress { coins = 1500, workerCount = 1, experience = MineGame.ExperienceForLevel(6) });
            demo.ShowWorkshop(); Click("Production section"); Click("Buy Warehouse");
            Check(demo.Game.Progress.coins == 0 && demo.Game.Progress.warehouseLevel == 1 && demo.Game.OfflineHours == 6 && !FindButton("Buy Warehouse").interactable, "Real storage purchase pays 1500, raises capacity to six hours and locks the next level");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); demo.ShowWorkshop(); Click("Production section");
            CheckLayout(); Capture("unity-warehouse-upgrade.png");
            demo.RestoreForSmoke(new MineProgress { coins = 1000, workerCount = 1, drillCount = 1, cartCount = 1, railLevel = 1, hits = 7, veinBoostLeft = 8 });
            demo.Game.RecordSession(epoch); demo.ReturnForSmoke(epoch + 7200); yield return null;
            Check(GameObject.Find("Offline reward modal") != null && demo.Game.Progress.offlineCoins == 56448 && demo.Game.Progress.coins == 1000 && demo.Game.Progress.hits == 7 && demo.Game.Progress.veinBoostLeft == 8, "Returning prepares the saved-rate two-hour crate and opens its modal without changing the wallet or manual progress");
            CheckLayout(); Capture("unity-offline-ready.png");
            demo.Mine(); demo.Upgrade(); demo.Forge(); demo.ShowPage(true);
            Check(demo.Game.Progress.coins == 1000 && demo.Game.Progress.totalClicks == 0 && demo.Game.Progress.upgradeLevel == 0 && FindButton("Mine rock") != null, "Offline window blocks background purchases, mining and navigation");
            Click("Close warehouse"); yield return null;
            Check(demo.Game.Progress.offlineCoins == 56448 && demo.Game.Progress.coins == 1000, "Later keeps the reward available without automatic credit");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
            demo.ReturnForSmoke(epoch + 10800); yield return null;
            Check(demo.Game.Progress.offlineCoins == 56448 && demo.Game.Progress.offlineRewardSeconds == 7200 && GameObject.Find("Offline reward modal") != null, "Reload and another return keep the existing crate instead of stacking another absence");
            demo.AdvanceForSmoke(1, false); Click("Claim offline reward"); demo.ClaimOfflineReward(); yield return null;
            Check(!demo.Game.HasOfflineReward && demo.Game.Progress.coins == 57463 && demo.Game.Progress.totalOfflineCoins == 56448 && demo.Game.Progress.tickets == 2 && demo.Game.Progress.experience == 0, "Claim button credits offline coins once alongside independent live income");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); yield return null;
            Check(GameObject.Find("Offline reward modal") == null && !demo.Game.ClaimOffline() && demo.Game.Progress.coins == 57463, "Reloading a claimed reward does not reopen or pay it again");
            demo.ShowWorkshop(); Click("Production section"); Click("Open warehouse"); yield return null;
            Check(!FindButton("Claim offline reward").interactable, "Claimed warehouse remains empty when reopened manually");
            CheckLayout(); Capture("unity-offline-claimed.png"); Click("Close warehouse"); yield return null;
            demo.RestoreForSmoke(new MineProgress { coins = 0, workerCount = 1, warehouseLevel = 2 }); demo.Game.RecordSession(epoch);
            demo.ReturnForSmoke(epoch + 86400); yield return null;
            Check(demo.Game.Progress.offlineCoins == 28800 && demo.Game.Progress.offlineRewardSeconds == 28800 && demo.Game.Progress.offlineElapsedSeconds == 86400, "Return modal shows a full-day absence capped by the purchased eight-hour warehouse");
            CheckLayout(); Capture("unity-offline-capped.png");
            string capped = JsonUtility.ToJson(demo.Game.Progress);
            Click("Close warehouse"); yield return null;
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.25f);
                demo.RestoreForSmoke(ProgressStore.Decode(capped)); yield return null;
                CheckLayout(); Capture("unity-offline-" + size.x + "x" + size.y + ".png");
                Click("Close warehouse"); yield return null;
            }
            Screen.SetResolution(480, 960, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(new MineProgress { coins = MineGame.CoinLimit, workerCount = 1 }); demo.Game.RecordSession(epoch);
            demo.ReturnForSmoke(epoch + 3600); yield return null;
            Check(!FindButton("Claim offline reward").interactable && demo.Game.Progress.offlineCoins == 3600, "A full wallet disables claim and retains all offline money");
            CheckLayout(); Capture("unity-offline-full.png"); Click("Close warehouse"); yield return null;
            Click("Upgrade pickaxe"); demo.ShowOfflineReward(); Click("Claim offline reward"); yield return null;
            Check(demo.Game.Progress.coins == MineGame.CoinLimit && demo.Game.Progress.offlineCoins == 3480 && demo.Game.Progress.totalOfflineCoins == 120, "After spending money the real claim button takes only the available space and saves the rest");
            demo.RestoreForSmoke(new MineProgress { coins = 0, workerCount = 1, tickets = 1, swapLevel = 1 }); demo.Game.RecordSession(epoch); demo.ShowPage(true);
            int draw = 0; demo.SpinForSmoke(() => ++draw == 2 ? .99 : 0); yield return WaitForChoice(demo);
            demo.ReturnForSmoke(epoch + 3600); yield return null;
            Check(GameObject.Find("Offline reward modal") == null && demo.Game.HasOfflineReward && demo.AwaitingPickChoice && !demo.Game.CanClaimOffline, "Pending draw defers the return modal and blocks its payout");
            demo.Spin(); yield return FinishRoll(demo); yield return null;
            Check(GameObject.Find("Offline reward modal") != null && demo.Game.Progress.offlineCoins == 3600 && demo.Game.Progress.tickets == 0 && !demo.Game.HasPendingRoll, "Return modal opens only after the paid draw has resolved");
            Click("Claim offline reward"); yield return null;
            Check(demo.Game.Progress.totalOfflineCoins == 3600 && !demo.Game.HasOfflineReward, "Deferred offline reward pays once after the draw");
            string sessionKey = "LuckyPickaxe.Runtime.Offline." + Guid.NewGuid().ToString("N");
            try
            {
                demo.RestoreForSmoke(new MineProgress { coins = 0, workerCount = 1 });
                demo.ConfigureSessionForSmoke(epoch, sessionKey); demo.SaveSessionForSmoke();
                demo.PauseForSmoke(true); long departed = demo.Game.Progress.lastSeenUtc;
                demo.AdvanceSessionForSmoke(600); demo.SaveSessionForSmoke();
                var pausedSave = ProgressStore.Load(sessionKey);
                Check(pausedSave.Progress.lastSeenUtc == departed && pausedSave.Progress.coins == 0, "Saving or quitting while paused keeps the departure timestamp and does not grant live income");
                demo.PauseForSmoke(false); yield return null;
                var resumedSave = ProgressStore.Load(sessionKey);
                Check(resumedSave.Progress.offlineCoins >= 600 && resumedSave.Progress.offlineCoins <= 601 && resumedSave.Progress.coins == 0 && GameObject.Find("Offline reward modal") != null, "Real pause and resume handlers prepare and persist the bounded offline crate before its UI");
                Click("Claim offline reward"); yield return null;
                var claimedSave = ProgressStore.Load(sessionKey);
                Check(claimedSave.Progress.coins >= 600 && claimedSave.Progress.coins <= 601 && !claimedSave.HasOfflineReward && !claimedSave.ClaimOffline(), "Controller claim saves the consumed crate and credited wallet together under an isolated key");
            }
            finally
            {
                demo.ConfigureSessionForSmoke(epoch, null); demo.PauseForSmoke(false);
                PlayerPrefs.DeleteKey(sessionKey); PlayerPrefs.DeleteKey(sessionKey + ".backup"); PlayerPrefs.Save();
            }
            demo.RestoreForSmoke(new MineProgress { coins = 9000, workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10, warehouseLevel = 4, experience = MineGame.ExperienceForLevel(50) });
            demo.ShowWorkshop(); Click("Production section"); Check(!FindButton("Buy Warehouse").interactable && demo.Game.OfflineHours == 12, "Maximum storage is disabled at twelve hours");
            CheckLayout(); Capture("unity-warehouse-max.png");
            demo.Game.RecordSession(epoch); demo.ReturnForSmoke(epoch + 86400); yield return null;
            Check(demo.Game.Progress.offlineCoins == 257040000, "Maximum offline crate fits the actual reward UI");
            CheckLayout(); Capture("unity-offline-max.png"); Click("Close warehouse"); yield return null;
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator VerifyRewardedOffline(DemoController demo)
        {
            const long epoch = 1700000000;
            string original = JsonUtility.ToJson(demo.Game.Progress);
            string key = "LuckyPickaxe.Runtime.Rewarded." + Guid.NewGuid().ToString("N");
            string request = null; int requests = 0;
            var audio = GetComponent<GameAudio>(); bool muted = audio.Muted;
            try
            {
                demo.RestoreForSmoke(new MineProgress { workerCount = 1 });
                demo.ConfigureSessionForSmoke(epoch, key); demo.Game.RecordSession(epoch);
                demo.AdvanceSessionForSmoke(600); demo.ReturnForSmoke(epoch + 600); yield return null;
                Check(!FindButton("Claim offline with video").interactable && FindButton("Claim offline reward").interactable, "Native demo preserves ordinary claim without inventing a video provider");
                demo.ConfigureRewardedForSmoke(id => { request = id; requests++; return true; });
                Check(FindButton("Claim offline with video").interactable, "An available provider enables the explicit doubled claim");
                CheckLayout(); Capture("unity-offline-video.png");
                Click("Claim offline with video");
                Check(demo.RewardedVideoPending && demo.IsPaused && audio.Suspended && !FindButton("Claim offline reward").interactable, "Opening video immediately freezes gameplay, audio and ordinary collection");
                demo.ClaimOfflineWithVideo(); demo.ClaimOfflineReward(); demo.AdvanceForSmoke(60, false);
                demo.AdvanceSessionForSmoke(35);
                Check(requests == 1 && demo.Game.Progress.coins == 80 && demo.Game.Progress.offlineCoins == 600, "Repeated input cannot open another video, earn production or collect while waiting");
                demo.OnBrowserPause("1"); demo.OnBrowserPause("0");
                Check(demo.IsPaused, "SDK resume cannot clear the controller's active video pause");
                demo.OnRewardedVideo("stale:rewarded");
                Check(demo.Game.Progress.coins == 80, "A callback for another request cannot issue a reward");
                demo.OnRewardedVideo(request + ":rewarded");
                var paid = ProgressStore.Load(key);
                Check(demo.IsPaused && paid.Progress.coins == 1280 && !paid.HasOfflineReward && paid.Progress.totalOfflineCoins == 1200, "onRewarded persists the doubled wallet and consumed crate before the video closes");
                demo.OnRewardedVideo(request + ":rewarded");
                Check(demo.Game.Progress.coins == 1280 && demo.Game.Progress.totalOfflineCoins == 1200, "Duplicate reward callbacks cannot credit twice");
                string firstRequest = request;
                demo.OnBrowserPause("1"); demo.OnRewardedVideo(request + ":closed");
                Check(demo.IsPaused && !demo.RewardedVideoPending && audio.Suspended, "Closing a rewarded video leaves independent host pause intact");
                demo.OnBrowserPause("0"); yield return null;
                Check(!demo.IsPaused && !demo.Game.HasOfflineReward && demo.Game.Progress.coins == 1280 && GameObject.Find("Offline reward modal") == null && audio.Muted == muted, "Resume presents one paid result and ad time cannot generate another crate");
                demo.RestoreForSmoke(ProgressStore.Load(key).Progress); yield return null;
                Check(!demo.Game.HasOfflineReward && !demo.Game.ClaimOffline(true) && demo.Game.Progress.coins == 1280, "Reload immediately after the reward cannot replay its payout");

                demo.AdvanceSessionForSmoke(600); demo.ReturnForSmoke(epoch + 1235); yield return null;
                long ordinary = demo.Game.Progress.offlineCoins, wallet = demo.Game.Progress.coins;
                Click("Claim offline with video");
                demo.OnRewardedVideo(firstRequest + ":rewarded");
                Check(demo.Game.Progress.coins == wallet && demo.Game.Progress.offlineCoins == ordinary, "Late callbacks from the previous video cannot pay a new crate");
                demo.OnRewardedVideo(request + ":closed"); yield return null;
                Check(!demo.IsPaused && demo.Game.Progress.coins == wallet && demo.Game.Progress.offlineCoins == ordinary && FindButton("Claim offline reward").interactable && GameObject.Find("Offline reward modal") != null, "Cancelled video preserves the warehouse and ordinary claim in the same window");
                CheckLayout(); Capture("unity-offline-video-cancelled.png");
                Click("Claim offline with video"); demo.OnRewardedVideo(request + ":error"); yield return null;
                Check(!demo.IsPaused && demo.Game.Progress.offlineCoins == ordinary && demo.Game.Progress.coins == wallet && FindButton("Claim offline reward").interactable, "Video error releases input and keeps all ordinary coins available");
                CheckLayout(); Capture("unity-offline-video-unavailable.png");
                demo.ConfigureRewardedForSmoke(id => false); Click("Claim offline with video"); yield return null;
                Check(!demo.IsPaused && !demo.RewardedVideoPending && demo.Game.Progress.offlineCoins == ordinary, "A rejected platform request cannot leave gameplay stuck or consume the crate");
                demo.ConfigureRewardedForSmoke(null); Click("Claim offline reward"); yield return null;
                Check(!demo.Game.HasOfflineReward && demo.Game.Progress.coins == wallet + ordinary && !ProgressStore.Load(key).HasOfflineReward, "After cancellation the ordinary button still saves the exact original payout");

                demo.RestoreForSmoke(new MineProgress { coins = MineGame.CoinLimit - 1199, workerCount = 1 });
                demo.Game.RecordSession(epoch); demo.ReturnForSmoke(epoch + 600); yield return null;
                demo.ConfigureRewardedForSmoke(id => true);
                Check(!FindButton("Claim offline with video").interactable && FindButton("Claim offline reward").interactable, "Insufficient room disables the video offer while retaining ordinary partial claim");
                CheckLayout(); Capture("unity-offline-video-wallet.png");
            }
            finally
            {
                if (demo.RewardedVideoPending) demo.OnRewardedVideo(request + ":error");
                demo.OnBrowserPause("0"); demo.ConfigureRewardedForSmoke(null); demo.ConfigureSessionForSmoke(epoch, null);
                PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save();
                demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
            }
        }

        private IEnumerator VerifyResponsiveLayout(DemoController demo, VoxelStage stage, VoxelStage shaft)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            var progress = new MineProgress { coins = 100000, experience = MineGame.ExperienceForLevel(50), upgradeLevel = 12, forgeLevel = 4, swapLevel = 1, efficiencyLevel = 2, workerCount = 8, drillCount = 2, cartCount = 2, railLevel = 1, stratum = 4 };
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(progress); demo.ShowPage(false); yield return null;
            Check(demo.IsWideLayout && stage.gameObject.activeSelf && FindButton("Buy Efficiency") != null, "Desktop mine and existing workshop controls are visible together");
            Check(stage.Target.width > 864 && stage.Target.filterMode == FilterMode.Point && stage.Target.antiAliasing == 1, "Desktop mine has a larger sharp render texture without smoothing");
            var ore = FindButton("Mine rock");
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, ore.GetComponent<RectTransform>().position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == ore.gameObject, "The enlarged ore receives pointer input at its actual projected location");
            long wallet = demo.Game.Progress.coins, price = demo.Game.UpgradePrice(UpgradeKind.Efficiency);
            Click("Buy Efficiency");
            Check(demo.Game.Progress.efficiencyLevel == 3 && demo.Game.Progress.coins == wallet - price && stage.gameObject.activeSelf, "Desktop sidebar purchases use the same rules while the mine remains visible");
            yield return new WaitForSecondsRealtime(2.9f);
            CheckLayout(); Capture("unity-desktop-mine.png");
            Click("Production section"); yield return null;
            Check(stage.gameObject.activeSelf && FindButton("Buy Worker") != null, "Production can be inspected beside the mine");
            CheckLayout(); Capture("unity-desktop-production.png");
            Click("Open warehouse"); yield return null;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, FindButton("Buy Worker").GetComponent<RectTransform>().position);
            hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.name == "Dialog shade", "A centered modal blocks input across the full desktop, including the sidebar");
            CheckLayout(); Capture("unity-desktop-warehouse.png"); Click("Close warehouse"); yield return null;
            demo.ShowMilestones(); yield return null;
            string beforeResize = JsonUtility.ToJson(demo.Game.Progress);
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(960, 600), new Vector2Int(1440, 900), new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.25f);
                Check(demo.IsWideLayout == (size.x >= 800 && size.x / (float)size.y >= 1.35f), "Responsive layout chooses the expected mode: " + size);
                CheckLayout();
                Check(JsonUtility.ToJson(demo.Game.Progress) == beforeResize, "Resizing an open book cannot change saved progress: " + size);
            }
            Click("Close milestones"); yield return null;
            Click("Equipment section"); demo.ShowPage(true); int sample = 0; double[] types = { .99, 0, .6, 0, 0 };
            demo.SpinForSmoke(() => types[sample++]);
            string paid = JsonUtility.ToJson(demo.Game.Progress);
            Screen.SetResolution(390, 844, false); yield return new WaitForSecondsRealtime(.25f);
            Check(JsonUtility.ToJson(demo.Game.Progress) == paid && demo.Game.HasPendingRoll, "Changing to portrait during a paid roll preserves its picks and spent ticket");
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.3f);
            yield return WaitForChoice(demo);
            Check(demo.IsWideLayout && shaft.gameObject.activeSelf && !stage.gameObject.activeSelf, "Desktop roulette uses the same active shaft and stops the mine renderer");
            CheckLayout(); Capture("unity-desktop-roulette.png");
            Click("Reel 0"); Click("Reel 1"); yield return WaitForChoice(demo);
            Check(demo.Game.Progress.pendingSwapCount == 1 && demo.Game.Progress.pendingTypes[1] == 4, "Repositioned desktop reels exchange the existing paid picks");
            CheckLayout(); Capture("unity-desktop-roulette-choice.png");
            int rolls = demo.Game.Progress.totalRolls;
            demo.Spin(); yield return FinishRoll(demo);
            Check(!demo.Game.HasPendingRoll && demo.Game.Progress.totalRolls == rolls, "The resized paid roll resolves without counting the already-paid draw again");
            CheckLayout(); Capture("unity-desktop-result.png");
            foreach (var size in new[] { new Vector2Int(960, 600), new Vector2Int(1440, 900), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.25f);
                CheckLayout(); Capture("unity-desktop-roulette-" + size.x + "x" + size.y + ".png");
            }
            Screen.SetResolution(480, 960, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
            Check(!demo.IsWideLayout && !stage.gameObject.activeSelf && shaft.Target.width == 432 && shaft.Target.height == 417, "Portrait restores its original layout, camera policy and render size");
        }

        private IEnumerator RecordDesktopDemo(DemoController demo)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(new MineProgress { coins = 6000, experience = MineGame.ExperienceForLevel(12), upgradeLevel = 5, forgeLevel = 3, swapLevel = 1, efficiencyLevel = 2, pressLevel = 2, workerCount = 4, drillCount = 1, cartCount = 2, railLevel = 2, stratum = 4 });
            demo.ShowPage(false); demo.UseLiveClockForSmoke(true);
            yield return new WaitForSecondsRealtime(.5f);
            string folder = Path.Combine(output, "store-frames"); Directory.CreateDirectory(folder);
            var times = new System.Text.StringBuilder("frame,seconds\n");
            double start = Time.realtimeSinceStartupAsDouble, nextMine = 0;
            bool launched = false, exchanged = false, confirmed = false, production = false;
            int frame = 0, sample = 0; double[] picks = { .99, 0, .6, .99, .6 };
            while (Time.realtimeSinceStartupAsDouble - start < 18)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - start;
                if (elapsed < 4.5 && elapsed >= nextMine) { Click("Mine rock"); nextMine = elapsed + .22; }
                if (elapsed >= 4.5 && !launched) { demo.ShowPage(true); demo.SpinForSmoke(() => picks[sample++]); launched = true; }
                if (demo.AwaitingPickChoice && !exchanged) { demo.SelectPick(0); demo.SelectPick(1); exchanged = true; }
                else if (demo.AwaitingPickChoice && exchanged && !confirmed && elapsed >= 9) { demo.Spin(); confirmed = true; }
                if (elapsed >= 14 && !demo.IsBusy && !production)
                {
                    demo.ShowPage(false); Click("Production section");
                    if (FindButton("Buy Worker").interactable) Click("Buy Worker");
                    production = true;
                }
                times.Append(frame).Append(',').AppendLine(elapsed.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                Capture(Path.Combine("store-frames", "frame-" + (frame++).ToString("D4") + ".jpg"));
                yield return new WaitForSecondsRealtime(1f / 30);
            }
            File.WriteAllText(Path.Combine(folder, "times.csv"), times.ToString());
            Check(launched && exchanged && confirmed && production && !demo.Game.HasPendingRoll, "Store video records real mining, exchange, paid result and production actions");
            demo.UseLiveClockForSmoke(false);
            Screen.SetResolution(480, 960, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator VerifyPrestige(DemoController demo)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            demo.RestoreForSmoke(new MineProgress()); demo.ShowWorkshop(); Click("Open prestige"); yield return null;
            Check(GameObject.Find("Prestige modal") != null && !FindButton("Prepare prestige").interactable && !FindButton("Buy prestige 0").interactable, "Permanent tree is visible but locked for a fresh miner");
            CheckLayout(); Capture("unity-prestige-locked.png"); Click("Close prestige"); yield return null;
            demo.RestoreForSmoke(new MineProgress { coins = 12345, tickets = 12, experience = MineGame.ExperienceForLevel(50), stratum = 51, upgradeLevel = 20, forgeLevel = 6, swapLevel = 2, efficiencyLevel = 3, workerCount = 3, totalClicks = 500, totalBlocks = 70, claimedContracts = 1, oresMined = 200, milestoneClaims = 5 });
            demo.ShowWorkshop(); Click("Open prestige"); yield return null;
            Check(FindButton("Prepare prestige").interactable && demo.Game.PrestigeReward == 8, "Level fifty enables the reset with its exact floor-based crystal preview");
            string before = JsonUtility.ToJson(demo.Game.Progress);
            Click("Prepare prestige"); yield return null;
            Check(FindButton("Confirm prestige").interactable && FindButton("Buy prestige 0") == null && demo.Game.Progress.crystals == 0, "Confirmation lists losses before the reset and hides purchase controls");
            CheckLayout(); Capture("unity-prestige-confirm.png");
            Click("Cancel prestige"); yield return null;
            Check(JsonUtility.ToJson(demo.Game.Progress) == before && FindButton("Prepare prestige").interactable, "Cancelling the confirmation does not spend, reset or grant anything");
            Click("Prepare prestige"); Click("Confirm prestige"); demo.ConfirmPrestige(); yield return null;
            Check(demo.Game.MinerLevel == 1 && demo.Game.Progress.coins == 80 && demo.Game.Progress.tickets == 2 && demo.Game.Progress.stratum == 1 && demo.Game.Progress.crystals == 8 && demo.Game.Progress.prestigeCount == 1 && demo.Game.Progress.upgradeLevel == 0 && demo.Game.Progress.workerCount == 0 && !FindButton("Prepare prestige").interactable, "Real confirmation resets the run once and opens the permanent shop with eight crystals");
            Check(demo.Game.Progress.milestoneClaims == 5 && demo.Game.MilestoneValue(11) == 20 && demo.Game.CanClaimMilestone(15) && demo.Game.Progress.claimedContracts == 1, "New-run interface retains unclaimed completed goals and lifetime contract progress");
            CheckLayout(); Capture("unity-prestige-reward.png");
            Click("Buy prestige 3"); Click("Buy prestige 4"); Click("Buy prestige 0"); demo.BuyPrestigeUpgrade(0);
            Check(demo.Game.Progress.crystals == 1 && demo.Game.Progress.coins == 1080 && demo.Game.Progress.upgradeLevel == 2 && demo.Game.ClickPower == 20 && demo.Game.HasPrestigeUpgrade(3) && demo.Game.HasPrestigeUpgrade(4) && demo.Game.HasPrestigeUpgrade(0) && !FindButton("Buy prestige 0").interactable && !FindButton("Buy prestige 1").interactable, "Real permanent purchases follow dependencies, spend crystals once and apply starter bonuses immediately");
            CheckLayout(); Capture("unity-prestige-bought.png");
            Click("Close prestige"); yield return null;
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); demo.ShowWorkshop(); Click("Open prestige"); yield return null;
            Check(demo.Game.Progress.crystals == 1 && demo.Game.Progress.prestigeUpgrades == 25 && !FindButton("Buy prestige 4").interactable && demo.Game.Progress.coins == 1080, "Reload keeps the purchased permanent nodes without replaying their starter gifts");
            Click("Close prestige"); yield return null;
            demo.ShowPage(false); CheckLayout(); Capture("unity-prestige-new-mine.png");
            demo.RestoreForSmoke(new MineProgress { prestigeCount = 3, prestigeUpgrades = 511, totalCrystals = 30, upgradeLevel = 2, workerCount = 1, experience = MineGame.ExperienceForLevel(3), veinOfferLeft = 20 });
            Click("Activate rich vein"); demo.AdvanceForSmoke(1, true);
            Check(demo.Game.RichVeinTarget == 20 && demo.Game.Progress.veinBoostLeft == 15 && demo.Game.ManualMultiplier == 4 && demo.Game.ProductionMilli == 2500, "Permanent rich-vein upgrades and production bonus reach the live mining UI");
            CheckLayout(); Capture("unity-prestige-rich.png");
            demo.ShowWorkshop(); Click("Open prestige"); yield return null;
            for (int i = 0; i < 9; i++) Check(!FindButton("Buy prestige " + i).interactable, "Owned permanent node is disabled: " + i);
            CheckLayout(); Capture("unity-prestige-complete.png");
            string complete = JsonUtility.ToJson(demo.Game.Progress); Click("Close prestige"); yield return null;
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.25f);
                demo.RestoreForSmoke(ProgressStore.Decode(complete)); demo.ShowWorkshop(); Click("Open prestige"); yield return null;
                CheckLayout(); Capture("unity-prestige-" + size.x + "x" + size.y + ".png");
                Click("Close prestige"); yield return null;
                demo.RestoreForSmoke(new MineProgress { coins = MineGame.CoinLimit, tickets = 1000000, experience = MineGame.ExperienceForLevel(50), stratum = 71 });
                demo.ShowWorkshop(); Click("Open prestige"); Click("Prepare prestige"); yield return null;
                CheckLayout(); Capture("unity-prestige-confirm-" + size.x + "x" + size.y + ".png");
                Click("Cancel prestige"); Click("Close prestige"); yield return null;
            }
            Screen.SetResolution(480, 960, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(new MineProgress { experience = MineGame.ExperienceForLevel(50), workerCount = 1 });
            demo.Game.RecordSession(1700000000); demo.Game.PrepareOffline(1700000600);
            demo.ShowWorkshop(); Click("Open prestige"); yield return null;
            Check(!FindButton("Prepare prestige").interactable && !demo.Game.CanPrestige && demo.Game.Progress.offlineCoins == 600, "Tree requires collecting the offline crate rather than losing it on reset");
            CheckLayout(); Click("Close prestige"); yield return null;
            demo.RestoreForSmoke(new MineProgress { experience = MineGame.ExperienceForLevel(50), swapLevel = 1, tickets = 1 }); demo.ShowPage(true);
            int draw = 0; demo.SpinForSmoke(() => ++draw == 2 ? .99 : 0); yield return WaitForChoice(demo);
            demo.ShowPrestige();
            Check(GameObject.Find("Prestige modal") == null && !demo.Game.CanPrestige, "Paid pick choice blocks opening prestige and prevents discarding its ticket");
            demo.Spin(); yield return FinishRoll(demo);
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator VerifyMilestones(DemoController demo)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            demo.RestoreForSmoke(new MineProgress());
            Click("Open milestones"); yield return null;
            Check(GameObject.Find("Milestones modal") != null && !FindButton("Claim milestone 0").interactable && !FindButton("Milestones previous").interactable, "Book opens on the first page with unfinished goals locked");
            CheckLayout(); Capture("unity-milestones-start.png");
            Click("Close milestones"); yield return null;
            demo.RestoreForSmoke(new MineProgress { coins = 1000, tickets = 0, totalClicks = 30, totalBlocks = 25 });
            Click("Open milestones"); yield return null;
            Check(FindButton("Claim milestone 0").interactable, "Completed goal enables its real reward button");
            long clicks = demo.Game.Progress.totalClicks;
            demo.Mine(); demo.Upgrade(); demo.Forge(); demo.ClaimContract(); demo.ShowWorkshop();
            Check(demo.Game.Progress.totalClicks == clicks && demo.Game.Progress.coins == 1000 && demo.Game.Progress.claimedContracts == 0 && demo.Game.Progress.upgradeLevel == 0 && demo.Game.Progress.forgeLevel == 0 && FindButton("Mine rock") != null, "Book blocks otherwise affordable background purchases, ready contracts, mining and navigation");
            CheckLayout(); Capture("unity-milestones-ready.png");
            Click("Claim milestone 0"); demo.ClaimMilestone(0);
            Check(demo.Game.Progress.coins == 1120 && demo.Game.Progress.tickets == 1 && demo.Game.ClaimedMilestones == 1 && !FindButton("Claim milestone 0").interactable, "Real goal button pays once and disables duplicate claims");
            yield return new WaitForSecondsRealtime(.2f);
            CheckLayout(); Capture("unity-milestones-claimed.png");
            Click("Close milestones"); yield return null;
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
            Click("Open milestones"); yield return null;
            Check(!FindButton("Claim milestone 0").interactable && demo.Game.Progress.coins == 1120 && demo.Game.ClaimedMilestones == 1, "Book restores paid goal state without awarding it again");
            Click("Close milestones"); yield return null;
            demo.RestoreForSmoke(new MineProgress { coins = 1000, tickets = 0, totalClicks = 30, oresMined = 10, totalRolls = 5, upgradeLevel = 20, totalBlocks = 50, experience = MineGame.ExperienceForLevel(50), swapLevel = 1, efficiencyLevel = 3, stratum = 10, forgeLevel = 6, pressLevel = 3, treasureLevel = 5 });
            Click("Open milestones"); yield return null;
            for (int page = 0; page < 4; page++)
            {
                CheckLayout();
                for (int slot = 0; slot < 4; slot++) Check(FindButton("Claim milestone " + slot).interactable, "Completed goal button is ready on page " + page + ", slot " + slot);
                if (page < 3) Click("Milestones next");
            }
            Check(!FindButton("Milestones next").interactable, "Last page cannot advance beyond the book");
            Capture("unity-milestones-late.png");
            Click("Claim milestone 3");
            Check(demo.Game.Progress.coins == 4000 && demo.Game.Progress.tickets == 5 && demo.Game.MilestoneClaimed(15), "Last page pays the selected goal without requiring earlier claims");
            Click("Milestones previous"); yield return null; CheckLayout();
            Click("Close milestones"); yield return null;
            demo.RestoreForSmoke(new MineProgress { swapLevel = 1, totalClicks = 30 }); demo.ShowPage(true);
            int draw = 0; demo.SpinForSmoke(() => ++draw == 2 ? .99 : 0); yield return WaitForChoice(demo);
            demo.ShowMilestones();
            Check(GameObject.Find("Milestones modal") == null && !demo.Game.ClaimMilestone(0), "Pending paid draw blocks both book opening and goal payouts");
            demo.Spin(); yield return FinishRoll(demo);
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator VerifyWorkshop(DemoController demo, VoxelStage stage, VoxelStage shaft)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            demo.RestoreForSmoke(new MineProgress()); Click("МАСТЕРСКАЯ");
            Check(!stage.gameObject.activeSelf && !shaft.gameObject.activeSelf && !FindButton("Buy Swap").interactable, "Workshop opens with exchange locked and both world cameras asleep");
            CheckLayout(); Capture("unity-workshop-locked.png");
            demo.Purchase(UpgradeKind.Swap);
            Check(demo.Game.Progress.swapLevel == 0 && demo.Game.Progress.coins == 80, "Locked purchase cannot spend the starter wallet");
            demo.RestoreForSmoke(new MineProgress { coins = 599, experience = MineGame.ExperienceForLevel(3) });
            demo.ShowWorkshop(); Check(!FindButton("Buy Swap").interactable, "Unlocked level still requires the full price");
            demo.Game.Progress.coins = 600; demo.ToggleSound();
            Click("Buy Swap");
            Check(demo.Game.Progress.swapLevel == 1 && demo.Game.Progress.coins == 0 && !FindButton("Buy Swap").interactable, "UI buys the permanent first exchange once and shows the next level gate");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); demo.ShowWorkshop();
            CheckLayout(); Capture("unity-workshop-unlock.png");
            demo.RestoreForSmoke(new MineProgress { coins = 5000, swapLevel = 1, hits = 11, experience = MineGame.ExperienceForLevel(6) });
            demo.ShowWorkshop(); int tickets = demo.Game.Progress.tickets;
            Click("Buy Efficiency"); Click("Buy Press"); Click("Buy Treasure");
            Check(demo.Game.MiningDamage == 2 && demo.Game.HitsRequired == 11 && demo.Game.ChestReward == 69 && demo.Game.Progress.coins == 3350 && demo.Game.Progress.tickets == tickets + 1, "Workshop purchases change damage, ticket cadence and chest reward at their listed prices");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress))); demo.ShowWorkshop();
            Check(demo.Game.Progress.swapLevel == 1 && demo.Game.Progress.efficiencyLevel == 1 && demo.Game.Progress.pressLevel == 1 && demo.Game.Progress.treasureLevel == 1, "All workshop branches survive presentation reload");
            CheckLayout(); Capture("unity-workshop.png");
            demo.ShowPage(false);
            bool correctCadence = false;
            foreach (var text in GetComponentsInChildren<Text>(false)) if (text.text == "0 / 11") correctCadence = true;
            Check(correctCadence, "Mining UI displays the upgraded ticket interval");

            demo.RestoreForSmoke(new MineProgress { coins = 9600, swapLevel = 2, experience = MineGame.ExperienceForLevel(15) });
            demo.ShowWorkshop(); Click("Buy Swap");
            Check(demo.Game.Progress.swapLevel == 3 && demo.Game.Progress.coins == 0 && !FindButton("Buy Swap").interactable, "Third exchange is purchased and capped through the real button");
            demo.ShowPage(true); int draw = 0; double[] samples = { .99, 0, .6, 0, 0 };
            demo.Game.Progress.tickets = 1;
            demo.SpinForSmoke(() => samples[draw++]); yield return WaitForChoice(demo);
            Check(demo.Game.SwapsRemaining == 3 && demo.Game.Progress.tickets == 0, "Purchased exchange allowance works on the last ticket");
            for (int swap = 0; swap < 3; swap++)
            {
                Click("Reel " + swap); Click("Reel " + (swap + 1));
                yield return WaitForChoice(demo);
                demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
                Check(demo.Game.SwapsRemaining == 2 - swap && demo.Game.Progress.pendingSwapCount == swap + 1 && demo.Game.Progress.pendingTypes[swap + 1] == 4, "Reload preserves each arranged pick and remaining allowance " + swap);
            }
            Check(!FindButton("Reel 0").interactable && !demo.Game.CanSwap, "No fourth exchange can be selected");
            CheckLayout(); Capture("unity-roulette-swap3.png");
            demo.Spin(); yield return FinishRoll(demo);
            Check(demo.Game.Progress.tickets == 0 && !demo.Game.HasPendingRoll, "Three exchanges confirm without another ticket");
            demo.RestoreForSmoke(new MineProgress { coins = 8000, upgradeLevel = 100, forgeLevel = 10, swapLevel = 3, efficiencyLevel = 10, pressLevel = 6, treasureLevel = 10, experience = MineGame.ExperienceForLevel(50) });
            demo.ShowWorkshop();
            for (int kind = 0; kind < (int)UpgradeKind.Worker; kind++) Check(!FindButton("Buy " + (UpgradeKind)kind).interactable, "Capped workshop action is disabled: " + (UpgradeKind)kind);
            CheckLayout(); Capture("unity-workshop-max.png");
            demo.RestoreForSmoke(ProgressStore.Decode(original)); demo.ShowPage(true);
        }

        private IEnumerator WaitForChoice(DemoController demo)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (demo.IsBusy && !demo.AwaitingPickChoice && Time.realtimeSinceStartup < deadline) yield return null;
            Check(demo.AwaitingPickChoice, "Purchased exchange returns to the choice phase");
        }

        private IEnumerator VerifyDevelopment(DemoController demo, VoxelStage stage)
        {
            string original = JsonUtility.ToJson(demo.Game.Progress);
            int meshCount = stage.OwnedMeshCount;
            int[] upgrades = { 0, 2, 5, 12, 20 }, forges = { 0, 1, 3, 6, 10 };
            for (int i = 0; i < upgrades.Length; i++)
            {
                var fixture = new MineProgress { upgradeLevel = upgrades[i], forgeLevel = forges[i], coins = i == 0 ? 80 : 8000, experience = MineGame.ExperienceForLevel(MineGame.OreUnlockLevel(i)), oreKind = i };
                demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(fixture)));
                string loaded = JsonUtility.ToJson(demo.Game.Progress);
                Check(stage.DevelopmentTier == i && stage.VisibleMineStructures == i && stage.ForgeStage == i && stage.VisibleForgeStructures == i, "Saved upgrade milestones restore mine and forge structures " + i);
                Check(stage.CartVisible == (i > 0) && stage.OwnedMeshCount == meshCount, "Restored development reuses the prepared geometry " + i);
                yield return new WaitForSecondsRealtime(.25f);
                CheckLayout(); Capture("unity-camp-" + i + ".png");
                Check(JsonUtility.ToJson(demo.Game.Progress) == loaded, "Decorations and their animations do not alter the economy " + i);
            }
            Vector3 cartBefore = stage.CartPosition;
            yield return new WaitForSecondsRealtime(.45f);
            Check((stage.CartPosition - cartBefore).sqrMagnitude > .00001f, "Unlocked ore cart moves along its rails");
            Button oreButton = FindButton("Mine rock");
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, oreButton.GetComponent<RectTransform>().position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == oreButton.gameObject, "The developed mine keeps the ore input target available");
            for (int i = 0; i < 100; i++) stage.SetDevelopment(i % 5, i % 11, false);
            Check(stage.OwnedMeshCount == meshCount, "Repeated development changes create no extra meshes");

            // Actual upgrade buttons reveal one new mine structure and one forge structure in the recording.
            demo.RestoreForSmoke(new MineProgress { upgradeLevel = 4, forgeLevel = 2, coins = 8000, experience = MineGame.ExperienceForLevel(4), oreKind = 2 });
            long walletBefore = demo.Game.Progress.coins;
            long upgradeCost = demo.Game.UpgradeCost, forgeCost = demo.Game.ForgeCost;
            int tickets = demo.Game.Progress.tickets;
            var times = new System.Text.StringBuilder();
            for (int frame = 0; frame < 48; frame++)
            {
                if (frame == 6)
                {
                    Click("Upgrade pickaxe");
                    Check(stage.DevelopmentTier == 2 && stage.VisibleMineStructures == 2 && demo.Game.Progress.coins == walletBefore - upgradeCost, "Buying the iron pick reinforces the entrance without an extra construction charge");
                }
                if (frame == 24)
                {
                    Click("Upgrade forge");
                    Check(stage.ForgeStage == 2 && stage.VisibleForgeStructures == 2, "Third forge upgrade adds its chimney");
                }
                times.AppendLine(Time.realtimeSinceStartup.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                Capture("unity-camp-animation-" + frame.ToString("000") + ".png");
                yield return new WaitForSecondsRealtime(.05f);
            }
            File.WriteAllText(Path.Combine(output, "camp-animation-times.txt"), times.ToString());
            Check(demo.Game.Progress.coins == walletBefore - upgradeCost - forgeCost && demo.Game.Progress.tickets == tickets && stage.OwnedMeshCount == meshCount, "Development preserves upgrade prices, tickets and mesh count");
            demo.RestoreForSmoke(ProgressStore.Decode(original));
            yield return new WaitForSecondsRealtime(.15f);
        }

        private IEnumerator VerifyPlatform(DemoController demo)
        {
            demo.RestoreForSmoke(new MineProgress { coins = 500, workerCount = 1 });
            var audio = GetComponent<GameAudio>(); bool muted = audio.Muted;
            demo.FocusForSmoke(false);
            Check(audio.Suspended && audio.Muted == muted, "Losing focus silences active audio without changing the player's sound preference");
            demo.FocusForSmoke(true);
            Check(!audio.Suspended && audio.Muted == muted, "Returning focus restores the user's sound preference");
            demo.OnBrowserPause("1"); string paused = JsonUtility.ToJson(demo.Game.Progress);
            Check(demo.IsPaused && !GetComponentInChildren<CanvasGroup>().interactable && !GetComponentInChildren<CanvasGroup>().blocksRaycasts && audio.Suspended, "Host pause freezes input, pointer events and sound");
            demo.Mine(); demo.Upgrade(); demo.Forge(); demo.Spin(); demo.ClaimContract(); demo.ActivateRichVein(); demo.AdvanceForSmoke(60, true);
            demo.UseLiveClockForSmoke(true); yield return new WaitForSecondsRealtime(.4f);
            Check(JsonUtility.ToJson(demo.Game.Progress) == paused, "Paused game cannot mine, buy, roll, activate a vein or earn live production");
            demo.PauseForSmoke(true); demo.OnBrowserPause("0");
            Check(demo.IsPaused && audio.Suspended, "Clearing the host pause cannot clear an independent engine pause");
            demo.PauseForSmoke(false); demo.UseLiveClockForSmoke(false);
            Check(!demo.IsPaused && GetComponentInChildren<CanvasGroup>().interactable && !audio.Suspended, "Input and sound return only after every pause reason clears");
            demo.RestoreForSmoke(new MineProgress { coins = 500, workerCount = 1, swapLevel = 1, tickets = 1 });
            demo.ShowPage(true); demo.SpinForSmoke(() => 0);
            yield return new WaitForSecondsRealtime(.2f);
            demo.OnBrowserPause("1"); paused = JsonUtility.ToJson(demo.Game.Progress); int settled = demo.SettledReels;
            yield return new WaitForSecondsRealtime(1);
            Check(demo.RollPhase == "reels" && demo.SettledReels == settled && JsonUtility.ToJson(demo.Game.Progress) == paused, "Host pause freezes a paid reel animation and retains its fixed picks and spent ticket");
            demo.OnBrowserPause("0"); yield return FinishRoll(demo);
            Check(!demo.Game.HasPendingRoll && demo.Game.Progress.totalRolls == 1, "Resuming the same paid draw resolves exactly once");
            demo.RestoreForSmoke(new MineProgress());
        }

        private IEnumerator VerifyGuide(DemoController demo)
        {
            demo.RestoreForSmoke(new MineProgress(), true);
            Check(demo.Game.GuideActive && FindButton("Guide action") != null && FindButton("Open milestones") == null, "First session replaces the contract with nonmodal guide controls");
            CheckLayout(); Capture("unity-guide-start.png");
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .45f, Screen.height * .58f) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            bool oreHit = false; foreach (var hit in hits) if (hit.gameObject.name == "Mine rock") oreHit = true;
            Check(oreHit, "Guide focus does not intercept input to the ore");
            for (int i = 0; i < 6; i++) { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            Check(demo.Game.Progress.guideStep == 1 && demo.Game.Progress.coins == 140, "Real mining completes the first hint without extra rewards");
            CheckLayout(); Capture("unity-guide-upgrade.png");
            Click("Guide action"); Click("Upgrade pickaxe");
            Check(demo.Game.Progress.guideStep == 2 && demo.Game.Progress.coins == 20, "Ordinary paid upgrade advances the first-session hint");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)), true);
            Click("Guide action");
            Check(FindButton("ЗАПУСТИТЬ КИРКИ") != null && demo.Game.Progress.tickets == 2, "Hint navigation opens roulette without spending a ticket");
            CheckLayout(); Capture("unity-guide-roulette.png");
            demo.SpinForSmoke(() => 0); yield return null;
            Check(FindButton("Skip guide") == null && demo.Game.HasPendingRoll, "Paid draw hides guide actions until the actual result");
            yield return FinishRoll(demo);
            Check(demo.Game.Progress.guideStep == 3, "Resolved draw advances the worker hint");
            demo.ShowPage(false);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!demo.Game.CanBuyUpgrade(UpgradeKind.Worker) && Time.realtimeSinceStartup < deadline)
            { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            Click("Guide action");
            Check(FindButton("Buy Worker") != null && FindButton("Buy Worker").interactable, "Worker hint opens the existing production tab when its real requirements are met");
            CheckLayout(); Capture("unity-guide-worker.png");
            Click("Buy Worker");
            Check(demo.Game.Progress.workerCount == 1 && demo.Game.Progress.guideStep == 4, "Hiring one worker opens the book hint");
            demo.ShowPage(false);
            while (demo.Game.ReadyMilestones == 0 && Time.realtimeSinceStartup < deadline)
            { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            CheckLayout(); Capture("unity-guide-goals.png");
            Click("Guide action"); yield return null;
            Check(FindButton("Skip guide") == null && FindButton("Claim milestone 0") != null, "Book modal hides guide without blocking its real reward");
            Click("Claim milestone 0"); Click("Close milestones"); yield return null;
            Check(!demo.Game.GuideActive && FindButton("Open milestones") != null && FindButton("Guide action") == null, "Claiming the existing goal restores normal contract controls");
            long coins = demo.Game.Progress.coins;
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)), true);
            Check(!demo.Game.GuideActive && demo.Game.Progress.coins == coins, "Completed first-session guide stays finished after reload");
            demo.RestoreForSmoke(new MineProgress(), true); Click("Skip guide");
            demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)), true);
            Check(!demo.Game.GuideActive && demo.Game.Progress.coins == 80 && demo.Game.Progress.tickets == 2, "Explicit skip persists without changing the initial wallet");
            demo.ShowPage(true); Click("ШАНСЫ И ПРАВИЛА"); Click("Start guide"); yield return null;
            Check(demo.Game.GuideActive && FindButton("Mine rock") != null && GameObject.Find("Rules modal") == null, "Rules can explicitly reenable skipped hints without resetting the game");
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.3f);
                CheckLayout(); Capture("unity-guide-" + size.x + "x" + size.y + ".png");
            }
            Screen.SetResolution(480, 960, false); yield return new WaitForSecondsRealtime(.3f);
            demo.RestoreForSmoke(new MineProgress { version = 10 }, true);
            Check(!demo.Game.GuideActive && FindButton("Open milestones") != null, "Existing v0.14 player sees the usual contract immediately after update");
        }

        private void CheckLayout()
        {
            var corners = new Vector3[4];
            foreach (var button in GetComponentsInChildren<Button>(false))
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Check(corners[0].x >= -1 && corners[2].x <= Screen.width + 1 && corners[0].y >= -1 && corners[2].y <= Screen.height + 1, "Control inside viewport: " + button.name);
            }
            foreach (var text in GetComponentsInChildren<Text>(false))
                if (!text.resizeTextForBestFit) Check(text.preferredHeight <= text.rectTransform.rect.height + 2, "Text fits height: " + text.text);
        }

        private IEnumerator FinishRoll(DemoController demo)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (demo.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (demo.AwaitingPickChoice) demo.Spin();
                yield return null;
            }
            Check(!demo.IsBusy, "Confirmed roll completes within deadline");
        }

        private IEnumerator RecordRoll(DemoController demo, string name, bool exchange = false)
        {
            var times = new System.Text.StringBuilder(); int frame = 0;
            float deadline = Time.realtimeSinceStartup + 14;
            int choiceFrames = 0, exchangeStage = 0;
            while (demo.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (demo.AwaitingPickChoice)
                {
                    choiceFrames++;
                    if (!exchange) demo.Spin();
                    else if (exchangeStage == 0 && choiceFrames == 3)
                    {
                        Check(demo.Game.Progress.coins == 0 && demo.Game.Progress.tickets == 0 && demo.Game.Progress.depths[3] == 3, "Last ticket is spent but the exposed obsidian has not been hit");
                        demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
                        Check(demo.AwaitingPickChoice && demo.Game.Progress.pendingTypes[0] == 4 && demo.Game.Progress.pendingTypes[3] == 1 && demo.Game.Progress.pendingSwapCount == 0, "Reload returns to the same unused paid draw");
                        CheckLayout(); Capture("unity-roulette-choice.png"); Capture("unity-roulette-exchange-before.png");
                        Click("Reel 3");
                        Check(demo.SelectedPick == 3, "First pointer click selects an active pick");
                        Button target = FindButton("Reel 0");
                        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, target.GetComponent<RectTransform>().position) };
                        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                        Check(hits.Count > 0 && hits[0].gameObject == target.gameObject, "Reel receives real pointer input above its mask and symbols");
                        Capture("unity-roulette-exchange-selected.png");
                        choiceFrames = 0; exchangeStage = 1;
                    }
                    else if (exchangeStage == 1 && choiceFrames == 7)
                    {
                        Click("Reel 0");
                        Check(demo.RollPhase == "exchange" && demo.Game.Progress.pendingSwapCount == 1 && demo.Game.Progress.pendingTypes[3] == 4 && demo.Game.Progress.pendingTypes[0] == 1, "Second pointer click saves the exchange before animation");
                        demo.Spin();
                        Check(demo.Game.HasPendingRoll && demo.Game.Progress.tickets == 0 && demo.Game.Progress.depths[3] == 3, "Confirmation is blocked while picks exchange places");
                        choiceFrames = 0; exchangeStage = 2;
                    }
                    else if (exchangeStage == 2 && choiceFrames == 7)
                    {
                        demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
                        Check(demo.AwaitingPickChoice && demo.Game.Progress.pendingSwapCount == 1 && demo.Game.Progress.pendingTypes[3] == 4 && !FindButton("Reel 0").interactable, "Reload retains the arranged picks and disables a second exchange");
                        demo.SelectPick(1); Check(demo.SelectedPick == -1, "Used exchange rejects further selection");
                        CheckLayout(); Capture("unity-roulette-exchange-after.png");
                        Click("ЗАПУСТИТЬ КИРКИ");
                        long coins = demo.Game.Progress.coins; demo.Spin();
                        Check(!demo.Game.HasPendingRoll && demo.Game.Progress.coins == coins && demo.Game.Progress.tickets == 0, "Repeated confirm cannot duplicate reward or spend tickets");
                        exchangeStage = 3;
                    }
                }
                times.AppendLine(Time.realtimeSinceStartup.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                Capture("unity-" + name + "-" + (frame++).ToString("000") + ".png");
                yield return new WaitForSecondsRealtime(.04f);
            }
            File.WriteAllText(Path.Combine(output, name + "-times.txt"), times.ToString());
            Check(!demo.IsBusy, "Risk animation completes: " + name);
            if (exchange) Check(exchangeStage == 3, "Exchange recording covers choice, swap, reload and confirm");
            yield return new WaitForSecondsRealtime(.45f);
        }

        private void Check(bool condition, string label)
        {
            if (!condition) Debug.LogError("Smoke check failed: " + label);
        }

        // Render UI offscreen: minimized/hidden Windows players may return black backbuffer captures.
        private void Capture(string file)
        {
            var canvas = GetComponentInChildren<Canvas>();
            foreach (var stage in GetComponentsInChildren<VoxelStage>(false)) stage.RenderNow();
            var camera = Camera.main;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var previous = RenderTexture.active;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); pixels.Apply();
            Check(pixels.GetPixel(Screen.width / 2, Screen.height / 2).grayscale > .03f, "Rendered UI is visible");
            File.WriteAllBytes(Path.Combine(output, file), file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? pixels.EncodeToJPG(94) : pixels.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            target.Release(); Destroy(target); Destroy(pixels);
        }

        private void Click(string prefix)
        {
            var button = FindButton(prefix);
            if (button == null) { Debug.LogError("Smoke check: missing button " + prefix); return; }
            Check(button.interactable, "Button enabled: " + prefix);
            if (button.interactable) button.onClick.Invoke();
        }

        private Button FindButton(string prefix)
        {
            foreach (var button in GetComponentsInChildren<Button>(false))
                if (button.name.StartsWith(prefix, StringComparison.Ordinal)) return button;
            return null;
        }
    }
}
