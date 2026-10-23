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
            for (int i = 0; i < 12; i++) { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            Check(demo.Game.Progress.coins == 176 && demo.Game.Progress.tickets == 3, "Clicker and ticket");
            Click("Upgrade pickaxe");
            Check(demo.Game.ClickPower == 12 && demo.Game.Progress.coins == 56, "Upgrade button");
            for (int i = 0; i < 12; i++) { Click("Mine rock"); yield return new WaitForSecondsRealtime(.085f); }
            Click("Upgrade forge");
            Check(demo.Game.Progress.forgeLevel == 1 && demo.Game.Progress.coins == 20, "Forge button");
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
            Click("Toggle speed"); Check(demo.FastRoll, "Fast animation mode");
            yield return null;
            Capture("unity-roulette.png");
            yield return new WaitForEndOfFrame();
            Click("ЗАПУСТИТЬ КИРКИ");
            Check(demo.IsBusy && demo.Game.Progress.tickets == 3, "Spin starts once");
            int[] committed = (int[])demo.Game.Progress.depths.Clone();
            demo.Spin();
            Check(demo.Game.Progress.tickets == 3, "Double spin blocked");
            float deadline = Time.realtimeSinceStartup + 8;
            while (demo.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!demo.IsBusy, "Animation completes");
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
            demo.Spin();
            while (demo.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            Capture("unity-crystal-shaft.png");
            demo.Game.Progress.stratum = 7; demo.Game.Progress.tickets = 1;
            Array.Clear(demo.Game.Progress.depths, 0, 5); Array.Clear(demo.Game.Progress.damage, 0, 5);
            demo.Spin();
            while (demo.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            Capture("unity-ember-shaft.png");
            demo.Game.Progress.totalBlocks = 25000000; demo.Game.Progress.claimedContracts = 999999;
            demo.ToggleSound();
            foreach (var size in new[] { new Vector2Int(320, 640), new Vector2Int(390, 844), new Vector2Int(960, 540) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.4f);
                CheckLayout();
                Capture("unity-" + size.x + "x" + size.y + ".png");
            }
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

        private void Check(bool condition, string label)
        {
            if (!condition) Debug.LogError("Smoke check failed: " + label);
        }

        // Render UI offscreen: minimized/hidden Windows players may return black backbuffer captures.
        private void Capture(string file)
        {
            var canvas = GetComponentInChildren<Canvas>();
            var stage = FindObjectOfType<VoxelStage>(); if (stage != null) stage.RenderNow();
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
            foreach (var button in GetComponentsInChildren<Button>(false))
            {
                if (!button.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                Check(button.interactable, "Button enabled: " + prefix);
                if (button.interactable) button.onClick.Invoke();
                return;
            }
            Debug.LogError("Smoke check: missing button " + prefix);
        }
    }
}
