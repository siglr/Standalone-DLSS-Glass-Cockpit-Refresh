using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// Exercise the translated/clipped paint surface used when Windows repaints disabled
// children through their parent. Full-window DrawToBitmap alone misses this path.
internal static class WsgPaintTests
{
    [STAThread] static int Main() {
        Application.EnableVisualStyles();
        try {
            foreach(bool light in new[]{false,true}) {
                WsgTheme.Light=light;
                foreach(float scale in new[]{1f,1.25f,1.5f,2f})
                foreach(Control control in new Control[]{new WsgLabel(),new WsgCheckBox(),new WsgButton()})
                using(control) {
                    control.Text="Disabled settings";control.Enabled=false;
                    control.Font=new Font("Segoe UI",10*scale);
                    control.Size=new Size((int)(240*scale),(int)(36*scale));
                    if(control is Button)WsgTheme.StyleButton((Button)control,false);
                    var paint=control.GetType().GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic);
                    using(var direct=new Bitmap(control.Width,control.Height))
                    using(var translated=new Bitmap(control.Width+180,control.Height+140)) {
                        using(var g=Graphics.FromImage(direct)) {
                            g.Clear(Color.Magenta);
                            paint.Invoke(control,new object[]{new PaintEventArgs(g,control.ClientRectangle)});
                        }
                        var clip=new Rectangle(8,2,control.Width-16,control.Height-4);
                        using(var g=Graphics.FromImage(translated)) {
                            g.Clear(Color.Magenta);g.TranslateTransform(90,70);g.SetClip(clip);
                            paint.Invoke(control,new object[]{new PaintEventArgs(g,clip)});
                        }
                        for(int y=0;y<translated.Height;y++)for(int x=0;x<translated.Width;x++) {
                            Color expected=clip.Contains(x-90,y-70)?direct.GetPixel(x-90,y-70):Color.Magenta;
                            if(expected.ToArgb()!=translated.GetPixel(x,y).ToArgb())
                                throw new Exception(control.GetType().Name+" paints outside its translated/clipped bounds or misplaces text (scale "+scale+")");
                        }
                    }
                }
            }
            // Reused paint buffers and partial WM_PAINT invalidations can expose
            // old sibling pixels even though a fresh DrawToBitmap looks correct.
            int repaintCases=0;
            foreach(bool light in new[]{false,true})foreach(bool enabled in new[]{false,true})
            foreach(float scale in new[]{1f,1.25f,1.5f,2f})foreach(bool card in new[]{false,true})
            foreach(CheckState checkState in new[]{CheckState.Unchecked,CheckState.Checked,CheckState.Indeterminate})
            foreach(bool partial in new[]{false,true})using(var box=new WsgCheckBox()) {
                WsgTheme.Light=light;box.FlatStyle=FlatStyle.Flat;box.UseVisualStyleBackColor=false;
                box.Text="Enable glass-cockpit refresh fix";box.Enabled=enabled;box.CheckState=checkState;
                box.Font=new Font("Segoe UI",10*scale);box.BackColor=card?WsgTheme.Panel:WsgTheme.Background;
                box.Size=box.GetPreferredSize(Size.Empty);
                var paint=typeof(WsgCheckBox).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic);
                using(var clean=new Bitmap(box.Width,box.Height))using(var dirty=new Bitmap(box.Width,box.Height)) {
                    using(var g=Graphics.FromImage(clean)) {
                        g.Clear(box.BackColor);paint.Invoke(box,new object[]{new PaintEventArgs(g,box.ClientRectangle)});
                    }
                    using(var g=Graphics.FromImage(dirty)) {
                        g.Clear(Color.Magenta);
                        TextRenderer.DrawText(g,"Settings Previous Control",box.Font,box.ClientRectangle,Color.Yellow);
                    }
                    using(var before=(Bitmap)dirty.Clone()) {
                        Rectangle clip=partial?new Rectangle(box.Width/4,2,box.Width/2,box.Height-4):box.ClientRectangle;
                        using(var g=Graphics.FromImage(dirty)) {
                            g.SetClip(clip);paint.Invoke(box,new object[]{new PaintEventArgs(g,clip)});
                        }
                        for(int y=0;y<box.Height;y++)for(int x=0;x<box.Width;x++) {
                            Color expected=clip.Contains(x,y)?clean.GetPixel(x,y):before.GetPixel(x,y);
                            if(expected.ToArgb()!=dirty.GetPixel(x,y).ToArgb())
                                throw new Exception("Checkbox retains stale pixels or paints beyond invalidation (enabled="+enabled+", scale="+scale+", state="+checkState+", partial="+partial+")");
                        }
                    }
                }
                repaintCases++;
            }
            foreach(float scale in new[]{1f,1.25f,1.5f,2f})using(var box=new WsgCheckBox()) {
                box.Text="Enable glass-cockpit refresh fix";box.Font=new Font("Segoe UI",10*scale);
                box.Size=box.GetPreferredSize(Size.Empty);
                if(box.GlyphSize<box.Font.Height||box.Height<box.GlyphSize+8)throw new Exception("Checkbox glyph/hit target does not scale with text");
                bool changed=false;box.CheckedChanged+=delegate { changed=true; };
                typeof(CheckBox).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(box,new object[]{EventArgs.Empty});
                if(!changed||!box.Checked)throw new Exception("Custom painting broke native checkbox activation");
            }
            Console.WriteLine("PASS "+repaintCases+" reused-buffer/full-and-partial checkbox repaints; translated/clipped control painting, scaling and native activation");return 0;
        } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
