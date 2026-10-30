using System.Runtime.InteropServices;

namespace MineLoot
{
    // Thin browser boundary; native builds keep using the same controller and PlayerPrefs.
    public static class BrowserPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void LP_Attach(string receiver);
        [DllImport("__Internal")] private static extern void LP_Ready();
        [DllImport("__Internal")] private static extern void LP_LanguageReady(string language);
        [DllImport("__Internal")] private static extern void LP_Gameplay(int active);
        [DllImport("__Internal")] private static extern int LP_ShowRewarded(string request);
        [DllImport("__Internal")] private static extern string LP_ReadProgress(string key);
        [DllImport("__Internal")] private static extern void LP_WriteProgress(string key, string json);
#endif
        public static void Attach(string receiver)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_Attach(receiver);
#endif
        }
        public static void Ready()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_Ready();
#endif
        }
        public static void LanguageReady(string language)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_LanguageReady(language);
#endif
        }
        public static void Gameplay(bool active)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_Gameplay(active ? 1 : 0);
#endif
        }
        public static string ReadProgress(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LP_ReadProgress(key);
#else
            return null;
#endif
        }
        public static bool ShowRewarded(string request)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LP_ShowRewarded(request) == 1;
#else
            return false;
#endif
        }
        public static void WriteProgress(string key, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_WriteProgress(key, json);
#endif
        }
    }
}
