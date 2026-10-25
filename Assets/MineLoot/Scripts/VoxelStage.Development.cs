using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // Development decorates the existing diorama; unlocks derive from the saved pick and forge levels.
    public sealed partial class VoxelStage
    {
        private readonly Transform[] mineStructures = new Transform[4], forgeStructures = new Transform[4];
        private readonly float[] mineBuildTime = new float[4], forgeBuildTime = new float[4];
        private readonly Transform[] cartWheels = new Transform[4], forgeSmoke = new Transform[6];
        private Transform cart, hoistWheel, hoistBucket, hoistRope, crystalSign;
        private MeshRenderer cartBody, cartCargo;
        private Vector3 smokeOrigin;
        private readonly Transform[] workers = new Transform[3], workerTools = new Transform[3], veinSparks = new Transform[6];
        private Transform drillRig, drillSpindle;
        public int VisibleWorkers { get { int count = 0; foreach (var worker in workers) if (worker != null && worker.gameObject.activeSelf) count++; return count; } }
        public bool DrillVisible => drillRig != null && drillRig.gameObject.activeSelf;
        public float DrillAngle => drillSpindle == null ? 0 : drillSpindle.localEulerAngles.y;
        public bool VeinVisible => veinSparks[0] != null && veinSparks[0].gameObject.activeSelf;
        private static readonly int[] ForgeMilestones = { 1, 3, 6, 10 };
        public int DevelopmentTier { get; private set; } = -1;
        public int ForgeStage { get; private set; }
        public int OwnedMeshCount => meshes.Count;
        public bool CartVisible => cart != null && cart.gameObject.activeSelf;
        public Vector3 CartPosition => cart == null ? Vector3.zero : cart.localPosition;
        public int VisibleMineStructures
        {
            get { int n = 0; foreach (var item in mineStructures) if (item != null && item.gameObject.activeSelf) n++; return n; }
        }
        public int VisibleForgeStructures
        {
            get { int n = 0; foreach (var item in forgeStructures) if (item != null && item.gameObject.activeSelf) n++; return n; }
        }

        private Transform DevelopmentGroup(string name, Dictionary<string, Geometry> batch, Vector3 origin, Vector3 pivot = default)
        {
            var root = new GameObject(name); root.layer = Layer; root.transform.SetParent(transform, false);
            root.transform.localPosition = origin + pivot;
            foreach (var pair in batch)
            {
                for (int v = 0; v < pair.Value.vertices.Count; v++) pair.Value.vertices[v] -= pivot;
                MakeMesh(name + " " + pair.Key, pair.Value, GetMaterial(pair.Key)).transform.SetParent(root.transform, false);
            }
            root.SetActive(false); return root.transform;
        }

        private void BuildDevelopment()
        {
            var batch = new Dictionary<string, Geometry>();
            Block(new Vector3(0, .42f, 0), new Vector3(1.04f, .15f, 1.3f), "planks", batch: batch);
            foreach (float x in new[] { -.49f, .49f }) Block(new Vector3(x, .67f, 0), new Vector3(.14f, .5f, 1.3f), "planks", batch: batch);
            foreach (float z in new[] { -.59f, .59f }) Block(new Vector3(0, .67f, z), new Vector3(.92f, .5f, .14f), "planks", batch: batch);
            Block(new Vector3(0, .35f, 0), new Vector3(.82f, .14f, 1.15f), "metal-dark", batch: batch);
            cart = mineStructures[0] = DevelopmentGroup("Working ore cart", batch, new Vector3(2, 0, 1.5f));
            foreach (var renderer in cart.GetComponentsInChildren<MeshRenderer>(true)) if (renderer.sharedMaterial == GetMaterial("planks")) cartBody = renderer;
            var load = new Geometry();
            AddCube(load, new Vector3(-.2f, .86f, -.25f), new Vector3(.45f, .42f, .45f), 1);
            AddCube(load, new Vector3(.23f, .91f, .15f), new Vector3(.42f, .46f, .48f), 1);
            AddCube(load, new Vector3(-.15f, .85f, .4f), Vector3.one * .36f, 1);
            cartCargo = MakeMesh("Cart ore load", load, GetMaterial("coal")).GetComponent<MeshRenderer>();
            cartCargo.transform.SetParent(cart, false);
            for (int i = 0; i < cartWheels.Length; i++)
            {
                var wheel = new Geometry();
                AddCube(wheel, Vector3.zero, new Vector3(.16f, .4f, .24f), 1);
                AddCube(wheel, Vector3.zero, new Vector3(.16f, .24f, .4f), 1);
                AddCube(wheel, new Vector3(i % 2 == 0 ? -.095f : .095f, 0, 0), Vector3.one * .13f, 1);
                cartWheels[i] = MakeMesh("Cart wheel " + i, wheel, GetMaterial("black")).transform;
                cartWheels[i].SetParent(cart, false);
                cartWheels[i].localPosition = new Vector3(i % 2 == 0 ? -.56f : .56f, .28f, i < 2 ? -.4f : .4f);
            }

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { .45f, 3.55f })
            {
                Block(new Vector3(x, .35f, 3.05f), new Vector3(.64f, .7f, .64f), "stone", batch: batch);
                foreach (float y in new[] { .9f, 1.95f }) Block(new Vector3(x, y, 2.94f), new Vector3(.39f, .15f, .46f), "metal-dark", batch: batch);
            }
            Block(new Vector3(2, 2.43f, 2.91f), new Vector3(3.65f, .17f, .18f), "metal-dark", batch: batch);
            foreach (float x in new[] { .7f, 3.3f }) Block(new Vector3(x, 2.44f, 2.8f), Vector3.one * .15f, "metal-gold", batch: batch);
            mineStructures[1] = DevelopmentGroup("Reinforced mine entrance", batch, Vector3.zero, new Vector3(2, 0, 3.05f));

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { -.58f, .58f }) Block(new Vector3(x, 1.25f, 0), new Vector3(.2f, 2.5f, .28f), "beam", batch: batch);
            Block(new Vector3(-.3f, 2.5f, 0), new Vector3(2.1f, .27f, .32f), "planks", batch: batch);
            Block(new Vector3(0, .36f, 0), new Vector3(1.4f, .32f, .92f), "stone", batch: batch);
            mineStructures[2] = DevelopmentGroup("Timber winding hoist", batch, new Vector3(4, 0, 1.1f));
            var spool = new Geometry();
            AddCube(spool, Vector3.zero, new Vector3(.74f, .16f, .19f), 1);
            AddCube(spool, Vector3.zero, new Vector3(.16f, .74f, .19f), 1);
            AddCube(spool, Vector3.zero, Vector3.one * .29f, 1);
            hoistWheel = MakeMesh("Hoist hand wheel", spool, GetMaterial("metal-gold")).transform;
            hoistWheel.SetParent(mineStructures[2], false); hoistWheel.localPosition = new Vector3(.2f, 1, -.3f);
            var bucket = new Geometry();
            AddCube(bucket, Vector3.zero, new Vector3(.43f, .23f, .43f), 1);
            foreach (float x in new[] { -.16f, .16f }) AddCube(bucket, new Vector3(x, .2f, 0), new Vector3(.06f, .3f, .06f), 1);
            AddCube(bucket, new Vector3(0, .35f, 0), new Vector3(.38f, .06f, .06f), 1);
            hoistBucket = MakeMesh("Hoist ore bucket", bucket, GetMaterial("metal-gold")).transform;
            hoistBucket.SetParent(mineStructures[2], false);
            var rope = new Geometry(); AddCube(rope, Vector3.zero, new Vector3(.06f, 1, .06f), 1);
            hoistRope = MakeMesh("Hoist cable", rope, GetMaterial("metal-dark")).transform;
            hoistRope.SetParent(mineStructures[2], false);

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { .45f, 3.55f })
            {
                Block(new Vector3(x, 2.7f, 3.04f), new Vector3(.22f, .38f, .22f), "metal-gold", batch: batch);
                Block(new Vector3(x, 2.98f, 3.04f), Vector3.one * .36f, "diamond", batch: batch);
                Block(new Vector3(x, 3.2f, 3.04f), Vector3.one * .18f, "diamond", batch: batch);
            }
            Block(new Vector3(2, 2.67f, 2.93f), new Vector3(1.28f, .12f, .17f), "metal-gold", batch: batch);
            mineStructures[3] = DevelopmentGroup("Diamond mine lanterns", batch, Vector3.zero, new Vector3(2, 2.4f, 3.04f));
            var crest = new Geometry(); AddCube(crest, Vector3.zero, Vector3.one * .45f, 1);
            crystalSign = MakeMesh("Floating mine crest", crest, GetMaterial("diamond")).transform;
            crystalSign.SetParent(mineStructures[3], false); crystalSign.localPosition = new Vector3(0, .55f, -.14f);

            var forgeOrigin = new Vector3(-2.85f, 0, .7f);
            batch = new Dictionary<string, Geometry>();
            Block(new Vector3(0, .1f, 0), new Vector3(2.2f, .2f, 1.65f), "stone", batch: batch);
            Block(new Vector3(-.45f, .65f, .15f), new Vector3(.95f, 1.1f, 1), "brick", batch: batch);
            Block(new Vector3(-.45f, 1.27f, .15f), new Vector3(1.1f, .18f, 1.1f), "stone", batch: batch);
            Block(new Vector3(-.45f, .67f, -.365f), new Vector3(.61f, .53f, .04f), "black", batch: batch);
            Block(new Vector3(-.45f, .55f, -.4f), new Vector3(.43f, .23f, .04f), "fire", batch: batch);
            Block(new Vector3(.64f, .45f, -.1f), new Vector3(.52f, .65f, .55f), "wood", batch: batch);
            Block(new Vector3(.64f, .87f, -.1f), new Vector3(.26f, .24f, .36f), "metal-dark", batch: batch);
            Block(new Vector3(.64f, 1.05f, -.1f), new Vector3(.85f, .17f, .43f), "metal-dark", batch: batch);
            forgeStructures[0] = DevelopmentGroup("Furnace and anvil", batch, forgeOrigin);

            batch = new Dictionary<string, Geometry>();
            Block(new Vector3(-.45f, 2, .35f), new Vector3(.54f, 1.7f, .58f), "brick", batch: batch);
            Block(new Vector3(-.45f, 2.92f, .35f), new Vector3(.75f, .2f, .8f), "stone", batch: batch);
            Block(new Vector3(-.45f, 3.04f, .35f), new Vector3(.4f, .04f, .46f), "black", batch: batch);
            Block(new Vector3(.61f, .8f, .59f), new Vector3(.9f, .18f, .35f), "planks", batch: batch);
            Block(new Vector3(.61f, 1.1f, .59f), new Vector3(.16f, .6f, .16f), "metal-dark", batch: batch);
            forgeStructures[1] = DevelopmentGroup("Forge chimney and tool rack", batch, forgeOrigin);

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { -1.04f, 1.05f }) Block(new Vector3(x, 1.15f, .65f), new Vector3(.17f, 2.3f, .2f), "beam", batch: batch);
            Block(new Vector3(0, 2.3f, .65f), new Vector3(2.45f, .18f, .23f), "planks", batch: batch);
            for (int step = 0; step < 3; step++) Block(new Vector3(0, 2.38f + step * .13f, .25f + step * .27f), new Vector3(2.5f, .14f, 1.45f - step * .4f), "roof", batch: batch);
            forgeStructures[2] = DevelopmentGroup("Forge timber canopy", batch, forgeOrigin);

            batch = new Dictionary<string, Geometry>();
            Block(new Vector3(0, 2.32f, -.5f), new Vector3(2.5f, .13f, .12f), "metal-gold", batch: batch);
            for (int i = 0; i < 3; i++) Block(new Vector3(.6f + i % 2 * .28f, .3f + i / 2 * .15f, -.55f), new Vector3(.27f, .14f, .37f), "metal-gold", batch: batch);
            Block(new Vector3(.64f, 1.18f, -.1f), new Vector3(.38f, .12f, .2f), "gold", batch: batch);
            forgeStructures[3] = DevelopmentGroup("Master forge fittings", batch, forgeOrigin);

            smokeOrigin = forgeOrigin + new Vector3(-.45f, 3.2f, .35f);
            for (int i = 0; i < forgeSmoke.Length; i++)
            {
                var puff = new Geometry(); AddCube(puff, Vector3.zero, Vector3.one, 1);
                forgeSmoke[i] = MakeMesh("Chimney smoke " + i, puff, GetMaterial("smoke")).transform;
                forgeSmoke[i].gameObject.SetActive(false);
            }
            BuildProduction();
        }

        private void BuildProduction()
        {
            Vector3[] positions = { new Vector3(-2.45f, 0, -1.4f), new Vector3(.6f, 0, 2), new Vector3(2.8f, 0, 1.5f) };
            for (int i = 0; i < workers.Length; i++)
            {
                var batch = new Dictionary<string, Geometry>();
                foreach (float x in new[] { -.14f, .14f }) Block(new Vector3(x, .26f, 0), new Vector3(.2f, .52f, .25f), "metal-dark", batch: batch);
                Block(new Vector3(0, .78f, 0), new Vector3(.55f, .57f, .3f), "water", batch: batch);
                Block(new Vector3(0, 1.23f, 0), new Vector3(.43f, .4f, .4f), "sand", batch: batch);
                Block(new Vector3(0, 1.47f, -.02f), new Vector3(.56f, .16f, .5f), "metal-gold", batch: batch);
                Block(new Vector3(0, 1.43f, -.29f), new Vector3(.16f, .13f, .07f), "diamond", batch: batch);
                foreach (float x in new[] { -.1f, .1f }) Block(new Vector3(x, 1.25f, -.205f), Vector3.one * .06f, "black", batch: batch);
                workers[i] = DevelopmentGroup("Working miner " + i, batch, positions[i]);
                batch = new Dictionary<string, Geometry>();
                Block(new Vector3(0, -.12f, 0), new Vector3(.1f, .5f, .1f), "wood", batch: batch);
                Block(new Vector3(0, .13f, 0), new Vector3(.5f, .1f, .13f), "iron", batch: batch);
                workerTools[i] = DevelopmentGroup("Miner tool " + i, batch, Vector3.zero);
                workerTools[i].SetParent(workers[i], false); workerTools[i].localPosition = new Vector3(.34f, 1, -.2f); workerTools[i].gameObject.SetActive(true);
            }
            var rig = new Dictionary<string, Geometry>();
            Block(new Vector3(0, .16f, 0), new Vector3(1.1f, .32f, .8f), "metal-dark", batch: rig);
            foreach (float x in new[] { -.4f, .4f }) Block(new Vector3(x, .9f, .2f), new Vector3(.13f, 1.5f, .15f), "metal-gold", batch: rig);
            Block(new Vector3(0, 1.65f, .1f), new Vector3(1.1f, .3f, .65f), "iron", batch: rig);
            drillRig = DevelopmentGroup("Automatic mine drill", rig, new Vector3(3.8f, 0, 1.45f));
            var spindle = new Geometry();
            for (int i = 0; i < 4; i++) AddCube(spindle, new Vector3(0, -.22f * i, 0), Vector3.one * (.4f - i * .07f), 1);
            drillSpindle = MakeMesh("Turning drill spindle", spindle, GetMaterial("metal-dark")).transform;
            drillSpindle.SetParent(drillRig, false); drillSpindle.localPosition = new Vector3(0, 1.32f, -.15f);
            for (int i = 0; i < veinSparks.Length; i++)
            {
                var spark = new Geometry(); AddCube(spark, Vector3.zero, Vector3.one * .17f, 1);
                veinSparks[i] = MakeMesh("Rich vein glint " + i, spark, GetMaterial("metal-gold")).transform;
                veinSparks[i].gameObject.SetActive(false);
            }
        }

        public void SetProduction(int workerCount, int drillCount, int carts, bool richVein)
        {
            if (IsExpedition) return;
            for (int i = 0; i < workers.Length; i++) workers[i].gameObject.SetActive(workerCount >= (i == 0 ? 1 : i == 1 ? 5 : 10));
            drillRig.gameObject.SetActive(drillCount > 0);
            if (carts > 0 && !cart.gameObject.activeSelf) { cart.gameObject.SetActive(true); mineBuildTime[0] = 1; cart.localScale = Vector3.one; }
            for (int i = 0; i < veinSparks.Length; i++) veinSparks[i].gameObject.SetActive(richVein);
        }

        public void SetDevelopment(int pickTier, int forgeLevel, bool animate = true)
        {
            if (IsExpedition) return;
            pickTier = Mathf.Clamp(pickTier, 0, 4); forgeLevel = Mathf.Clamp(forgeLevel, 0, 10);
            int forgeStage = 0; foreach (int level in ForgeMilestones) if (forgeLevel >= level) forgeStage++;
            if (DevelopmentTier == pickTier && ForgeStage == forgeStage && animate) return;
            bool first = DevelopmentTier < 0 || !animate;
            for (int i = 0; i < 4; i++)
            {
                bool mine = i < pickTier, forge = i < forgeStage;
                if (mine && !mineStructures[i].gameObject.activeSelf) mineBuildTime[i] = first ? 1 : 0;
                if (forge && !forgeStructures[i].gameObject.activeSelf) forgeBuildTime[i] = first ? 1 : 0;
                if (!first && mine && !mineStructures[i].gameObject.activeSelf) EmitChips(mineStructures[i].localPosition + Vector3.up * .35f, art.Texture(i == 3 ? "tex-diamond" : "tex-planks"), 6, true);
                if (!first && forge && !forgeStructures[i].gameObject.activeSelf) EmitChips(forgeStructures[i].localPosition + Vector3.up * .7f, art.Texture("tex-brick"), 6, true);
                mineStructures[i].gameObject.SetActive(mine); forgeStructures[i].gameObject.SetActive(forge);
                mineStructures[i].localScale = Vector3.one * (first || !mine ? 1 : BuildScale(mineBuildTime[i]));
                forgeStructures[i].localScale = Vector3.one * (first || !forge ? 1 : BuildScale(forgeBuildTime[i]));
            }
            DevelopmentTier = pickTier; ForgeStage = forgeStage;
            cartBody.sharedMaterial = GetMaterial(pickTier >= 2 ? "metal-dark" : "planks");
            cartCargo.sharedMaterial = GetMaterial(pickTier >= 4 ? "diamond" : pickTier == 3 ? "gold" : pickTier == 2 ? "iron" : "coal");
            foreach (var puff in forgeSmoke) puff.gameObject.SetActive(forgeStage >= 2);
        }

        private static float BuildScale(float t)
        {
            t = Mathf.Clamp01(t) - 1;
            return Mathf.Max(.001f, 1 + 2.70158f * t * t * t + 1.70158f * t * t);
        }

        private void UpdateDevelopment(float dt)
        {
            if (DevelopmentTier < 0) return;
            for (int i = 0; i < workers.Length; i++) if (workers[i].gameObject.activeSelf) workerTools[i].localEulerAngles = new Vector3(Mathf.Sin(clock * 3 + i * 2) * 42, 0, -22);
            if (DrillVisible) drillSpindle.localEulerAngles = new Vector3(0, clock * 250, 0);
            if (VeinVisible) for (int i = 0; i < veinSparks.Length; i++)
            {
                float angle = clock * 1.1f + i * Mathf.PI / 3;
                veinSparks[i].localPosition = new Vector3(-.4f + Mathf.Cos(angle) * 1.35f, 1.35f + Mathf.Sin(angle * 2) * .38f, -.8f + Mathf.Sin(angle) * 1.35f);
                veinSparks[i].localEulerAngles = new Vector3(clock * 35, clock * 55, 45);
            }
            for (int i = 0; i < 4; i++)
            {
                if (mineStructures[i].gameObject.activeSelf)
                {
                    mineBuildTime[i] = Mathf.Min(1, mineBuildTime[i] + dt / .5f);
                    mineStructures[i].localScale = Vector3.one * BuildScale(mineBuildTime[i]);
                }
                if (forgeStructures[i].gameObject.activeSelf)
                {
                    forgeBuildTime[i] = Mathf.Min(1, forgeBuildTime[i] + dt / .5f);
                    forgeStructures[i].localScale = Vector3.one * BuildScale(forgeBuildTime[i]);
                }
            }
            if (CartVisible)
            {
                float cycle = (clock * .08f) % 1, travel = (1 - Mathf.Cos(cycle * Mathf.PI * 2)) * .5f;
                cart.localPosition = new Vector3(2, Mathf.Sin(travel * 32) * .015f, .25f + travel * 2.75f);
                foreach (var wheel in cartWheels) wheel.localEulerAngles = new Vector3(travel * 720, 0, 0);
            }
            if (mineStructures[2].gameObject.activeSelf)
            {
                float lift = Mathf.Sin(clock * .9f), bucketHeight = 1.2f + lift * .3f;
                hoistWheel.localEulerAngles = new Vector3(0, 0, lift * 110);
                hoistBucket.localPosition = new Vector3(-1, bucketHeight, 0);
                float ropeBottom = bucketHeight + .38f, ropeLength = 2.4f - ropeBottom;
                hoistRope.localPosition = new Vector3(-1, ropeBottom + ropeLength * .5f, 0);
                hoistRope.localScale = new Vector3(1, ropeLength, 1);
            }
            if (mineStructures[3].gameObject.activeSelf)
            {
                crystalSign.localPosition = new Vector3(0, .55f + Mathf.Sin(clock * 1.8f) * .06f, -.14f);
                crystalSign.localEulerAngles = new Vector3(0, clock * 20, 45);
            }
            if (ForgeStage >= 1) GetMaterial("fire").color = new Color(1.15f + Mathf.Sin(clock * 11) * .15f, 1.03f, .85f);
            if (ForgeStage >= 2) for (int i = 0; i < forgeSmoke.Length; i++)
            {
                float age = (clock * .23f + i / (float)forgeSmoke.Length) % 1;
                forgeSmoke[i].localPosition = smokeOrigin + new Vector3(age * .75f + Mathf.Sin(age * 5 + i) * .08f, age * 1.5f, age * .2f);
                forgeSmoke[i].localScale = Vector3.one * (.12f + age * .23f) * Mathf.Clamp01((1 - age) * 5);
            }
        }
    }
}
