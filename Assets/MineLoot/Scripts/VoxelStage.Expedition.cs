using UnityEngine;

namespace MineLoot
{
    // The same voxel renderer, materials and particle pool also draw the five-lane shaft.
    public sealed partial class VoxelStage
    {
        private readonly Transform[,] shaftBlocks = new Transform[5, 5];
        private readonly MeshRenderer[,] shaftRenderers = new MeshRenderer[5, 5];
        private readonly float[,] shaftHits = new float[5, 5];
        private readonly Transform[] shaftCracks = new Transform[5], chestLids = new Transform[5];
        private readonly Material[] shaftCrackMaterials = new Material[5];
        private readonly float[] chestOpenness = new float[5];
        private readonly bool[] chestTargets = new bool[5];
        private float shaftShake;
        private static readonly string[][] ShaftLayers = {
            new[] { "grass", "stone", "iron", "gold", "diamond" },
            new[] { "stone", "iron", "diamond", "stone", "diamond" },
            new[] { "stone", "ember", "iron", "ember", "gold" }
        };
        private static Vector3 ShaftPosition(int column, int row) => new Vector3((column - 2) * 1.8f, 1.93f - row * .98f, 0);
        public int VisibleBlocks
        {
            get { int count = 0; foreach (var block in shaftBlocks) if (block != null && block.gameObject.activeSelf) count++; return count; }
        }
        public int OpenChests
        {
            get { int count = 0; foreach (bool open in chestTargets) if (open) count++; return count; }
        }

        private void BuildExpedition()
        {
            var lightMaterial = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("torch-glow") };
            materials.Add("Torch atmosphere", lightMaterial);
            // Recessed masonry, structural beams and rails give the blocks a readable silhouette.
            for (int x = -5; x <= 5; x++) for (int y = -4; y <= 4; y++)
                Block(new Vector3(x, y, 1.5f), new Vector3(.98f, .98f, .65f), "slate");
            foreach (float x in new[] { -4.65f, 4.65f })
            {
                Block(new Vector3(x, -.15f, -.05f), new Vector3(.45f, 8, 1.6f), "beam");
                for (int y = -3; y <= 3; y += 2) Block(new Vector3(x, y, -.9f), new Vector3(.52f, .22f, .14f), "metal-dark");
                Block(new Vector3(x * .91f, 1.45f, -.2f), new Vector3(.14f, .7f, .2f), "beam");
                Block(new Vector3(x * .91f, 1.9f, -.24f), new Vector3(.24f, .35f, .24f), "torch");
                Block(new Vector3(x * .91f, 2.03f, -.24f), new Vector3(.13f, .2f, .13f), "metal-gold");
                var glow = new Geometry(); AddFace(glow, new Vector3(x * .91f, 1.9f, .75f), new Vector3(1.75f, 2.2f, 0), 0, 1);
                MakeMesh("Warm torch light", glow, lightMaterial);
            }
            foreach (float y in new[] { 3.25f, -3.65f }) Block(new Vector3(0, y, .4f), new Vector3(9.5f, .36f, 1.2f), "beam");
            for (int c = 0; c < 5; c++)
            {
                float x = (c - 2) * 1.8f;
                Block(new Vector3(x, -.1f, 1.03f), new Vector3(1.7f, 5.65f, .2f), "black");
                foreach (float side in new[] { -.83f, .83f })
                    Block(new Vector3(x + side, -.05f, .7f), new Vector3(.09f, 5.7f, .13f), "metal-dark");
                for (int r = 0; r < 5; r++)
                {
                    var block = new Geometry(); AddCube(block, Vector3.zero, new Vector3(1.54f, .88f, 1.35f), 1);
                    var item = MakeMesh("Shaft ore " + c + "/" + r, block, GetMaterial(ShaftLayers[0][r]));
                    item.transform.localPosition = ShaftPosition(c, r);
                    shaftBlocks[c, r] = item.transform; shaftRenderers[c, r] = item.GetComponent<MeshRenderer>();
                }
                var crackGeometry = new Geometry(); AddCube(crackGeometry, Vector3.zero, new Vector3(1.553f, .893f, 1.363f), 1);
                var material = new Material(Resources.Load<Shader>("VoxelShadow")) { mainTexture = art.Texture("crack1") };
                materials.Add("Shaft cracks " + c, material); shaftCrackMaterials[c] = material;
                shaftCracks[c] = MakeMesh("Shaft cracks " + c, crackGeometry, material).transform;
                shaftCracks[c].gameObject.SetActive(false);
                BuildChest(c, x);
            }
            foreach (var pair in terrain) MakeMesh("Shaft batched " + pair.Key, pair.Value, GetMaterial(pair.Key));
        }

