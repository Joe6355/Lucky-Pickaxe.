using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    public sealed partial class VoxelStage
    {
        private readonly Transform[] workerRocks = new Transform[3], workerHeads = new Transform[3];
        private readonly MeshRenderer[] workerCracks = new MeshRenderer[3], workerOreRenderers = new MeshRenderer[3];
        private readonly int[] workerLastCycle = { -1, -1, -1 }, workerImpacts = new int[3];
        private readonly Vector3[] workerHomes = new Vector3[3];
        private readonly Transform[,] workerLimbs = new Transform[3,6];
        private static readonly Vector3 WorkerGrip = new Vector3(.42f,.9f,.3f), WorkerTip = new Vector3(0,.46f,.5f);
        public int WorkerStrikeCount => workerImpacts[0] + workerImpacts[1] + workerImpacts[2];

        private void BuildMiningWorkers()
        {
            Vector3[] homes = { new Vector3(4.55f,0,2.3f), new Vector3(-1.6f,0,3.05f), new Vector3(-3.25f,0,2.65f) };
            Vector3[] targets = { new Vector3(5.15f,.78f,3.1f), new Vector3(-1.7f,.78f,4.05f), new Vector3(-4.2f,.78f,2.85f) };
            for (int i=0;i<workers.Length;i++)
            {
                var body=new Dictionary<string,Geometry>();
                foreach (float x in new[] { -.15f,.15f })
                {
                    Block(new Vector3(x,.1f,.035f),new Vector3(.23f,.2f,.36f),"mine-timber",batch:body);
                    Block(new Vector3(x,.4f,0),new Vector3(.2f,.42f,.26f),"mine-uniform",batch:body);
                }
                Block(new Vector3(0,.85f,0),new Vector3(.56f,.58f,.32f),"mine-cloth",batch:body);
                Block(new Vector3(0,.62f,0),new Vector3(.57f,.08f,.34f),"mine-timber",batch:body);
                foreach (float x in new[] { -.18f,.18f }) Block(new Vector3(x,.92f,.18f),new Vector3(.075f,.42f,.035f),"mine-uniform",batch:body);
                Block(new Vector3(0,1.16f,0),new Vector3(.18f,.15f,.18f),"mine-skin",batch:body);
                workers[i]=DevelopmentGroup("Background miner "+i,body,homes[i]); workerHomes[i]=homes[i];
                Vector3 forward=targets[i]-homes[i]; forward.y=0;
                workers[i].localRotation=Quaternion.LookRotation(forward);
                var head=new Dictionary<string,Geometry>();
                Block(Vector3.zero,new Vector3(.48f,.45f,.44f),"mine-skin",batch:head);
                foreach(float x in new[]{-.115f,.115f}) Block(new Vector3(x,.045f,.229f),new Vector3(.065f,.05f,.015f),"black",batch:head);
                Block(new Vector3(0,-.08f,.23f),new Vector3(.15f,.035f,.018f),"mine-timber",batch:head);
                Block(new Vector3(0,.25f,0),new Vector3(.54f,.12f,.5f),"mine-timber",batch:head);
                Block(new Vector3(0,.32f,0),new Vector3(.44f,.08f,.42f),"mine-timber",batch:head);
                Block(new Vector3(0,.22f,.275f),new Vector3(.18f,.16f,.08f),"mine-steel",batch:head);
                Block(new Vector3(0,.22f,.324f),new Vector3(.105f,.1f,.018f),"mine-lamp",batch:head);
                workerHeads[i]=DevelopmentGroup("Miner head "+i,head,Vector3.zero);
                workerHeads[i].SetParent(workers[i],false); workerHeads[i].localPosition=new Vector3(0,1.38f,0); workerHeads[i].gameObject.SetActive(true);
                var tool=new Dictionary<string,Geometry>();
                Block(new Vector3(0,.21f,0),new Vector3(.065f,.78f,.075f),"mine-timber",batch:tool);
                Block(new Vector3(0,.61f,-.03f),new Vector3(.14f,.12f,.54f),"mine-steel",batch:tool);
                Block(new Vector3(0,.58f,.27f),new Vector3(.12f,.1f,.17f),"mine-steel",batch:tool);
                Block(new Vector3(0,.53f,.36f),new Vector3(.1f,.1f,.12f),"mine-steel",batch:tool);
                var point=new Geometry();
                Vector3[] rim={new Vector3(-.05f,.61f,.35f),new Vector3(.05f,.61f,.35f),new Vector3(.05f,.51f,.35f),new Vector3(-.05f,.51f,.35f)};
                for(int side=0;side<4;side++)
                {
                    int n=point.vertices.Count; Vector3 a=rim[side],b=rim[(side+1)%4];
                    Vector3 normal=Vector3.Cross(WorkerTip-a,b-a).normalized;
                    point.vertices.AddRange(new[]{a,WorkerTip,b}); point.normals.AddRange(new[]{normal,normal,normal});
                    point.uv.AddRange(new[]{Vector2.zero,Vector2.one,new Vector2(1,0)}); point.indices.AddRange(new[]{n,n+1,n+2});
                }
                // Point and crossbar lie in the YZ swing plane, outside the head and torso.
                workerTools[i]=DevelopmentGroup("Two-handed mining swing "+i,tool,Vector3.zero);
                workerTools[i].SetParent(workers[i],false); workerTools[i].localPosition=WorkerGrip; workerTools[i].gameObject.SetActive(true);
                MakeMesh("Pickaxe tapered point "+i,point,GetMaterial("mine-steel")).transform.SetParent(workerTools[i],false);
                var tip=new GameObject("Pick striking point"); tip.layer=Layer; tip.transform.SetParent(workerTools[i],false); tip.transform.localPosition=WorkerTip;
                for(int limb=0;limb<6;limb++)
                {
                    var shape=new Geometry(); AddCube(shape,Vector3.zero,Vector3.one,1);
                    workerLimbs[i,limb]=MakeMesh("Miner limb "+i+" "+limb,shape,GetMaterial(limb<2?"mine-cloth":"mine-skin")).transform;
                    workerLimbs[i,limb].SetParent(workers[i],false);
                }
                var baseShape=new Geometry(); AddCube(baseShape,new Vector3(.42f,.17f,1.43f),new Vector3(.96f,.34f,.96f),1);
                MakeMesh("Worker outcrop base "+i,baseShape,GetMaterial("mine-wall")).transform.SetParent(workers[i],false);
                var rockShape=new Geometry(); AddCube(rockShape,Vector3.zero,Vector3.one*.9f,1);
                var oreObject=MakeMesh("Worker ore "+i,rockShape,GetMaterial("mine-iron"));
                workerRocks[i]=oreObject.transform; workerRocks[i].localPosition=transform.InverseTransformPoint(workers[i].TransformPoint(new Vector3(.42f,.78f,1.43f)));
                workerRocks[i].localRotation=workers[i].localRotation; workerOreRenderers[i]=oreObject.GetComponent<MeshRenderer>();
                var cracks=new Geometry(); AddCube(cracks,Vector3.zero,Vector3.one*.906f,1);
                var crackMat=new Material(Resources.Load<Shader>("VoxelShadow")){mainTexture=art.Texture("crack1")}; materials.Add("Worker cracks "+i,crackMat);
                var overlay=MakeMesh("Worker rock cracks "+i,cracks,crackMat); overlay.transform.SetParent(workerRocks[i],false);
                workerCracks[i]=overlay.GetComponent<MeshRenderer>(); workerCracks[i].enabled=false;
                workerRocks[i].gameObject.SetActive(false);
            }
        }
        private void PoseWorkerArms(int i)
        {
            Vector3 left=workers[i].InverseTransformPoint(workerTools[i].TransformPoint(new Vector3(0,.15f,0)));
            Vector3 right=workers[i].InverseTransformPoint(workerTools[i].TransformPoint(new Vector3(0,-.1f,0)));
            Vector3 leftElbow=new Vector3(-.4f,.8f,.48f),rightElbow=new Vector3(.58f,.78f,.4f);
            PlaceWorkerLimb(workerLimbs[i,0],new Vector3(-.34f,1.03f,.02f),leftElbow,.17f);
            PlaceWorkerLimb(workerLimbs[i,1],new Vector3(.34f,1.03f,.02f),rightElbow,.17f);
            PlaceWorkerLimb(workerLimbs[i,2],leftElbow,left,.14f);
            PlaceWorkerLimb(workerLimbs[i,3],rightElbow,right,.14f);
            for(int hand=0;hand<2;hand++)
            {
                workerLimbs[i,4+hand].localPosition=hand==0?left:right;
                workerLimbs[i,4+hand].localRotation=workerTools[i].localRotation;
                workerLimbs[i,4+hand].localScale=Vector3.one*.14f;
            }
        }
        private static void PlaceWorkerLimb(Transform limb,Vector3 start,Vector3 end,float width)
        {
            limb.localPosition=(start+end)*.5f;
            limb.localRotation=Quaternion.FromToRotation(Vector3.up,end-start);
            limb.localScale=new Vector3(width,(end-start).magnitude,width);
        }
        private void UpdateMiningWorkers()
        {
            for(int i=0;i<workers.Length;i++)
            {
                if(!workers[i].gameObject.activeSelf) continue;
                float time=clock*1.2f+i*.31f, phase=time-Mathf.Floor(time); int cycle=Mathf.FloorToInt(time);
                float swing=phase<.3f?Mathf.Lerp(5,-55,Mathf.SmoothStep(0,1,phase/.3f)):phase<.56f?Mathf.Lerp(-55,40,Mathf.Pow((phase-.3f)/.26f,2)):phase<.62f?40:Mathf.Lerp(40,5,Mathf.SmoothStep(0,1,(phase-.62f)/.38f));
                workerTools[i].localEulerAngles=new Vector3(swing,0,0);
                PoseWorkerArms(i);
                float lean=phase>.3f&&phase<.7f?Mathf.Sin((phase-.3f)/.4f*Mathf.PI):0;
                workers[i].localPosition=workerHomes[i]+Vector3.up*(lean*.018f);
                workerHeads[i].localEulerAngles=new Vector3(lean*9,0,0);
                if(phase>=.56f&&workerLastCycle[i]!=cycle)
                {
                    workerLastCycle[i]=cycle; workerImpacts[i]++;
                    bool broken=workerImpacts[i]%6==0;
                    string key=new[]{"iron","copper","coal"}[(workerImpacts[i]/6+i)%3];
                    Vector3 contact=transform.InverseTransformPoint(workers[i].TransformPoint(WorkerGrip+Quaternion.Euler(40,0,0)*WorkerTip));
                    EmitChips(contact,art.Texture("tex-mine-"+key),broken?14:3,broken);
                    workerCracks[i].enabled=!broken;
                    workerCracks[i].sharedMaterial.mainTexture=art.Texture("crack"+(1+workerImpacts[i]%6));
                    if(broken) workerOreRenderers[i].sharedMaterial=GetMaterial("mine-"+key);
                }
                float scale=workerImpacts[i]>0&&workerImpacts[i]%6==0&&phase>.56f&&phase<.83f?Mathf.Max(.02f,(phase-.56f)/.27f):1;
                workerRocks[i].localScale=Vector3.one*scale;
            }
        }
    }
}
