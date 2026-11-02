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
                var arms=new Dictionary<string,Geometry>();
                foreach(float x in new[]{-.23f,.23f})
                {
                    Block(new Vector3(x,-.12f,.16f),new Vector3(.17f,.4f,.2f),"mine-cloth",batch:arms);
                    Block(new Vector3(x*.65f,-.29f,.34f),new Vector3(.17f,.16f,.18f),"mine-skin",batch:arms);
                }
                Block(new Vector3(0,-.02f,.42f),new Vector3(.065f,.74f,.075f),"mine-timber",batch:arms);
                Block(new Vector3(0,.32f,.42f),new Vector3(.62f,.1f,.15f),"mine-steel",batch:arms);
                foreach(float x in new[]{-.26f,.26f}) Block(new Vector3(x,.265f,.42f),new Vector3(.1f,.16f,.15f),"mine-steel",batch:arms);
                workerTools[i]=DevelopmentGroup("Two-handed mining swing "+i,arms,Vector3.zero);
                workerTools[i].SetParent(workers[i],false); workerTools[i].localPosition=new Vector3(0,1.0f,.02f); workerTools[i].gameObject.SetActive(true);
                var baseShape=new Geometry(); AddCube(baseShape,new Vector3(0,.17f,0),new Vector3(.88f,.34f,.88f),1);
                var pedestal=MakeMesh("Worker outcrop base "+i,baseShape,GetMaterial("mine-wall")); pedestal.transform.SetParent(workers[i],false);
                pedestal.transform.localPosition=workers[i].localRotation==Quaternion.identity?Vector3.zero:Quaternion.Inverse(workers[i].localRotation)*(targets[i]-homes[i]-Vector3.up*.78f);
                var rockShape=new Geometry(); AddCube(rockShape,Vector3.zero,Vector3.one*.78f,1);
                var oreObject=MakeMesh("Worker ore "+i,rockShape,GetMaterial("mine-iron"));
                workerRocks[i]=oreObject.transform; workerRocks[i].localPosition=targets[i]; workerOreRenderers[i]=oreObject.GetComponent<MeshRenderer>();
                var cracks=new Geometry(); AddCube(cracks,Vector3.zero,Vector3.one*.785f,1);
                var crackMat=new Material(Resources.Load<Shader>("VoxelShadow")){mainTexture=art.Texture("crack1")}; materials.Add("Worker cracks "+i,crackMat);
                var overlay=MakeMesh("Worker rock cracks "+i,cracks,crackMat); overlay.transform.SetParent(workerRocks[i],false);
                workerCracks[i]=overlay.GetComponent<MeshRenderer>(); workerCracks[i].enabled=false;
                workerRocks[i].gameObject.SetActive(false);
            }
        }
        private void UpdateMiningWorkers()
        {
            for(int i=0;i<workers.Length;i++)
            {
                if(!workers[i].gameObject.activeSelf) continue;
                float time=clock*1.2f+i*.31f, phase=time-Mathf.Floor(time); int cycle=Mathf.FloorToInt(time);
                float swing=phase<.3f?Mathf.Lerp(5,-58,phase/.3f):phase<.56f?Mathf.Lerp(-58,45,Mathf.Pow((phase-.3f)/.26f,2)):Mathf.Lerp(45,5,(phase-.56f)/.44f);
                workerTools[i].localEulerAngles=new Vector3(swing,0,0);
                float lean=phase>.3f&&phase<.7f?Mathf.Sin((phase-.3f)/.4f*Mathf.PI):0;
                workers[i].localPosition=workerHomes[i]+Vector3.up*(lean*.018f);
                workerHeads[i].localEulerAngles=new Vector3(lean*9,0,0);
                if(phase>=.56f&&workerLastCycle[i]!=cycle)
                {
                    workerLastCycle[i]=cycle; workerImpacts[i]++;
                    bool broken=workerImpacts[i]%6==0;
                    string key=new[]{"iron","copper","coal"}[(workerImpacts[i]/6+i)%3];
                    EmitChips(workerRocks[i].localPosition,art.Texture("tex-mine-"+key),broken?14:3,broken);
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
