using System;
using System.Windows.Forms;

internal static class ShortcutRecorderTests {
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    [STAThread]
    static int Main() {
        try {
            // Official 1.0.2 virtual-key names, including physical OEM keys on non-US layouts.
            int[] codes={0x09,0x08,0x0D,0x26,0x28,0x25,0x27,0x6A,0x6B,0x6D,0x6E,0x6F,
                0xBA,0xBB,0xBC,0xBD,0xBE,0xBF,0xC0,0xDB,0xDC,0xDD,0xDE,0xE2};
            string[] names={"Tab","Backspace","Enter","Up","Down","Left","Right","Num*","NumPlus","Num-","Num.","Num/",
                ";","=",",","-",".","/","`","[","\\","]","'","<>"};
            for(int i=0;i<codes.Length;i++) {
                foreach(Keys modifiers in new[]{Keys.Control,Keys.Shift,Keys.Alt,Keys.Control|Keys.Shift|Keys.Alt}) {
                    string prefix=((modifiers&Keys.Control)!=0?"Ctrl+":"")+((modifiers&Keys.Shift)!=0?"Shift+":"")+((modifiers&Keys.Alt)!=0?"Alt+":"");
                    bool refused=modifiers==Keys.Alt&&(names[i]=="Tab"||names[i]=="Enter");
                    using(var recorder=new ShortcutRecorder("Ctrl+Shift+F6")) {
                        Check(recorder.CaptureKey((Keys)codes[i]|modifiers),"Modified key escaped recording: "+prefix+names[i]);
                        Check(refused?recorder.Shortcut==null:recorder.Shortcut==prefix+names[i],"Incorrect recording: "+prefix+names[i]);
                        Check(recorder.DialogResult==DialogResult.None,"Recording submitted the dialog");
                    }
                    if(!refused)Check(SettingsWindowShortcut.Normalize(prefix+names[i])==prefix+names[i],"Incorrect INI spelling");
                }
                bool rejected=false;
                try { SettingsWindowShortcut.Normalize(names[i]); } catch(ArgumentException) { rejected=true; }
                Check(rejected,"Unmodified typing/navigation key accepted");
            }
            foreach(string shortcut in new[]{"Alt+Tab","Alt+Space","Alt+Enter","Alt+F4","Ctrl+Shift+F7","Ctrl+Shift+F12","Win+Up","Ctrl+Escape","Ctrl+Num+","Ctrl++"}) {
                bool rejected=false;
                try { SettingsWindowShortcut.Normalize(shortcut); } catch(ArgumentException) { rejected=true; }
                Check(rejected,"Reserved or invalid shortcut accepted: "+shortcut);
            }
            using(var recorder=new ShortcutRecorder("Pause")) {
                recorder.CaptureKey(Keys.Control|Keys.Enter);
                Check(recorder.Shortcut=="Ctrl+Enter"&&recorder.DialogResult==DialogResult.None,"Ctrl+Enter submitted instead of recording");
                Check(!recorder.CaptureKey(Keys.Tab)&&recorder.Shortcut=="Ctrl+Enter","Plain Tab must navigate without erasing capture");
                recorder.CaptureKey(Keys.Shift|Keys.Tab);
                Check(recorder.Shortcut=="Shift+Tab","Shift+Tab must record");
                recorder.CaptureKey(Keys.Enter);
                Check(recorder.DialogResult==DialogResult.OK,"Plain Enter must save the recorded shortcut");
            }
            using(var recorder=new ShortcutRecorder("Pause")) {
                recorder.CaptureKey(Keys.Control|Keys.Oemplus);
                recorder.CaptureKey(Keys.Escape);
                Check(recorder.DialogResult==DialogResult.Cancel,"Escape must cancel");
            }
            foreach(string sample in new[]{"Pause","ScrollLock","F13","F24","Ctrl+Shift+F6","Ctrl+Shift+Tab","Ctrl+Alt+Enter","Ctrl+Alt+Space"})
                Check(SettingsWindowShortcut.Normalize(sample)==sample,"Existing valid shortcut rejected");
            Console.WriteLine("PASS "+checks+" shortcut capture, key-name, modifier, reservation and dialog-navigation checks");
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
