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
        private readonly float[] particleAge = new float[48], particleLife = new float[48];
        private readonly Vector3[] particleOrigin = new Vector3[48], particleVelocity = new Vector3[48];
        private MaterialPropertyBlock particleProperties;
        private float hit, flash, clock, replacement = -1, damageFraction;
        private int particleCursor;
        private string desiredOre = "coal";
        private int biome = -1;
        public RenderTexture Target { get; private set; }
        public Camera View => view;
        public int StaticBatches => terrain.Count;
        public int OreVertices => ore == null ? 0 : ore.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        public bool CrackVisible => crackRenderer != null && crackRenderer.enabled;
        public string DisplayedOre => oreMaterial.mainTexture.name;
        public bool IsReplacing => replacement >= 0;
        public bool IsExpedition { get; private set; }

        public void Initialize(PixelArt art, bool expedition = false)
        {
            this.art = art;
            IsExpedition = expedition;
            if (expedition) transform.localPosition = Vector3.right * 1000;
            particleProperties = new MaterialPropertyBlock();
            gameObject.layer = Layer;
            Target = new RenderTexture(864, expedition ? 764 : 560, 24) { name = "Block world view", filterMode = FilterMode.Point, antiAliasing = 1 };
            var cameraObject = new GameObject("Diorama camera", typeof(Camera)); cameraObject.transform.SetParent(transform, false);
            view = cameraObject.GetComponent<Camera>(); view.orthographic = true; view.orthographicSize = 3.4f;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = ColorOf("8fcaea"); view.cullingMask = 1 << Layer;
            view.nearClipPlane = .1f; view.farClipPlane = 100; view.targetTexture = Target; view.depth = -10;
            view.transform.localPosition = new Vector3(8.3f, 6.4f, -10.8f); view.transform.LookAt(transform.position + new Vector3(0, .8f, 1));
            Camera.main.cullingMask &= ~(1 << Layer);
            if (expedition)
            {
                view.orthographicSize = 4.25f;
                view.transform.localPosition = new Vector3(3, 6, -21);
                view.transform.LookAt(transform.position);
                BuildExpedition(); InitializeChips(); SetBiome(0); return;
            }
            BuildTerrain();
            var shadowMaterial = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("shadow") };
            materials.Add("ore shadow", shadowMaterial);
            var shadow = new Geometry(); AddFace(shadow, new Vector3(-.4f,.015f,-.8f), new Vector3(3.3f,0,3.3f), 4, 1);
            MakeMesh("Contact shadow", shadow, shadowMaterial);
            var mainOre = new Geometry(); AddCube(mainOre, Vector3.zero, Vector3.one * 1.85f, 1);
            oreMaterial = new Material(GetMaterial("coal")); materials.Add("Mining ore", oreMaterial);
            ore = MakeMesh("Clickable ore block", mainOre, oreMaterial).transform;
            ore.localPosition = new Vector3(-.4f, .95f, -.8f); ore.localEulerAngles = new Vector3(0, -8, 0);
            var crackGeometry = new Geometry(); AddCube(crackGeometry, Vector3.zero, Vector3.one * 1.864f, 1);
            crackMaterial = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("crack1") };
            materials.Add("Mining cracks", crackMaterial);
            var crack = MakeMesh("Progressive cracks", crackGeometry, crackMaterial); crack.transform.SetParent(ore, false);
            crackRenderer = crack.GetComponent<MeshRenderer>(); crackRenderer.enabled = false;
            InitializeChips();
            SetBiome(0);
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
            // A stepped grassy clearing, with the visible dirt/stone cutaway at the front.
            for (int x = -6; x <= 6; x++) for (int z = -4; z <= 5; z++)
            {
                if (x * x / 40f + z * z / 28f > 1.5f) continue;
                float y = z >= 3 ? .15f + (x > 1 ? .5f : 0) : -.5f;
                if (x < -3 && z < 0) y -= .5f;
                Block(new Vector3(x, y - 1, z), Vector3.one, "dirt");
                Block(new Vector3(x, y, z), Vector3.one, "grass", "grass-top");
            }
            // Bright stepped cliff and the dark mouth of a timber mine.
            for (int x = 0; x <= 5; x++) for (int y = 1; y <= 3; y++)
            {
                if (x >= 1 && x <= 3 && y <= 2) continue;
                Block(new Vector3(x, y, 3.8f), Vector3.one, y == 3 ? "grass" : "stone", y == 3 ? "grass-top" : null);
            }
            Block(new Vector3(2, 1.25f, 4.3f), new Vector3(3.2f, 2.4f, .1f), "black");
            Block(new Vector3(.45f, 1.2f, 3.15f), new Vector3(.28f, 2.5f, .35f), "wood");
            Block(new Vector3(3.55f, 1.2f, 3.15f), new Vector3(.28f, 2.5f, .35f), "wood");
            Block(new Vector3(2, 2.4f, 3.15f), new Vector3(3.4f, .32f, .4f), "planks");
            for (int z = -1; z <= 3; z++)
            {
                Block(new Vector3(2, .08f, z), new Vector3(1.4f, .07f, .18f), "planks");
                Block(new Vector3(1.5f, .12f, z), new Vector3(.08f, .06f, 1), "iron");
                Block(new Vector3(2.5f, .12f, z), new Vector3(.08f, .06f, 1), "iron");
            }
            Tree(-3.7f, 1.7f); Tree(-5, 4.3f); Tree(5.4f, 4.1f);
            // A squat wooden supply crate, flowers, stepped grass and little torches.
            Block(new Vector3(-2.6f, .4f, -.5f), new Vector3(.9f, .8f, .7f), "planks");
            Block(new Vector3(-2.6f, .43f, -.87f), new Vector3(.12f, .21f, .06f), "gold");
            foreach (var p in new[] { new Vector3(-2.7f,.15f,-2), new Vector3(3.9f,.15f,-1.6f), new Vector3(-3.5f,.15f,0), new Vector3(.7f,.15f,1.6f) })
            {
                Block(p, new Vector3(.08f,.5f,.08f), "leaves");
                Block(p + Vector3.up*.24f, new Vector3(.32f,.14f,.14f), "flower");
                Block(p + Vector3.up*.24f, new Vector3(.14f,.14f,.32f), "flower");
            }
            foreach (float x in new[] { .2f, 3.8f })
            {
                Block(new Vector3(x,.8f,2.7f),new Vector3(.12f,.75f,.12f),"wood");
                Block(new Vector3(x,1.2f,2.7f),new Vector3(.17f,.24f,.17f),"torch");
            }
            for(int i=0;i<3;i++) Block(new Vector3(-5+i*4,5.7f+i*.3f,7+i),new Vector3(2.8f,.3f,1.2f),"cloud");
            foreach (var pair in terrain) MakeMesh("Batched " + pair.Key, pair.Value, GetMaterial(pair.Key));
        }

        private void Tree(float x, float z)
        {
            Block(new Vector3(x,1.4f,z),new Vector3(.6f,3.4f,.6f),"wood");
            Block(new Vector3(x,3.35f,z),new Vector3(2.7f,1.3f,2.7f),"leaves");
            Block(new Vector3(x,4.25f,z),new Vector3(1.8f,.7f,1.8f),"leaves");
        }

        private void Block(Vector3 center, Vector3 size, string side, string top = null)
        {
            // Group each face by texture, retaining per-face shade in a small custom shader.
            for (int f = 0; f < 6; f++)
            {
                string key = f == 4 && top != null ? top : side;
                if (!terrain.TryGetValue(key, out var geometry)) { geometry = new Geometry(); terrain.Add(key, geometry); }
                AddFace(geometry, center, size, f, 1);
            }
        }

        public void SetOre(string key, float damage)
        {
            desiredOre = key; damageFraction = Mathf.Clamp01(damage);
            if (replacement < 0 || replacement >= .08f) oreMaterial.mainTexture = art.Texture("tex-" + desiredOre);
            crackMaterial.mainTexture = art.Texture("crack" + Mathf.Clamp(1 + Mathf.FloorToInt(damageFraction * 3), 1, 3));
            crackRenderer.enabled = damageFraction > 0 && replacement < 0;
        }

        public void Hit(bool broken)
        {
            // Finish the prior replacement if a stronger pick already breaks the next block.
            if (replacement >= 0) { oreMaterial.mainTexture = art.Texture("tex-" + desiredOre); replacement = -1; }
            hit = flash = 1;
            EmitChips(ore.localPosition + (broken ? Vector3.up * .2f : new Vector3(.55f, .1f, -.8f)), oreMaterial.mainTexture, broken ? 16 : 4, broken);
            if (broken) { replacement = 0; crackRenderer.enabled = false; }
        }

        private void EmitChips(Vector3 origin, Texture texture, int count, bool broken)
        {
            particleProperties.Clear();
            for (int i = 0; i < count; i++)
            {
                int p = particleCursor++ % pieces.Count;
                float angle = particleCursor * 2.399963f;
                particleAge[p] = 0; particleLife[p] = broken ? .6f : .32f;
                particleOrigin[p] = origin;
                float force = broken ? 2.8f : 1.35f;
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
            view.backgroundColor = index == 0 ? ColorOf("8fcaea") : index == 1 ? ColorOf("a8d9ee") : ColorOf("efcc9b");
        }
        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (IsExpedition) UpdateExpedition();
            else
            {
                hit = Mathf.MoveTowards(hit, 0, Time.unscaledDeltaTime * 6);
                flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 10);
                float scale = 1 - hit * .06f;
                if (replacement >= 0)
                {
                    replacement += Time.unscaledDeltaTime;
                    if (replacement < .08f) scale = 1 - replacement / .08f;
                    else
                    {
                        oreMaterial.mainTexture = art.Texture("tex-" + desiredOre);
                        float t = Mathf.Clamp01((replacement - .08f) / .16f) - 1;
                        scale = 1 + 2.70158f * t * t * t + 1.70158f * t * t;
                    }
                    if (replacement >= .24f) replacement = -1;
                }
                ore.localScale = Vector3.one * Mathf.Max(.001f, scale);
                ore.localPosition = new Vector3(-.4f + Mathf.Sin(hit * 24) * hit * .055f, .95f, -.8f);
                ore.localEulerAngles = new Vector3(hit * 4, -8, hit * 3);
                oreMaterial.color = Color.white * (1 + flash * .3f);
                crackRenderer.enabled = damageFraction > 0 && replacement < 0;
            }
            for(int i=0;i<pieces.Count;i++)
            {
                if (!pieces[i].activeSelf) continue;
                float t = particleAge[i] += Time.unscaledDeltaTime;
                if (t >= particleLife[i]) { pieces[i].SetActive(false); continue; }
                pieces[i].transform.localPosition = particleOrigin[i] + particleVelocity[i] * t + Vector3.down * (5 * t * t);
                pieces[i].transform.localEulerAngles = new Vector3(i * 27 + t * 180, i * 19 + t * 230, 0);
                pieces[i].transform.localScale = Vector3.one * (1 - Mathf.Pow(t / particleLife[i], 3));
            }
            if (materials.TryGetValue("torch", out var torch)) torch.color = new Color(1.2f + Mathf.Sin(clock * 7) * .08f, 1.07f, .8f);
        }
        public void RenderNow() { if(view != null && view.enabled) view.Render(); }

        private Material GetMaterial(string key)
        {
            if(materials.TryGetValue(key,out var material)) return material;
            material=new Material(Resources.Load<Shader>("VoxelWorld")) { name="Voxel "+key };
            material.SetFloat("_Shade", IsExpedition ? .53f : .68f);
            string tex = key=="black" ? "stone" : key=="flower" ? "gold" : key=="cloud" ? "stone" : key=="torch" ? "gold" : key;
            material.mainTexture=art.Texture("tex-"+tex);
            if(key=="black") material.color=ColorOf("20262d");
            if(key=="cloud") material.color=new Color(1.35f,1.35f,1.35f,1);
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
            if(Target!=null) { Target.Release(); Destroy(Target); }
            foreach(var mesh in meshes) Destroy(mesh); foreach(var material in materials.Values) Destroy(material);
        }
    }
}
