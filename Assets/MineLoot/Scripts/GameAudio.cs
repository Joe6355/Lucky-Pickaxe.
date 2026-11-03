using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // Short original synthesized effects; no downloaded sound assets.
    public sealed class GameAudio : MonoBehaviour
    {
        public enum Channel { Effects, Mining, Roulette }
        public const string PreferencesKey = "MineLoot.Audio.v1";
        [Serializable] private sealed class Preferences
        {
            public int version = 1;
            public float master = 1, mining = 1, roulette = 1;
            public bool muted;
        }
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly AudioSource[] sources = new AudioSource[3];
        private Preferences preferences = new Preferences();
        private string preferencesKey = PreferencesKey;
        private bool dirty;
        public bool Muted => preferences.muted;
        public float MasterVolume => preferences.master;
        public float MiningVolume => preferences.mining;
        public float RouletteVolume => preferences.roulette;
        public bool Suspended { get; private set; }
        private void Awake()
        {
            for(int i=0;i<sources.Length;i++) { sources[i]=gameObject.AddComponent<AudioSource>();sources[i].playOnAwake=false; }
            if (FindObjectOfType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            LoadPreferences();
            Create("hit", 130, .055f, false); Create("tick", 440, .035f, false);
            Create("reward", 660, .3f, true); Create("upgrade", 330, .22f, true);
            Create("break", 90, .09f, false);
            Create("metal", 240, .06f, false); Create("crystal", 740, .075f, false);
            Create("level", 520, .45f, true);
            Create("reel-stop", 175, .075f, false); Create("rare", 880, .19f, true);
            Create("chest", 330, .18f, true);
            Create("shatter", 65, .16f, false);
            CreateImpact("stone-hit", .11f, 170, 47);
            CreateImpact("stone-break", .32f, 84, 6355);
        }
        private void CreateImpact(string name, float duration, float frequency, int seed)
        {
            const int rate = 22050;
            var rng = new System.Random(seed); var data = new float[Mathf.CeilToInt(duration * rate)];
            float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate, p = t / duration, noise = (float)rng.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, noise, .18f);
                float body = Mathf.Sin(t * frequency * 2 * Mathf.PI * (1 - p * .3f));
                data[i] = (low * .7f + body * .3f) * Mathf.Exp(-p * 7) * Mathf.Min(1, t * 1800);
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); clips.Add(name, clip);
        }
        public void Toggle()
        {
            preferences.muted=!Muted; Changed();
        }
        public void SetVolumes(float master,float mining,float roulette)
        {
            preferences.master=Volume(master);preferences.mining=Volume(mining);preferences.roulette=Volume(roulette);Changed();
        }
        public void ResetVolumes()
        {
            preferences=new Preferences();Changed();
        }
        private static float Volume(float value) => float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp01(value);
        public void Play(string name,Channel channel=Channel.Effects)
        {
            var source=sources[(int)channel];
            if (!Muted && !Suspended && source.volume>0 && clips.TryGetValue(name, out var clip)) source.PlayOneShot(clip);
        }
        public void SetSuspended(bool suspended)
        {
            Suspended = suspended;ApplyVolumes();
            if(suspended) FlushPreferences();
        }
        private void ApplyVolumes()
        {
            for(int i=0;i<sources.Length;i++)
            {
                var source=sources[i];source.volume=.22f*MasterVolume*(i==1?MiningVolume:i==2?RouletteVolume:1);
                source.mute=Suspended||Muted;
                if(source.mute||source.volume==0) source.Stop();
            }
        }
        private bool CanPersist => !DemoController.SmokeMode || preferencesKey!=PreferencesKey;
        private void LoadPreferences()
        {
            preferences=new Preferences();
            if(CanPersist)
            {
                preferences.muted=preferencesKey==PreferencesKey && PlayerPrefs.GetInt("LuckyPickaxe.Muted",0)==1;
                string json=BrowserPlatform.ReadProgress(preferencesKey)??PlayerPrefs.GetString(preferencesKey,"");
                try { var saved=JsonUtility.FromJson<Preferences>(json);if(saved!=null&&saved.version==1) preferences=saved; } catch(ArgumentException) { }
            }
            preferences.master=Volume(MasterVolume);preferences.mining=Volume(MiningVolume);preferences.roulette=Volume(RouletteVolume);
            dirty=false;ApplyVolumes();
        }
        private void Changed()
        {
            dirty=true;ApplyVolumes();
            // The browser journal is synchronous; only flush PlayerPrefs when the panel closes or focus changes.
            if(CanPersist) BrowserPlatform.WriteProgress(preferencesKey,JsonUtility.ToJson(preferences));
        }
        public void FlushPreferences()
        {
            if(!dirty||!CanPersist) return;
            string json=JsonUtility.ToJson(preferences);BrowserPlatform.WriteProgress(preferencesKey,json);
            PlayerPrefs.SetString(preferencesKey,json);PlayerPrefs.Save();dirty=false;
        }
        internal void UsePreferencesForSmoke(string key)
        {
            if(!DemoController.SmokeMode) return;
            preferencesKey=key;LoadPreferences();
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
        private void OnApplicationFocus(bool focused) { if(!focused) FlushPreferences(); }
        private void OnApplicationPause(bool paused) { if(paused) FlushPreferences(); }
        private void OnApplicationQuit() => FlushPreferences();
        private void OnDestroy() { FlushPreferences();foreach (var clip in clips.Values) Destroy(clip); }
    }
}
