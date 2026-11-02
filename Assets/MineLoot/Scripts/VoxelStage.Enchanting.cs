using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    public sealed partial class VoxelStage
    {
        private Transform enchantingBook, bookLeft, bookRight;
        private readonly Transform[] bookRunes = new Transform[12];
        private float enchantBurst;
        private bool enchantedTool;
        public float BookHover => enchantingBook == null ? 0 : enchantingBook.localPosition.y;

        private void BuildEnchantingRoom()
        {
            for (int x = -3; x <= 3; x++) for (int z = -2; z <= 3; z++)
                Block(new Vector3(x, -.15f, z), new Vector3(.99f,.3f,.99f), "planks");
            for (int x = -2; x <= 2; x++) for (int y = 0; y < 3; y++)
                Block(new Vector3(x, .52f + y, 2.55f), new Vector3(.98f,.98f,.42f), "bookshelf", "planks");
            foreach (float x in new[] { -2.5f, 2.5f }) for (int z = 0; z < 3; z++) for (int y = 0; y < 3; y++)
                Block(new Vector3(x, .52f + y, z), new Vector3(.42f,.98f,.98f), "bookshelf", "planks");
            Block(new Vector3(0,.08f,0), new Vector3(1.65f,.16f,1.65f), "slate");
            Block(new Vector3(0,.56f,0), new Vector3(1.25f,.88f,1.25f), "obsidian");
            Block(new Vector3(0,1.04f,0), new Vector3(1.42f,.18f,1.42f), "enchant-cloth");
            foreach (float x in new[] { -.64f,.64f }) foreach (float z in new[] { -.64f,.64f })
            {
                Block(new Vector3(x,.85f,z), new Vector3(.2f,.3f,.2f), "diamond");
                Block(new Vector3(x,.55f,z), new Vector3(.12f,.12f,.12f), "metal-gold");
            }
            foreach (var pair in terrain) MakeMesh("Enchanting " + pair.Key, pair.Value, GetMaterial(pair.Key));
            enchantingBook = new GameObject("Floating open book").transform; enchantingBook.SetParent(transform,false);
            bookLeft = BuildBookPage(-1); bookRight = BuildBookPage(1);
            for (int i = 0; i < bookRunes.Length; i++)
            {
                var shape = new Geometry();
                AddCube(shape,Vector3.zero,new Vector3(.035f,.16f,.025f),1);
                AddCube(shape,new Vector3(.05f,.025f,0),new Vector3(.1f,.025f,.025f),1);
                AddCube(shape,new Vector3(.05f,-.055f,0),new Vector3(.1f,.025f,.025f),1);
                bookRunes[i] = MakeMesh("Floating rune " + i,shape,GetMaterial("diamond")).transform;
            }
        }
        private Transform BuildBookPage(int side)
        {
            var batch = new Dictionary<string,Geometry>();
            Block(new Vector3(side*.23f,-.025f,0),new Vector3(.45f,.045f,.66f),"enchant-cloth",batch:batch);
            for (int sheet=0;sheet<3;sheet++)
                Block(new Vector3(side*.23f,.005f+sheet*.014f,0),new Vector3(.42f,.015f,.62f),"book-page",batch:batch);
            var page=DevelopmentGroup(side<0?"Left book pages":"Right book pages",batch,Vector3.zero);
            page.SetParent(enchantingBook,false); page.gameObject.SetActive(true); return page;
        }
        public void SetEnchantment(bool enabled) => enchantedTool = enabled;
        public void PlayEnchantment() => enchantBurst = 1;
        private void UpdateEnchantingRoom()
        {
            enchantBurst=Mathf.MoveTowards(enchantBurst,0,Time.unscaledDeltaTime*.7f);
            enchantingBook.localPosition=new Vector3(0,1.5f+Mathf.Sin(clock*1.6f)*.065f+enchantBurst*.12f,0);
            enchantingBook.localEulerAngles=new Vector3(-38,Mathf.Sin(clock*.65f)*6,0);
            bookLeft.localEulerAngles=new Vector3(0,0,-15-Mathf.Sin(clock*3)*4-enchantBurst*12);
            bookRight.localEulerAngles=new Vector3(0,0,15+Mathf.Sin(clock*3+1)*4+enchantBurst*12);
            for (int i=0;i<bookRunes.Length;i++)
            {
                float angle=clock*.36f+i*Mathf.PI/6, radius=1.1f+enchantBurst*.7f;
                bookRunes[i].localPosition=new Vector3(Mathf.Cos(angle)*radius,1.42f+Mathf.Sin(angle*2)*.23f+enchantBurst*.35f,Mathf.Sin(angle)*radius);
                bookRunes[i].localEulerAngles=new Vector3(0,-angle*Mathf.Rad2Deg,0);
                bookRunes[i].localScale=Vector3.one*(enchantedTool?.72f:.5f)*(1+enchantBurst*.7f);
            }
        }
    }
}
