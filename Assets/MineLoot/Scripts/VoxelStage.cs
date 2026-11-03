using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // A real, self-contained block diorama. Static faces are batched by material.
    // The UI consumes its camera texture; no background artwork or network assets.
    public sealed partial class VoxelStage : MonoBehaviour
    {
        private const int Layer = 30;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Geometry> terrain = new Dictionary<string, Geometry>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> pieces = new List<GameObject>();
        private PixelArt art;
        private Camera view;
        private Transform ore;
        private Material oreMaterial;
        private MeshRenderer crackRenderer;
        private Material crackMaterial;
        private readonly float[] particleAge = new float[96], particleLife = new float[96];
        private readonly Vector3[] particleOrigin = new Vector3[96], particleVelocity = new Vector3[96];
        private MaterialPropertyBlock particleProperties;
        private float hit, flash, clock, replacement = -1, damageFraction;
        private int particleCursor;
        private string desiredOre = "coal";
        private int biome = -1;
        private readonly Transform[] dust = new Transform[12];
        private readonly float[] dustTime = new float[12];
        private readonly Vector3[] dustOrigin = new Vector3[12];
        private int dustCursor;
        public RenderTexture Target { get; private set; }
        public Camera View => view;
        public Vector3 MiningOrePosition => ore.position;
        public int StaticBatches => terrain.Count;
        public int OreVertices => ore == null ? 0 : ore.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        public bool CrackVisible => crackRenderer != null && crackRenderer.enabled;
        public string DisplayedOre => oreMaterial.mainTexture.name;
        public bool IsReplacing => replacement >= 0;
        public bool IsExpedition { get; private set; }
        public bool IsEnchanting { get; private set; }

        public void ResizeViewport(int width, int height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            if (Target.width == width && Target.height == height) return;
            var previous = Target;
            Target = new RenderTexture(width, height, 24) { name = "Block world view", filterMode = FilterMode.Point, antiAliasing = 1 };
            view.targetTexture = Target; view.aspect = width / (float)height;
            previous.Release();
            if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous);
        }

        public void Initialize(PixelArt art, bool expedition = false, bool enchanting = false)
        {
            this.art = art;
            IsExpedition = expedition; IsEnchanting = enchanting;
            if (expedition || enchanting) transform.localPosition = Vector3.right * (enchanting ? 2000 : 1000);
            particleProperties = new MaterialPropertyBlock();
            gameObject.layer = Layer;
            Target = new RenderTexture(expedition ? 432 : 864, expedition ? 417 : 560, 24) { name = "Block world view", filterMode = FilterMode.Point, antiAliasing = 1 };
            var cameraObject = new GameObject("Diorama camera", typeof(Camera)); cameraObject.transform.SetParent(transform, false);
            view = cameraObject.GetComponent<Camera>(); view.orthographic = true; view.orthographicSize = 3.4f;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = ColorOf("8fcaea"); view.cullingMask = 1 << Layer;
            view.nearClipPlane = .1f; view.farClipPlane = 100; view.targetTexture = Target; view.depth = -10;
            view.transform.localPosition = new Vector3(8.3f, 6.4f, -10.8f); view.transform.LookAt(transform.position + new Vector3(0, .8f, 1));
            Camera.main.cullingMask &= ~(1 << Layer);
            if (enchanting)
            {
                view.orthographic = false; view.fieldOfView = 52; view.backgroundColor = ColorOf("292337");
                view.transform.localPosition = new Vector3(0, 2.1f, -4);
                view.transform.LookAt(transform.position + new Vector3(0, 1.25f, .45f));
                BuildEnchantingRoom(); return;
            }
            if (expedition)
            {
                view.orthographicSize = 4.65f;
                view.transform.localPosition = new Vector3(2.6f, 4.5f, -23);
                view.transform.LookAt(transform.position);
                BuildExpedition(); InitializeChips(); SetBiome(0); return;
            }
            BuildTerrain(); BuildDevelopment();
            var shadowMaterial = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("shadow") };
            materials.Add("ore shadow", shadowMaterial);
            var shadow = new Geometry(); AddFace(shadow, new Vector3(-.4f,.015f,-.8f), new Vector3(3.3f,0,3.3f), 4, 1);
            MakeMesh("Contact shadow", shadow, shadowMaterial);
            var mainOre = new Geometry(); AddCube(mainOre, Vector3.zero, Vector3.one * 1.85f, 1);
            oreMaterial = new Material(GetMaterial("mine-coal")); materials.Add("Mining ore", oreMaterial);
            ore = MakeMesh("Clickable ore block", mainOre, oreMaterial).transform;
            ore.localPosition = new Vector3(-.4f, .95f, -.8f); ore.localEulerAngles = new Vector3(0, -8, 0);
            var crackGeometry = new Geometry(); AddCube(crackGeometry, Vector3.zero, Vector3.one * 1.864f, 1);
            crackMaterial = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("crack1") };
            materials.Add("Mining cracks", crackMaterial);
            var crack = MakeMesh("Progressive cracks", crackGeometry, crackMaterial); crack.transform.SetParent(ore, false);
            crackRenderer = crack.GetComponent<MeshRenderer>(); crackRenderer.enabled = false;
            InitializeChips();
            InitializeDust();
            SetBiome(0);
        }

        private void InitializeDust()
        {
            var material = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("dust") };
            materials.Add("Mining dust", material);
            for (int i = 0; i < dust.Length; i++)
            {
                var shape = new Geometry(); AddFace(shape, Vector3.zero, Vector3.one, 0, 1);
                dust[i] = MakeMesh("Mining dust " + i, shape, material).transform;
                dust[i].gameObject.SetActive(false);
            }
        }

        private void EmitDust(bool broken)
        {
            for (int i = 0; i < (broken ? 10 : 2); i++)
            {
                int p = dustCursor++ % dust.Length; float angle = dustCursor * 2.4f;
                dustTime[p] = 0;
                dustOrigin[p] = ore.localPosition + new Vector3(Mathf.Cos(angle) * .65f, i % 3 * .2f - .3f, -.9f + Mathf.Sin(angle) * .3f);
                dust[p].rotation = view.transform.rotation;
                dust[p].gameObject.SetActive(true);
            }
        }

        private void InitializeChips()
        {
            for (int i = 0; i < particleAge.Length; i++)
            {
                var chip = new Geometry(); AddCube(chip, Vector3.zero, Vector3.one * .13f, .16f);
                var item = MakeMesh("Ore chip", chip, GetMaterial("coal")); item.SetActive(false); pieces.Add(item);
            }
        }

        private void BuildTerrain()
        {
            // A compact underground quarry: calm stone, structural timber and warm lanterns.
            // The centre stays open so the active ore and its fragments have a clear silhouette.
            for (int x = -6; x <= 6; x++) for (int z = -4; z <= 5; z++)
            {
                Block(new Vector3(x, -.32f, z), new Vector3(.98f, .6f, .98f), "mine-floor");
                if (z == -4) Block(new Vector3(x, -.72f, z), new Vector3(.98f, .22f, 1), "mine-wall");
            }
            for (int x = -6; x <= 6; x++) for (int y = 0; y < 5; y++)
            {
                if (x >= 1 && x <= 3 && y < 3) continue;
                float inset = (x + y * 3) % 4 == 0 ? .18f : 0;
                Block(new Vector3(x, .5f + y, 4.8f + inset), new Vector3(.98f, .98f, 1), "mine-wall");
            }
            Block(new Vector3(2, 1.5f, 5.1f), new Vector3(3.1f, 3, .2f), "tunnel-dark");
            foreach (float x in new[] { .45f, 3.55f })
            {
                Block(new Vector3(x, 1.35f, 3.15f), new Vector3(.35f, 2.8f, .4f), "mine-timber");
                Block(new Vector3(x, .18f, 3.15f), new Vector3(.55f, .38f, .62f), "mine-steel");
                Block(new Vector3(x, 2.04f, 2.83f), new Vector3(.42f, .08f, .4f), "mine-steel");
                Block(new Vector3(x, 1.8f, 2.83f), new Vector3(.27f, .4f, .26f), "mine-lamp");
                Block(new Vector3(x, 1.56f, 2.83f), new Vector3(.42f, .08f, .4f), "mine-steel");
            }
            Block(new Vector3(2, 2.65f, 3.15f), new Vector3(3.55f, .38f, .46f), "mine-timber");
            foreach (float x in new[] { -4.6f, 4.9f })
            {
                Block(new Vector3(x, 1.65f, 3.9f), new Vector3(.35f, 3.3f, .5f), "mine-timber");
                Block(new Vector3(x, 2.75f, 3.5f), new Vector3(.65f, .13f, .7f), "mine-steel");
                Block(new Vector3(x, 2.45f, 3.5f), new Vector3(.34f, .48f, .34f), "mine-lamp");
                Block(new Vector3(x, 2.16f, 3.5f), new Vector3(.52f, .12f, .52f), "mine-steel");
            }
            Block(new Vector3(0, 3.3f, 4), new Vector3(10.1f, .35f, .5f), "mine-timber");
            // Tracks lead to the lit mine opening, rather than floating on grass.
            for (int z = -2; z <= 6; z++)
            {
                Block(new Vector3(2, .035f, z), new Vector3(1.55f, .09f, .2f), "mine-timber");
                foreach (float x in new[] { 1.47f, 2.53f }) Block(new Vector3(x, .11f, z), new Vector3(.08f, .09f, 1.02f), "mine-steel");
            }
            // Deliberate clusters of exposed minerals; no noisy full-screen texture.
            foreach (var p in new[] { new Vector3(-3, 1.2f, 4.21f), new Vector3(-2, 2.2f, 4.21f), new Vector3(5, .8f, 4.21f) })
                Block(p, Vector3.one * .86f, p.x > 0 ? "mine-copper" : "mine-coal");
            Block(new Vector3(-.4f, -.015f, -.8f), new Vector3(2.55f, .14f, 2.55f), "mine-wall");
            foreach (var p in new[] { new Vector3(-1.8f,.13f,-2), new Vector3(.8f,.1f,-1.9f), new Vector3(-3.8f,.12f,1.5f) })
                Block(p, new Vector3(.3f, .22f, .34f), "mine-floor");
            foreach (var pair in terrain) MakeMesh("Batched " + pair.Key, pair.Value, GetMaterial(pair.Key));
        }

        private void Tree(float x, float z)
        {
            Block(new Vector3(x,1.4f,z),new Vector3(.6f,3.4f,.6f),"wood");
            Block(new Vector3(x,3.35f,z),new Vector3(2.7f,1.3f,2.7f),"leaves");
            Block(new Vector3(x,4.25f,z),new Vector3(1.8f,.7f,1.8f),"leaves");
        }

        private void Block(Vector3 center, Vector3 size, string side, string top = null, Dictionary<string, Geometry> batch = null)
        {
            var target = batch ?? terrain;
            // Group each face by texture, retaining per-face shade in a small custom shader.
            for (int f = 0; f < 6; f++)
            {
                string key = f == 4 && top != null ? top : side;
                if (!target.TryGetValue(key, out var geometry)) { geometry = new Geometry(); target.Add(key, geometry); }
                AddFace(geometry, center, size, f, 1);
            }
        }

        public void SetOre(string key, float damage)
        {
            desiredOre = key; damageFraction = Mathf.Clamp01(damage);
            if (replacement < 0 || replacement >= .08f) oreMaterial.mainTexture = art.Texture("tex-mine-" + desiredOre);
            crackMaterial.mainTexture = art.Texture("crack" + Mathf.Clamp(1 + Mathf.FloorToInt(damageFraction * 6), 1, 6));
            crackRenderer.enabled = damageFraction > 0 && replacement < 0;
        }

        public void Hit(bool broken)
        {
            // Finish the prior replacement if a stronger pick already breaks the next block.
            if (replacement >= 0) { oreMaterial.mainTexture = art.Texture("tex-mine-" + desiredOre); replacement = -1; }
            hit = flash = 1;
            EmitDust(broken);
            EmitChips(ore.localPosition + (broken ? Vector3.up * .2f : new Vector3(.55f, .1f, -.8f)), oreMaterial.mainTexture, broken ? 44 : 7, broken);
            if (broken) { replacement = 0; crackRenderer.enabled = false; }
        }

        private void EmitChips(Vector3 origin, Texture texture, int count, bool broken)
        {
            particleProperties.Clear();
            for (int i = 0; i < count; i++)
            {
                int p = particleCursor++ % pieces.Count;
                float angle = particleCursor * 2.399963f;
                particleAge[p] = 0; particleLife[p] = broken ? .85f : .42f;
                particleOrigin[p] = origin;
                float force = broken ? 3.4f : 1.3f;
                particleVelocity[p] = new Vector3(Mathf.Cos(angle) * force, 1.7f + i % 4 * .35f, Mathf.Sin(angle) * force);
                particleProperties.SetTexture("_MainTex", texture);
                pieces[p].GetComponent<MeshRenderer>().SetPropertyBlock(particleProperties);
                pieces[p].SetActive(true);
            }
        }
        public void SetBiome(int index)
        {
            if (biome == index) return; biome = index;
            if (IsExpedition) { SetExpeditionBiome(index); return; }
            view.backgroundColor = index == 0 ? ColorOf("213942") : index == 1 ? ColorOf("2b3448") : ColorOf("42333c");
        }
        public bool Paused { get; set; }
        private void Update()
        {
            if (Paused) return;
            clock += Time.unscaledDeltaTime;
            if (IsEnchanting) { UpdateEnchantingRoom(); return; }
            if (IsExpedition) UpdateExpedition();
            else
            {
                UpdateDevelopment(Time.unscaledDeltaTime);
                for (int i = 0; i < dust.Length; i++) if (dust[i].gameObject.activeSelf)
                {
                    float t = dustTime[i] += Time.unscaledDeltaTime;
                    if (t >= .65f) { dust[i].gameObject.SetActive(false); continue; }
                    dust[i].localPosition = dustOrigin[i] + new Vector3(Mathf.Sin(i * 2.4f) * t * .6f, t * .7f, 0);
                    dust[i].localScale = Vector3.one * (.15f + t * (i % 3 + 1) * .8f);
                    particleProperties.Clear(); particleProperties.SetColor("_Color", new Color(1, 1, 1, (1 - t / .65f) * .45f));
                    dust[i].GetComponent<MeshRenderer>().SetPropertyBlock(particleProperties);
                }
                hit = Mathf.MoveTowards(hit, 0, Time.unscaledDeltaTime * 6);
                flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 10);
                float scale = 1 - hit * .085f;
                if (replacement >= 0)
                {
                    replacement += Time.unscaledDeltaTime;
                    if (replacement < .08f) scale = 1 - replacement / .08f;
                    else
                    {
                        oreMaterial.mainTexture = art.Texture("tex-mine-" + desiredOre);
                        float t = Mathf.Clamp01((replacement - .08f) / .16f) - 1;
                        scale = 1 + 2.70158f * t * t * t + 1.70158f * t * t;
                    }
                    if (replacement >= .24f) replacement = -1;
                }
                ore.localScale = new Vector3(1 + hit * .025f, Mathf.Max(.001f, scale), 1 + hit * .025f) * (replacement >= 0 ? Mathf.Max(.001f, scale) : 1);
                ore.localPosition = new Vector3(-.4f + Mathf.Sin(hit * 20) * hit * .065f, .95f - hit * .055f, -.8f);
                ore.localEulerAngles = new Vector3(hit * 4, -8, hit * 3);
                oreMaterial.color = Color.white * (1 + flash * .3f);
                crackRenderer.enabled = damageFraction > 0 && replacement < 0;
            }
            for(int i=0;i<pieces.Count;i++)
            {
                if (!pieces[i].activeSelf) continue;
                float t = particleAge[i] += Time.unscaledDeltaTime;
                if (t >= particleLife[i]) { pieces[i].SetActive(false); continue; }
                Vector3 point = particleOrigin[i] + particleVelocity[i] * t + Vector3.down * (7 * t * t);
                if (point.y < .08f) point.y = .08f + Mathf.Abs(point.y - .08f) * .2f;
                pieces[i].transform.localPosition = point;
                pieces[i].transform.localEulerAngles = new Vector3(i * 27 + t * 180, i * 19 + t * 230, 0);
                pieces[i].transform.localScale = Vector3.one * (.45f + (i % 5) * .22f) * (1 - Mathf.Pow(t / particleLife[i], 3));
            }
            if (materials.TryGetValue("torch", out var torch)) torch.color = new Color(1.2f + Mathf.Sin(clock * 7) * .08f, 1.07f, .8f);
        }
        public void RenderNow() { if(view != null && view.enabled) view.Render(); }

        private Material GetMaterial(string key)
        {
            if(materials.TryGetValue(key,out var material)) return material;
            material=new Material(Resources.Load<Shader>("VoxelWorld")) { name="Voxel "+key };
            material.SetFloat("_Shade", IsExpedition ? .53f : .68f);
            string tex = key=="black" || key=="tunnel-dark" ? "stone" : key=="failure" ? "metal-dark" : key=="flower" ? "gold" : key=="cloud" || key=="smoke" ? "stone" : key=="torch" ? "gold" : key;
            material.mainTexture=art.Texture("tex-"+tex);
            if(key=="black") material.color=ColorOf("20262d");
            if(key=="tunnel-dark") material.color=ColorOf("020609");
            if(key=="failure") material.color=ColorOf("e86b61");
            if(key=="cloud") material.color=new Color(1.35f,1.35f,1.35f,1);
            if(key=="smoke") material.color=ColorOf("bcc0ba");
            if(key=="flower" || key=="torch") material.color=new Color(1.15f,1.05f,.8f,1);
            materials.Add(key,material); return material;
        }
        private GameObject MakeMesh(string name, Geometry data, Material material)
        {
            var mesh=new Mesh { name=name }; mesh.SetVertices(data.vertices); mesh.SetNormals(data.normals); mesh.SetUVs(0,data.uv); mesh.SetTriangles(data.indices,0); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.layer=Layer; go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=material; return go;
        }
        private static void AddCube(Geometry g, Vector3 center, Vector3 size, float uvScale) { for(int face=0;face<6;face++) AddFace(g,center,size,face,uvScale); }
        private static readonly Vector3[] Normals={Vector3.back,Vector3.right,Vector3.forward,Vector3.left,Vector3.up,Vector3.down};
        private static readonly Vector3[] Rights={Vector3.right,Vector3.forward,Vector3.left,Vector3.back,Vector3.right,Vector3.right};
        private static readonly Vector3[] Ups={Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.forward,Vector3.back};
        private static void AddFace(Geometry g,Vector3 center,Vector3 size,int face,float uvScale)
        {
            Vector3 n=Normals[face],r=Rights[face],u=Ups[face]; int v=g.vertices.Count;
            foreach(var corner in new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f)})
            {
                g.vertices.Add(center+Vector3.Scale(n*.5f+r*corner.x+u*corner.y,size)); g.normals.Add(n); g.uv.Add((corner+Vector2.one*.5f)*uvScale);
            }
            // The chosen right/up basis faces inward in Unity winding, reverse for the visible exterior.
            g.indices.AddRange(new[]{v,v+2,v+1,v,v+3,v+2});
        }
        private static Color ColorOf(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out var c); return c; }
        private sealed class Geometry
        {
            public readonly List<Vector3> vertices=new List<Vector3>(), normals=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>(); public readonly List<int> indices=new List<int>();
        }
        private void OnDestroy()
        {
            if(view!=null) view.targetTexture=null;
            if(Target!=null)
            {
                Target.Release();
                if(Application.isPlaying) Destroy(Target); else DestroyImmediate(Target);
            }
            foreach(var mesh in meshes)
                if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            foreach(var material in materials.Values)
                if(Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
