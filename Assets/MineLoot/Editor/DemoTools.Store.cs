using System.IO;
using MineLoot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class DemoTools
{
    // Store artwork is rendered from the same original voxel world and sprites as the game.
    [MenuItem("Mine Loot/Export store images")]
    public static void ExportStoreImages()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new System.Exception("Use Unity 2022.3.47f1.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play mode before export.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new System.InvalidOperationException("Save all open scenes before export.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        foreach (var scene in previous)
            if (string.IsNullOrEmpty(scene.path) && previous.Length > 1)
                throw new System.InvalidOperationException("Save all open scenes before export.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PixelArt art = null;
        GameObject main = null, worldObject = null;
        try
        {
            art = new PixelArt();
            main = new GameObject("Store camera", typeof(Camera)); main.tag = "MainCamera";
            var camera = main.GetComponent<Camera>(); camera.orthographic = true;
            camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            worldObject = new GameObject("Store voxel world");
            var world = worldObject.AddComponent<VoxelStage>(); world.Initialize(art);
            world.SetDevelopment(4, 6, false); world.SetOre("diamond", 0);
            string folder = "Docs/store"; Directory.CreateDirectory(folder);
            var view = world.View;
            Vector3 originalPosition = view.transform.position;
            Quaternion originalRotation = view.transform.rotation;
            float originalSize = view.orthographicSize;
            var ore = new Vector3(-.4f, .95f, -.8f);
            view.transform.position = ore + new Vector3(4.2f, 3.8f, -6);
            view.transform.LookAt(ore); view.orthographicSize = 1.75f;
            ExportStoreImage(camera, world, art, folder + "/icon.png", 512, 512, false);
            view.transform.SetPositionAndRotation(originalPosition, originalRotation); view.orthographicSize = originalSize;
            ExportStoreImage(camera, world, art, folder + "/cover.png", 800, 470, true);
            Debug.Log("MINE_LOOT_STORE_IMAGES: PASS (512x512 icon, 800x470 cover)");
        }
        finally
        {
            if (worldObject != null) Object.DestroyImmediate(worldObject);
            if (main != null) Object.DestroyImmediate(main);
            art?.Dispose();
            if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static void ExportStoreImage(Camera camera, VoxelStage world, PixelArt art, string path, int width, int height, bool cover)
    {
        var output = new RenderTexture(width, height, 24) { antiAliasing = 1, filterMode = FilterMode.Point };
        var background = new RenderTexture(width, height, 24) { antiAliasing = 1, filterMode = FilterMode.Point };
        var canvasObject = new GameObject("Store canvas", typeof(RectTransform), typeof(Canvas)); canvasObject.layer = 5;
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var root = canvasObject.GetComponent<RectTransform>(); root.pivot = Vector2.zero; root.sizeDelta = new Vector2(width, height);
        var priorTarget = world.View.targetTexture;
        var priorActive = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            world.View.targetTexture = background; world.View.Render();
            var image = StoreRect(root, "World", 0, 0, width, height).gameObject.AddComponent<RawImage>(); image.texture = background;
            if (cover)
            {
                var plate = StoreRect(root, "Title plate", 0, height - 116, width, 116).gameObject.AddComponent<Image>();
                plate.color = new Color(.13f, .17f, .12f, .9f);
                StoreSprite(root, art.Get("pick4"), 28, height - 104, 88, 88);
                var title = StoreRect(root, "Title", 128, height - 110, width - 148, 102).gameObject.AddComponent<Text>();
                title.font = Resources.Load<Font>("Fonts/Tiny5-Regular"); title.fontSize = 66;
                title.text = "КИРКА УДАЧИ"; title.alignment = TextAnchor.MiddleCenter; title.color = new Color32(255, 237, 173, 255);
                title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
                title.font.RequestCharactersInTexture(title.text, title.fontSize, FontStyle.Normal);
            }
            else
            {
                StoreSprite(root, art.Get("pick4"), 180, 166, 288, 288);
                foreach (var edge in new[] {new Rect(0,0,width,8),new Rect(0,height-8,width,8),new Rect(0,0,8,height),new Rect(width-8,0,8,height)})
                    StoreRect(root, "Border", edge.x, edge.y, edge.width, edge.height).gameObject.AddComponent<Image>().color = new Color32(45, 59, 40, 255);
            }
            camera.transform.position = new Vector3(width / 2f, height / 2f, -100);
            camera.orthographicSize = height / 2f; camera.aspect = width / (float)height; camera.targetTexture = output;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = output;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            world.View.targetTexture = priorTarget; camera.targetTexture = null; RenderTexture.active = priorActive;
            Object.DestroyImmediate(canvasObject); if (pixels != null) Object.DestroyImmediate(pixels);
            output.Release(); background.Release(); Object.DestroyImmediate(output); Object.DestroyImmediate(background);
        }
    }
    private static RectTransform StoreRect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = 5;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    private static void StoreSprite(Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        var image = StoreRect(parent, sprite.name, x, y, width, height).gameObject.AddComponent<Image>(); image.sprite = sprite;
    }
}
