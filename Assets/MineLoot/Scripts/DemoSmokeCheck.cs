using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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
            Capture("unity-clicker.png");
            yield return new WaitForEndOfFrame();
            Check(demo.Game.Progress.coins == 1240, "Initial wallet");
            for (int i = 0; i < 5; i++) Click("Mine rock");
            Check(demo.Game.Progress.coins == 1365 && demo.Game.Progress.tickets == 4, "Clicker and ticket");
            Click("Upgrade pickaxe");
            Check(demo.Game.ClickPower == 35 && demo.Game.Progress.coins == 1015, "Upgrade button");
            Click("РУЛЕТКА ШАХТЁРА");
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
            for (int i = 0; i < 5; i++) Check(demo.Game.Progress.depths[i] == committed[i] && committed[i] > 0, "Depth retained");
            Click("ШАНСЫ И ПРАВИЛА");
            yield return null;
            Check(GameObject.Find("Rules modal") != null, "Rules modal");
            Click("ПОНЯТНО");
            yield return null;
            Capture("unity-roll-result.png");
            yield return new WaitForSecondsRealtime(.3f);
            Screen.SetResolution(960, 540, false);
            yield return new WaitForSecondsRealtime(.6f);
            Capture("unity-landscape.png");
            yield return new WaitForSecondsRealtime(.4f);
            File.WriteAllText(Path.Combine(output, "runtime-check.json"), "{\"result\":\"" + (errors == 0 ? "PASS" : "FAIL") + "\",\"errors\":" + errors + ",\"unity\":\"" + Application.unityVersion + "\"}");
            Debug.Log("MINE_LOOT_RUNTIME_CHECK: " + (errors == 0 ? "PASS" : "FAIL"));
            Application.Quit(errors == 0 ? 0 : 1);
        }

        private void Check(bool condition, string label)
        {
            if (!condition) Debug.LogError("Smoke check failed: " + label);
        }

        // Render UI offscreen: minimized/hidden Windows players may return black backbuffer captures.
        private void Capture(string file)
        {
            var canvas = GetComponentInChildren<Canvas>();
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
