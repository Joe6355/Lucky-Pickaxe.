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
        private Transform cart, hoistWheel, hoistBucket, hoistRope;
        private MeshRenderer cartBody;
        private readonly MeshRenderer[] cartCargo=new MeshRenderer[7];
        private MeshRenderer[] cartRenderers; private MaterialPropertyBlock cartProperties;
        private float cartClock;private bool cartInTunnel;
        public float CartLoadFraction {get;private set;}
        public string CartPhase {get;private set;}="loading";
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
        public bool CartVisible => cart != null && cart.gameObject.activeSelf && !cartInTunnel;
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
            foreach (float y in new[] { .47f, .93f })
            {
                foreach (float x in new[] { -.52f, .52f }) Block(new Vector3(x, y, 0), new Vector3(.08f, .08f, 1.4f), "mine-steel", batch: batch);
                foreach (float z in new[] { -.65f, .65f }) Block(new Vector3(0, y, z), new Vector3(1.1f, .08f, .08f), "mine-steel", batch: batch);
            }
            foreach (float x in new[] { -.53f, .53f }) foreach (float z in new[] { -.5f, .5f })
            {
                Block(new Vector3(x, .7f, z), new Vector3(.065f, .5f, .1f), "mine-steel", batch: batch);
                Block(new Vector3(x * 1.05f, .8f, z), Vector3.one * .07f, "metal-gold", batch: batch);
            }
            cart = mineStructures[0] = DevelopmentGroup("Working ore cart", batch, new Vector3(2, 0, 1.5f));
            foreach (var renderer in cart.GetComponentsInChildren<MeshRenderer>(true)) if (renderer.sharedMaterial == GetMaterial("planks")) cartBody = renderer;
            for(int i=0;i<cartCargo.Length;i++)
            {
                var load=new Geometry();AddCube(load,Vector3.zero,Vector3.one*(i<4?.3f:.34f),1);
                cartCargo[i]=MakeMesh("Cart ore chunk "+i,load,GetMaterial("mine-coal")).GetComponent<MeshRenderer>();
                cartCargo[i].transform.SetParent(cart,false);
                cartCargo[i].transform.localPosition=i<4?new Vector3(i%2==0?-.18f:.18f,.69f,i<2?-.24f:.24f):new Vector3(i==4?-.19f:i==5?.18f:0,.93f,i==4?-.2f:i==5?.21f:.02f);
            }
            for (int i = 0; i < cartWheels.Length; i++)
            {
                var wheel = new Geometry();
                AddWheel(wheel, .24f, .16f);
                AddCube(wheel, new Vector3(i % 2 == 0 ? -.095f : .095f, 0, 0), Vector3.one * .13f, 1);
                cartWheels[i] = MakeMesh("Cart wheel " + i, wheel, GetMaterial("mine-steel")).transform;
                cartWheels[i].SetParent(cart, false);
                cartWheels[i].localPosition = new Vector3(i % 2 == 0 ? -.56f : .56f, .28f, i < 2 ? -.4f : .4f);
            }

            cartRenderers=cart.GetComponentsInChildren<MeshRenderer>(true);cartProperties=new MaterialPropertyBlock();

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { .45f, 3.55f })
            {
                Block(new Vector3(x, .35f, 3.05f), new Vector3(.64f, .7f, .64f), "stone", batch: batch);
                foreach (float y in new[] { .9f, 1.95f }) Block(new Vector3(x, y, 2.94f), new Vector3(.39f, .15f, .46f), "metal-dark", batch: batch);
            }
            Block(new Vector3(2, 2.43f, 2.91f), new Vector3(3.65f, .17f, .18f), "metal-dark", batch: batch);
            foreach (float x in new[] { .7f, 3.3f }) Block(new Vector3(x, 2.44f, 2.8f), Vector3.one * .15f, "metal-gold", batch: batch);
            mineStructures[1] = DevelopmentGroup("Reinforced mine entrance", batch, Vector3.zero, new Vector3(2, 0, 3.05f));

            batch=new Dictionary<string,Geometry>();
            Block(new Vector3(0,.12f,0),new Vector3(1.5f,.24f,1.25f),"mine-wall",batch:batch);
            foreach(float x in new[]{-.59f,.59f}) Block(new Vector3(x,.94f,.1f),new Vector3(.2f,1.4f,.8f),"mine-timber",batch:batch);
            Block(new Vector3(0,1.65f,.1f),new Vector3(1.5f,.2f,.95f),"mine-timber",batch:batch);
            foreach(float x in new[]{-.6f,.6f}) Block(new Vector3(x,.92f,-.32f),new Vector3(.24f,.18f,.08f),"mine-steel",batch:batch);
            mineStructures[2]=DevelopmentGroup("Mining cable winch",batch,new Vector3(3.4f,0,1.7f));
            var winchShape=new Geometry();AddWheel(winchShape,.32f,.12f);
            hoistWheel=MakeMesh("Winch crank wheel",winchShape,GetMaterial("metal-gold")).transform;
            hoistWheel.SetParent(mineStructures[2],false);hoistWheel.localPosition=new Vector3(.73f,1.12f,.1f);
            var drum=new Geometry();AddWheel(drum,.3f,.92f);
            var drumObject=MakeMesh("Winch cable drum",drum,GetMaterial("mine-steel")).transform;drumObject.SetParent(hoistWheel,false);drumObject.localPosition=new Vector3(-.73f,0,0);
            var bucket=new Geometry();AddCube(bucket,new Vector3(0,.04f,0),new Vector3(.46f,.08f,.46f),1);
            foreach(float x in new[]{-.2f,.2f}) AddCube(bucket,new Vector3(x,.21f,0),new Vector3(.06f,.32f,.46f),1);
            foreach(float z in new[]{-.2f,.2f}) AddCube(bucket,new Vector3(0,.21f,z),new Vector3(.34f,.32f,.06f),1);
            foreach(float x in new[]{-.18f,.18f}) AddCube(bucket,new Vector3(x,.46f,0),new Vector3(.035f,.28f,.035f),1);
            AddCube(bucket,new Vector3(0,.6f,0),new Vector3(.38f,.035f,.035f),1);
            hoistBucket=MakeMesh("Winch ore bucket",bucket,GetMaterial("mine-steel")).transform;hoistBucket.SetParent(mineStructures[2],false);
            var bucketOre=new Geometry();AddCube(bucketOre,new Vector3(0,.2f,0),Vector3.one*.29f,1);
            MakeMesh("Winch bucket ore",bucketOre,GetMaterial("mine-copper")).transform.SetParent(hoistBucket,false);
            var rope=new Geometry();AddCube(rope,Vector3.zero,new Vector3(.045f,1,.045f),1);
            hoistRope=MakeMesh("Winch lifting cable",rope,GetMaterial("metal-dark")).transform;hoistRope.SetParent(mineStructures[2],false);

            batch = new Dictionary<string, Geometry>();
            foreach (float x in new[] { .45f, 3.55f })
            {
                Block(new Vector3(x, 2.7f, 3.04f), new Vector3(.22f, .38f, .22f), "metal-gold", batch: batch);
                Block(new Vector3(x, 2.98f, 3.04f), Vector3.one * .36f, "diamond", batch: batch);
                Block(new Vector3(x, 3.2f, 3.04f), Vector3.one * .18f, "diamond", batch: batch);
            }
            Block(new Vector3(2, 2.67f, 2.93f), new Vector3(1.28f, .12f, .17f), "metal-gold", batch: batch);
            Block(new Vector3(2,2.74f,2.9f),new Vector3(.78f,.58f,.14f),"mine-timber",batch:batch);
            foreach(float x in new[]{1.66f,2.34f}) Block(new Vector3(x,2.74f,2.81f),new Vector3(.055f,.58f,.05f),"metal-gold",batch:batch);
            foreach(float y in new[]{2.48f,3f}) Block(new Vector3(2,y,2.81f),new Vector3(.78f,.055f,.05f),"metal-gold",batch:batch);
            mineStructures[3] = DevelopmentGroup("Diamond mine lanterns", batch, Vector3.zero, new Vector3(2, 2.4f, 3.04f));
            var crest=new Geometry();
            crest.vertices.AddRange(new[]{new Vector3(0,.21f,0),new Vector3(.23f,0,0),new Vector3(0,-.21f,0),new Vector3(-.23f,0,0)});
            for(int i=0;i<4;i++) crest.normals.Add(Vector3.back);
            crest.uv.AddRange(new[]{new Vector2(.5f,1),new Vector2(1,.5f),new Vector2(.5f,0),new Vector2(0,.5f)});
            crest.indices.AddRange(new[]{0,1,2,0,2,3});
            var emblem=MakeMesh("Mounted mineral emblem",crest,GetMaterial("emblem-cyan")).transform;
            emblem.SetParent(mineStructures[3],false); emblem.localPosition=new Vector3(0,.34f,-.275f);

            var forgeOrigin=new Vector3(-2.85f,0,.7f);batch=new Dictionary<string,Geometry>();
            Block(new Vector3(0,.12f,0),new Vector3(2.25f,.24f,1.8f),"mine-wall",batch:batch);
            Block(new Vector3(-.42f,.8f,.12f),new Vector3(1.2f,1.32f,1.2f),"slate","stone",batch:batch);
            Block(new Vector3(-.42f,1.5f,.12f),new Vector3(1.3f,.14f,1.3f),"mine-steel",batch:batch);
            Block(new Vector3(-.42f,.64f,-.492f),new Vector3(.76f,.58f,.025f),"black",batch:batch);
            Block(new Vector3(-.42f,.53f,-.513f),new Vector3(.6f,.34f,.025f),"fire",batch:batch);
            foreach(float x in new[]{-.65f,-.42f,-.19f}) Block(new Vector3(x,.63f,-.536f),new Vector3(.04f,.38f,.025f),"mine-steel",batch:batch);
            Block(new Vector3(-.42f,1.18f,-.495f),new Vector3(.72f,.23f,.03f),"black",batch:batch);
            foreach(float x in new[]{-.63f,-.42f,-.21f}) Block(new Vector3(x,1.18f,-.52f),new Vector3(.035f,.2f,.025f),"mine-steel",batch:batch);
            Block(new Vector3(.66f,.46f,-.05f),new Vector3(.62f,.58f,.68f),"mine-timber",batch:batch);
            Block(new Vector3(.66f,.8f,-.05f),new Vector3(.64f,.12f,.52f),"mine-steel",batch:batch);
            Block(new Vector3(.66f,.98f,-.05f),new Vector3(.25f,.28f,.3f),"metal-dark",batch:batch);
            Block(new Vector3(.66f,1.2f,-.05f),new Vector3(.9f,.2f,.45f),"mine-steel",batch:batch);
            Block(new Vector3(1.16f,1.18f,-.05f),new Vector3(.2f,.12f,.28f),"mine-steel",batch:batch);
            forgeStructures[0]=DevelopmentGroup("Furnace and anvil",batch,forgeOrigin);
            batch=new Dictionary<string,Geometry>();
            for(int brick=0;brick<4;brick++) Block(new Vector3(-.42f,1.78f+brick*.4f,.24f),new Vector3(.55f,.39f,.55f),"slate",batch:batch);
            Block(new Vector3(-.42f,3.2f,.24f),new Vector3(.72f,.12f,.72f),"mine-steel",batch:batch);
            Block(new Vector3(-.42f,3.27f,.24f),new Vector3(.4f,.025f,.4f),"black",batch:batch);
            forgeStructures[1]=DevelopmentGroup("Forge chimney",batch,forgeOrigin);
            batch=new Dictionary<string,Geometry>();
            Block(new Vector3(-1.23f,.48f,.22f),new Vector3(.36f,.7f,.95f),"mine-timber",batch:batch);
            for(int log=0;log<3;log++) Block(new Vector3(-1.24f,.35f+log*.2f,-.28f),new Vector3(.4f,.16f,.45f),"beam",batch:batch);
            Block(new Vector3(.7f,1.8f,.68f),new Vector3(1,.15f,.18f),"mine-timber",batch:batch);
            foreach(float x in new[]{.35f,.7f,1.05f}) Block(new Vector3(x,1.56f,.64f),new Vector3(.055f,.4f,.08f),"mine-steel",batch:batch);
            forgeStructures[2]=DevelopmentGroup("Forge supplies and tools",batch,forgeOrigin);
            batch=new Dictionary<string,Geometry>();
            Block(new Vector3(.75f,.45f,.78f),new Vector3(.64f,.64f,.55f),"mine-steel",batch:batch);
            Block(new Vector3(.75f,.79f,.78f),new Vector3(.52f,.035f,.43f),"water",batch:batch);
            Block(new Vector3(1.22f,1.7f,.61f),new Vector3(.15f,.25f,.15f),"mine-lamp",batch:batch);
            forgeStructures[3]=DevelopmentGroup("Master forge fittings",batch,forgeOrigin);

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
            BuildMiningWorkers();
            var rig = new Dictionary<string, Geometry>();
            Block(new Vector3(0, .16f, 0), new Vector3(1.1f, .32f, .8f), "metal-dark", batch: rig);
            foreach (float x in new[] { -.4f, .4f }) Block(new Vector3(x, .9f, .2f), new Vector3(.13f, 1.5f, .15f), "metal-gold", batch: rig);
            Block(new Vector3(0, 1.65f, .1f), new Vector3(1.1f, .3f, .65f), "mine-steel", batch: rig);
            Block(new Vector3(0,1.65f,-.236f),new Vector3(.58f,.16f,.025f),"metal-dark",batch:rig);
            foreach(float x in new[]{-.43f,.43f}) Block(new Vector3(x,1.65f,-.24f),Vector3.one*.07f,"metal-gold",batch:rig);
            Block(new Vector3(.12f,1.68f,-.255f),new Vector3(.09f,.06f,.025f),"mine-lamp",batch:rig);
            drillRig = DevelopmentGroup("Automatic mine drill", rig, new Vector3(4.5f, 0, .15f));
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

        public void SetProduction(int workerCount, int drillCount, int carts, bool richVein,int finishedMines=0)
        {
            if (IsExpedition) return;
            for (int i = 0; i < workers.Length; i++)
            {
                bool visible = workerCount >= (i == 0 ? 1 : i == 1 ? 5 : 10);
                workers[i].gameObject.SetActive(visible); workerRocks[i].gameObject.SetActive(visible);
            }
            SetWorkerOreTier(finishedMines);
            drillRig.gameObject.SetActive(drillCount > 0);
            if (carts > 0 && !cart.gameObject.activeSelf) { cart.gameObject.SetActive(true); mineBuildTime[0] = 1; cart.localScale = Vector3.one; }
            for (int i = 0; i < veinSparks.Length; i++) veinSparks[i].gameObject.SetActive(richVein);
        }

        public void SetDevelopment(int pickTier, int forgeLevel, bool animate = true)
        {
            if (IsExpedition) return;
            pickTier = Mathf.Clamp(pickTier, 0, 5); forgeLevel = Mathf.Clamp(forgeLevel, 0, 10);
            int forgeStage = 0; foreach (int level in ForgeMilestones) if (forgeLevel >= level) forgeStage++;
            if (DevelopmentTier == pickTier && ForgeStage == forgeStage && animate) return;
            bool first = DevelopmentTier < 0 || !animate;
            if(!animate) { cartClock=0;cartInTunnel=false;CartPhase="loading";CartLoadFraction=0;UpdateWorkingCart(0); }
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
            foreach(var cargo in cartCargo) cargo.sharedMaterial=GetMaterial(pickTier==5?"mine-ancient":pickTier>=4?"mine-diamond":pickTier==3?"mine-gold":pickTier==2?"mine-iron":"mine-coal");
            foreach (var puff in forgeSmoke) puff.gameObject.SetActive(forgeStage >= 2);
        }

        private static void AddWheel(Geometry g, float radius, float width)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                var p = new Vector3(0, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                var q = new Vector3(0, Mathf.Cos(b) * radius, Mathf.Sin(b) * radius);
                foreach (float side in new[] { -1f, 1f })
                {
                    int n = g.vertices.Count; Vector3 x = Vector3.right * (side * width / 2);
                    g.vertices.AddRange(new[] { x, x + p, x + q });
                    g.normals.AddRange(new[] { Vector3.right * side, Vector3.right * side, Vector3.right * side });
                    g.uv.AddRange(new[] { Vector2.one * .5f, new Vector2(.5f + p.y, .5f + p.z), new Vector2(.5f + q.y, .5f + q.z) });
                    g.indices.AddRange(side > 0 ? new[] { n, n + 1, n + 2 } : new[] { n, n + 2, n + 1 });
                }
                int v = g.vertices.Count; Vector3 offset = Vector3.right * width / 2;
                g.vertices.AddRange(new[] { p - offset, q - offset, q + offset, p + offset });
                for (int k = 0; k < 4; k++) { g.normals.Add((p + q).normalized); g.uv.Add(new Vector2(k % 2, k / 2)); }
                g.indices.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
            }
        }

        private static float BuildScale(float t)
        {
            t = Mathf.Clamp01(t) - 1;
            return Mathf.Max(.001f, 1 + 2.70158f * t * t * t + 1.70158f * t * t);
        }

        private void UpdateWorkingCart(float dt)
        {
            cartClock=(cartClock+dt)%18;float t=cartClock,z;
            if(t<6) { CartPhase="loading";CartLoadFraction=t/6;z=.35f; }
            else if(t<9.5f) { CartPhase="outbound";CartLoadFraction=1;z=Mathf.Lerp(.35f,5.7f,Mathf.SmoothStep(0,1,(t-6)/3.5f)); }
            else if(t<12.5f) { CartPhase="tunnel";CartLoadFraction=t<10.5f?1:0;z=5.7f; }
            else if(t<16) { CartPhase="return";CartLoadFraction=0;z=Mathf.Lerp(5.7f,.35f,Mathf.SmoothStep(0,1,(t-12.5f)/3.5f)); }
            else { CartPhase="waiting";CartLoadFraction=0;z=.35f; }
            float movement=z-cart.localPosition.z;cartInTunnel=CartPhase=="tunnel";
            cart.localPosition=new Vector3(2,Mathf.Abs(movement)>.0001f?Mathf.Sin(z*26)*.012f:0,z);
            foreach(var wheel in cartWheels) wheel.Rotate(Vector3.right,movement*240,Space.Self);
            float light=Mathf.Lerp(1,.025f,Mathf.InverseLerp(3.6f,5.55f,z));cartProperties.SetColor("_Color",new Color(light,light,light,1));
            foreach(var renderer in cartRenderers) { renderer.enabled=!cartInTunnel;renderer.SetPropertyBlock(cartProperties); }
            for(int i=0;i<cartCargo.Length;i++)
            {
                float fill=Mathf.Clamp01(CartLoadFraction*cartCargo.Length-i);cartCargo[i].enabled=!cartInTunnel&&fill>.01f;cartCargo[i].transform.localScale=Vector3.one*fill;
            }
        }

        private void UpdateDevelopment(float dt)
        {
            if (DevelopmentTier < 0) return;
            UpdateMiningWorkers();
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
            if(cart.gameObject.activeSelf) UpdateWorkingCart(dt);
            if(mineStructures[2].gameObject.activeSelf)
            {
                float lift=Mathf.Sin(clock*.9f),height=.44f+lift*.12f;
                hoistWheel.localEulerAngles=new Vector3(lift*110,0,0);
                hoistBucket.localPosition=new Vector3(-.2f,height,-.32f);
                float bottom=height+.63f,length=1.55f-bottom;
                hoistRope.localPosition=new Vector3(-.2f,bottom+length*.5f,-.32f);hoistRope.localScale=new Vector3(1,length,1);
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
