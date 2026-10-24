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
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(rockButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.65f);
            ExecuteEvents.Execute(rockButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            rockButton.onClick.Invoke();
            long heldCount = demo.Game.Progress.totalClicks - heldStart;
            Check(heldCount >= 2 && heldCount <= 4, "Hold repeats at controlled rate without release bonus");
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
            while (demo.IsBusy && !demo.AwaitingPickChoice && Time.realtimeSinceStartup < deadline)
            {
                Check(demo.SettledReels >= settled, "Reels stop in one direction"); settled = demo.SettledReels;
                yield return null;
            }
            Check(demo.AwaitingPickChoice && demo.SettledReels == 5 && demo.Game.Progress.coins == coinsBeforeSpin, "Reels await the player's choice without awarding coins");
            for (int c = 0; c < 5; c++) Check(demo.Game.Progress.depths[c] == 0, "No strike before confirmation");
            CheckLayout(); Capture("unity-roulette-choice.png");
            Click("ЗАПУСТИТЬ КИРКИ");
            int[] committed = (int[])demo.Game.Progress.depths.Clone();
            demo.Spin();
            Check(!demo.Game.HasPendingRoll && demo.Game.Progress.tickets == ticketsBeforeSpin - 1, "Confirm and repeated click never charge a second ticket");
            while (demo.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
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
            demo.RestoreForSmoke(new MineProgress { boardSeed = 17, tickets = 1, coins = 0, depths = new[] { 0, 0, 0, 3, 0 } });
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
            demo.Game.Progress.totalBlocks = 25000000; demo.Game.Progress.claimedContracts = 999999;
            demo.ToggleSound();
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.4f);
                CheckLayout();
                Capture("unity-" + size.x + "x" + size.y + ".png");
                demo.ShowPage(true); yield return null; CheckLayout();
                demo.ShowPage(false); yield return null;
            }
            demo.RestoreForSmoke(new MineProgress { tickets = 1 });
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
                Check(demo.SelectedPick == -1 && !demo.Game.Progress.pendingSwapUsed, "Deselect keeps the exchange available at " + size);
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
                        Check(demo.AwaitingPickChoice && demo.Game.Progress.pendingTypes[0] == 4 && demo.Game.Progress.pendingTypes[3] == 1 && !demo.Game.Progress.pendingSwapUsed, "Reload returns to the same unused paid draw");
                        CheckLayout(); Capture("unity-roulette-exchange-before.png");
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
                        Check(demo.RollPhase == "exchange" && demo.Game.Progress.pendingSwapUsed && demo.Game.Progress.pendingTypes[3] == 4 && demo.Game.Progress.pendingTypes[0] == 1, "Second pointer click saves the exchange before animation");
                        demo.Spin();
                        Check(demo.Game.HasPendingRoll && demo.Game.Progress.tickets == 0 && demo.Game.Progress.depths[3] == 3, "Confirmation is blocked while picks exchange places");
                        choiceFrames = 0; exchangeStage = 2;
                    }
                    else if (exchangeStage == 2 && choiceFrames == 7)
                    {
                        demo.RestoreForSmoke(ProgressStore.Decode(JsonUtility.ToJson(demo.Game.Progress)));
                        Check(demo.AwaitingPickChoice && demo.Game.Progress.pendingSwapUsed && demo.Game.Progress.pendingTypes[3] == 4 && !FindButton("Reel 0").interactable, "Reload retains the arranged picks and disables a second exchange");
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
            File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG());
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