        private void BuildChest(int column, float x)
        {
            Vector3 center = new Vector3(x, -2.95f, -.08f);
            var body = new Geometry(); AddCube(body, center, new Vector3(1.05f, .53f, .76f), 1);
            MakeMesh("Treasure chest " + column, body, GetMaterial("planks"));
            var metal = new Geometry();
            foreach (float dx in new[] { -.4f, .4f }) AddCube(metal, center + new Vector3(dx, 0, -.39f), new Vector3(.09f, .56f, .045f), 1);
            AddCube(metal, center + new Vector3(0, .09f, -.42f), new Vector3(.19f, .26f, .08f), 1);
            MakeMesh("Chest straps " + column, metal, GetMaterial("metal-gold"));
            var hinge = new GameObject("Chest lid hinge " + column).transform; hinge.SetParent(transform, false);
            hinge.localPosition = center + new Vector3(0, .29f, .36f); chestLids[column] = hinge;
            var lid = new Geometry(); AddCube(lid, new Vector3(0, .05f, -.38f), new Vector3(1.09f, .22f, .8f), 1);
            var top = MakeMesh("Chest lid " + column, lid, GetMaterial("planks")); top.transform.SetParent(hinge, false);
            var rim = new Geometry();
            foreach (float dx in new[] { -.42f, .42f }) AddCube(rim, new Vector3(dx, .17f, -.38f), new Vector3(.09f, .04f, .82f), 1);
            var bands = MakeMesh("Lid brass " + column, rim, GetMaterial("metal-gold")); bands.transform.SetParent(hinge, false);
            var treasure = new Geometry();
            for (int i = 0; i < 5; i++) AddCube(treasure, center + new Vector3((i % 3 - 1) * .24f, .29f + i % 2 * .06f, -.12f + i / 3 * .22f), new Vector3(.2f, .12f, .2f), 1);
            MakeMesh("Chest gold " + column, treasure, GetMaterial("metal-gold"));
        }

        private void SetExpeditionBiome(int index)
        {
            view.backgroundColor = ColorOf("1c2528");
            GetMaterial("slate").color = index == 1 ? ColorOf("91b2c3") : index == 2 ? ColorOf("c1a08b") : Color.white;
            for (int c = 0; c < 5; c++) for (int r = 0; r < 5; r++)
                shaftRenderers[c, r].sharedMaterial = GetMaterial(ShaftLayers[index][r]);
        }

        public void SetBoard(int[] depths, int[] damage, int hardness, int biomeIndex)
        {
            SetBiome(biomeIndex);
            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 5; r++) shaftBlocks[c, r].gameObject.SetActive(r >= depths[c]);
                bool open = depths[c] == 5;
                if (open && !chestTargets[c]) EmitChips(new Vector3((c - 2) * 1.8f, -2.6f, -.5f), art.Texture("tex-metal-gold"), 8, true);
                chestTargets[c] = open;
                shaftCracks[c].gameObject.SetActive(!open && damage[c] > 0);
                if (!open)
                {
                    shaftCracks[c].localPosition = ShaftPosition(c, depths[c]);
                    shaftCrackMaterials[c].mainTexture = art.Texture("crack" + Mathf.Clamp(1 + damage[c] * 3 / hardness, 1, 3));
                }
            }
        }

        public Vector2 BlockViewport(int column, int row)
        {
            Vector3 p = view.WorldToViewportPoint(transform.TransformPoint(ShaftPosition(column, row) + new Vector3(.25f, .22f, -.7f)));
            return new Vector2(p.x, p.y);
        }

        public void HitBlock(int column, int row, bool broken)
        {
            shaftHits[column, row] = 1; shaftShake = 1;
            EmitChips(ShaftPosition(column, row) + new Vector3(.2f, .1f, -.8f), shaftRenderers[column, row].sharedMaterial.mainTexture, broken ? 7 : 3, broken);
        }

        private void UpdateExpedition()
        {
            float dt = Time.unscaledDeltaTime;
            shaftShake = Mathf.MoveTowards(shaftShake, 0, dt * 8);
            view.transform.localPosition = new Vector3(3, 6 + Mathf.Sin(shaftShake * 20) * shaftShake * .025f, -21);
            for (int c = 0; c < 5; c++)
            {
                chestOpenness[c] = Mathf.MoveTowards(chestOpenness[c], chestTargets[c] ? 1 : 0, dt * 3.8f);
                float t = chestOpenness[c], ease = 1 - Mathf.Pow(1 - t, 3);
                chestLids[c].localEulerAngles = new Vector3(104 * ease, 0, 0);
                for (int r = 0; r < 5; r++)
                {
                    shaftHits[c, r] = Mathf.MoveTowards(shaftHits[c, r], 0, dt * 7);
                    float pulse = shaftHits[c, r];
                    shaftBlocks[c, r].localScale = Vector3.one * (1 - pulse * .045f);
                    particleProperties.Clear(); particleProperties.SetColor("_Color", Color.white * (1 + pulse * .5f));
                    shaftRenderers[c, r].SetPropertyBlock(particleProperties);
                }
            }
        }
    }
}
