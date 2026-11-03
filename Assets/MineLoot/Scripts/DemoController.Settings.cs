using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        private bool settingsWindowOpen;
        private readonly Slider[] volumeSliders=new Slider[3];
        private readonly Text[] volumeValues=new Text[3];
        private Button settingsMute;

        public void ShowSettings()
        {
            if(applicationPaused || modal!=null) return;
            holding=false;settingsWindowOpen=true;shaft.Paused=true;
            modal=CreateModal("Settings modal");Card(modal.transform,"Settings card",24,104,432,752,Panel);
            Label(modal.transform,"НАСТРОЙКИ ЗВУКА",44,120,392,42,27,Ink,FontStyle.Bold);
            settingsMute=ButtonAt(modal.transform,"",44,176,392,44,()=>{if(applicationPaused)return;sounds.Toggle();RefreshSettings();},Hex("bad5ca"),Ink);
            settingsMute.name="Settings mute";
            string[] titles={"Общая громкость","Удары кирки","Рулетка"};
            string[] details={"Все игровые звуки","Удары и разрушение блоков в шахте","Вращение, кирки и награды рулетки"};
            for(int i=0;i<3;i++)
            {
                var row=Card(modal.transform,"Volume row "+i,44,236+i*128,392,116,Hex("d6dfd2"));
                Label(row.transform,titles[i],16,8,280,28,22,Ink,FontStyle.Bold);
                volumeValues[i]=Label(row.transform,"",304,8,72,28,21,Ink,FontStyle.Bold,TextAnchor.MiddleRight);
                Label(row.transform,details[i],16,38,360,26,16,Muted);
                var rect=MakeRect(row.transform,"Volume slider "+i,16,78,360,26);
                var slider=rect.gameObject.AddComponent<Slider>();volumeSliders[i]=slider;
                PanelAt(rect,"Touch area",Color.clear,0,-9,360,44).raycastTarget=true;
                PanelAt(rect,"Track",Hex("82948a"),0,9,360,8);
                var fillArea=MakeRect(rect,"Fill area",0,9,360,8);
                var fill=PanelAt(fillArea,"Fill",Hex("91b96c"),0,0,360,8);
                fill.rectTransform.sizeDelta=Vector2.zero;
                var handleArea=MakeRect(rect,"Handle area",10,0,340,26);
                var handle=Card(handleArea,"Handle",0,0,20,26,Hex("e0c78e"));handle.raycastTarget=true;
                handle.rectTransform.sizeDelta=new Vector2(20,0);handle.rectTransform.pivot=new Vector2(.5f,.5f);
                slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
                slider.minValue=0;slider.maxValue=1;slider.direction=Slider.Direction.LeftToRight;
                slider.onValueChanged.AddListener(_=>ChangeVolume());
            }
            ButtonAt(modal.transform,"ПРОВЕРИТЬ УДАР",44,634,190,40,()=>sounds.Play("stone-hit",GameAudio.Channel.Mining),Hex("d8c598"),Ink).name="Preview mining audio";
            ButtonAt(modal.transform,"ПРОВЕРИТЬ РУЛЕТКУ",246,634,190,40,()=>sounds.Play("reel-stop",GameAudio.Channel.Roulette),Hex("d8c598"),Ink).name="Preview roulette audio";
            ButtonAt(modal.transform,"Сбросить громкость",44,698,392,38,()=>{if(applicationPaused)return;sounds.ResetVolumes();RefreshSettings();},Panel,Ink).name="Reset audio settings";
            ButtonAt(modal.transform,"Вернуться в игру",44,786,392,48,CloseModal,Hex("bad5ca"),Ink).name="Close settings";
            foreach(var button in modal.GetComponentsInChildren<Button>()) if(button.name.StartsWith("Preview ")) button.GetComponentInChildren<Text>().fontSize=17;
            RefreshSettings();
        }
        private void ChangeVolume()
        {
            if(applicationPaused || !settingsWindowOpen) return;
            sounds.SetVolumes(volumeSliders[0].value,volumeSliders[1].value,volumeSliders[2].value);RefreshSettings();
        }
        private void RefreshSettings()
        {
            soundLabel.text=sounds.Muted||sounds.MasterVolume==0?"♪×":"♪";
            if(!settingsWindowOpen) return;
            settingsMute.GetComponentInChildren<Text>().text=sounds.Muted?"ЗВУК ВЫКЛЮЧЕН · ВКЛЮЧИТЬ":"ЗВУК ВКЛЮЧЁН · ВЫКЛЮЧИТЬ";
            for(int i=0;i<3;i++)
            {
                float value=i==0?sounds.MasterVolume:i==1?sounds.MiningVolume:sounds.RouletteVolume;
                volumeSliders[i].SetValueWithoutNotify(value);volumeValues[i].text=Mathf.RoundToInt(value*100)+"%";
            }
        }
    }
}
