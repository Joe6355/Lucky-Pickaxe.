using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // Short original synthesized effects; no downloaded sound assets.
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource source;
        public bool Muted { get; private set; }
        public bool Suspended { get; private set; }
        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.volume = .22f;
            if (FindObjectOfType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            Muted = !DemoController.SmokeMode && PlayerPrefs.GetInt("LuckyPickaxe.Muted", 0) == 1;
            Create("hit", 130, .055f, false); Create("tick", 440, .035f, false);
            Create("reward", 660, .3f, true); Create("upgrade", 330, .22f, true);
            Create("break", 90, .09f, false);
            Create("metal", 240, .06f, false); Create("crystal", 740, .075f, false);
            Create("level", 520, .45f, true);
            Create("reel-stop", 175, .075f, false); Create("rare", 880, .19f, true);
            Create("chest", 330, .18f, true);
            Create("shatter", 65, .16f, false);
        }
        public void Toggle()
        {
            Muted = !Muted;
            if (!DemoController.SmokeMode) { PlayerPrefs.SetInt("LuckyPickaxe.Muted", Muted ? 1 : 0); PlayerPrefs.Save(); }
        }
        public void Play(string name)
        {
            if (!Muted && !Suspended && clips.TryGetValue(name, out var clip)) source.PlayOneShot(clip);
        }
        public void SetSuspended(bool suspended)
        {
            Suspended = suspended; source.mute = suspended;
            if (suspended) source.Stop();
        }
        private void Create(string name, float frequency, float duration, bool melody)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, p = t / duration;
                float f = melody ? frequency * (p < .33f ? 1 : p < .66f ? 1.25f : 1.5f) : frequency * (1 - p * .3f);
                phase += 2 * Mathf.PI * f / rate;
                samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * p) * (1 - p) * .6f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); clips.Add(name, clip);
        }
        private void OnDestroy() { foreach (var clip in clips.Values) Destroy(clip); }
    }
}
