using System.Runtime.InteropServices;

namespace MineLoot
{
    // Thin browser boundary; native builds keep using the same controller and PlayerPrefs.
    public static class BrowserPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void LP_Attach(string receiver);
        [DllImport("__Internal")] private static extern void LP_Ready();
        [DllImport("__Internal")] private static extern int LP_Authorize();
        [DllImport("__Internal")] private static extern void LP_CloudStart(string json, int existed);
        [DllImport("__Internal")] private static extern void LP_CloudSync();
        [DllImport("__Internal")] private static extern void LP_CloudResolve(string token, int remote);
        [DllImport("__Internal")] private static extern int LP_SubmitRating(string json);
        [DllImport("__Internal")] private static extern int LP_RequestRating();
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
        public static void StartCloud(string json, bool existed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_CloudStart(json, existed ? 1 : 0);
#endif
        }
        public static void SyncCloud()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_CloudSync();
#endif
        }
        public static void ResolveCloud(string token, bool remote)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LP_CloudResolve(token, remote ? 1 : 0);
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
        public static bool Authorize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LP_Authorize()==1;
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
        public static bool SubmitRating(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LP_SubmitRating(json)==1;
#else
            return false;
#endif
        }
        public static bool RequestRating()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LP_RequestRating()==1;
#else
            return false;
#endif
        }
    }
}
