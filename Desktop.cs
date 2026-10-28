using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace XunfeiLite {
static class MicArt {
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
 public static void Draw(Graphics g,Rectangle r,Color color){g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;float u=Math.Min(r.Width,r.Height)/32f;using(var pen=new Pen(color,2.5f*u)){float cx=r.Left+r.Width/2f,top=r.Top+4*u;g.DrawArc(pen,cx-5*u,top,10*u,10*u,180,180);g.DrawLine(pen,cx-5*u,top+5*u,cx-5*u,top+13*u);g.DrawLine(pen,cx+5*u,top+5*u,cx+5*u,top+13*u);g.DrawArc(pen,cx-5*u,top+8*u,10*u,10*u,0,180);g.DrawArc(pen,cx-9*u,top+7*u,18*u,16*u,0,180);g.DrawLine(pen,cx,top+23*u,cx,top+18*u);g.DrawLine(pen,cx-6*u,top+24*u,cx+6*u,top+24*u);}}
 public static Icon Create(Color color){using(var b=new Bitmap(32,32)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);Draw(g,new Rectangle(0,0,32,32),color);}IntPtr h=b.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}}}
}
class Pill : Form {
 AppOptions options;string status="待机",partial="";bool listening,lit,configuring;
 public event Action GeometryChanged;
 public Pill(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.FromArgb(35,39,47);ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",9);DoubleBuffered=true;MinimumSize=new Size(120,40);MaximumSize=new Size(1600,800);}
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
 public void Configure(AppOptions o){options=o;configuring=true;try{Bounds=o.Bounds();Opacity=o.Opacity/100.0;}finally{configuring=false;}Invalidate();}
 public void Display(string text,bool active,string live,bool pulse,bool preview){status=text;listening=active;partial=live??"";lit=pulse;if(options!=null&&((options.ShowWindow&&(options.ShowIdle||active||text!="待机"))||preview)){if(!Visible)Show();}else Hide();Invalidate();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var color=listening?(lit?Color.FromArgb(255,105,115):Color.FromArgb(160,75,85)):Color.FromArgb(130,205,230);int mic=Math.Min(34,Height-10);MicArt.Draw(e.Graphics,new Rectangle(7,(Height-mic)/2,mic,mic),color);var rect=new Rectangle(mic+15,7,Math.Max(1,Width-mic-25),Math.Max(1,Height-14));string text=options!=null&&options.LiveText&&partial.Length>0?partial:status;TextRenderer.DrawText(e.Graphics,text,Font,rect,ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);using(var pen=new Pen(Color.FromArgb(110,120,135))){e.Graphics.DrawLine(pen,Width-12,Height-4,Width-4,Height-12);e.Graphics.DrawLine(pen,Width-7,Height-4,Width-4,Height-7);}}
 protected override void WndProc(ref Message m){if(m.Msg==0x84){long v=m.LParam.ToInt64();var p=PointToClient(new Point((short)(v&65535),(short)((v>>16)&65535)));bool l=p.X<7,r=p.X>=Width-7,t=p.Y<7,b=p.Y>=Height-7;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:2;m.Result=(IntPtr)hit;return;}base.WndProc(ref m);}
 protected override void OnResizeEnd(EventArgs e){base.OnResizeEnd(e);if(!configuring&&GeometryChanged!=null)GeometryChanged();}
}
class Host : Form {
 HotkeyConfig hotkey=HotkeyConfig.Load();AppOptions options=AppOptions.Load();int hotkeyId=1,captureGeneration;bool registered,settingsOpen,busy,recording,finishing,pulse,closing;
 NotifyIcon tray;Pill pill=new Pill();Settings settings;Credentials credentials=Credentials.Load();SegmentedSession session;IntPtr target;string lastText="",liveText="",status="待机";
 Icon idleIcon=MicArt.Create(Color.FromArgb(40,150,200)),activeIcon=MicArt.Create(Color.FromArgb(240,65,85));
 System.Windows.Forms.Timer blink=new System.Windows.Forms.Timer{Interval=450};
 public Host(){ShowInTaskbar=false;WindowState=FormWindowState.Minimized;Opacity=0;var menu=new ContextMenuStrip();menu.Items.Add("设置",null,(s,e)=>{ShowSettings();});menu.Items.Add("查看上次结果",null,(s,e)=>{if(lastText.Length>0)MessageBox.Show(lastText,"上次识别结果");});menu.Items.Add("退出",null,(s,e)=>Close());tray=new NotifyIcon{Icon=idleIcon,Text="轻声 · "+hotkey,ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(s,e)=>{ShowSettings();};pill.Configure(options);pill.GeometryChanged+=()=>{if(settingsOpen)return;options.X=pill.Left;options.Y=pill.Top;options.Width=pill.Width;options.Height=pill.Height;try{options.Save();Balloon("浮窗大小和位置已保存并生效。");}catch(Exception ex){Balloon("浮窗位置保存失败："+ex.Message);}};blink.Tick+=(s,e)=>{pulse=!pulse;Render();};var h=Handle;BeginInvoke(new Action(Initialize));}
 void Initialize(){registered=Native.RegisterHotKey(Handle,hotkeyId,0x4000u|hotkey.Modifiers,(uint)hotkey.Key);Render();if(!registered){MessageBox.Show(hotkey+" 被其他程序占用，请更换快捷键。");ShowSettings();}else if(!credentials.Valid||Environment.GetCommandLineArgs().Contains("--settings"))ShowSettings();}
 void Render(){if(closing)return;bool bright=!options.Blink||pulse;tray.Icon=recording&&bright?activeIcon:idleIcon;pill.Display(status,recording,liveText,bright,false);}
 void ShowSettings(){if(settings!=null&&!settings.IsDisposed){settings.Activate();return;}settingsOpen=true;try{var dialog=new Settings(credentials,hotkey,options,ApplyHotkey,o=>{if(busy)return;pill.Configure(o);pill.Display("预览 · 拖动移动，边缘缩放",false,"示例：语音识别文字将显示在这里。",true,true);},()=>pill.Bounds,o=>{options=o;if(session!=null)session.SetGain(o.MicrophoneGainPercent);pill.Configure(options);Render();});settings=dialog;dialog.RecognitionBusy=()=>busy;dialog.FormClosed+=(s,e)=>{settings=null;settingsOpen=false;pill.Configure(options);Render();};dialog.Show();}catch{settings=null;settingsOpen=false;throw;}}
 string ApplyHotkey(HotkeyConfig next){if(registered&&next.Key==hotkey.Key&&next.Modifiers==hotkey.Modifiers){next.Save();return null;}int nextId=hotkeyId==1?2:1;if(!Native.RegisterHotKey(Handle,nextId,0x4000u|next.Modifiers,(uint)next.Key))return next+" 已被占用，原快捷键保持不变。";try{next.Save();}catch{Native.UnregisterHotKey(Handle,nextId);throw;}if(registered)Native.UnregisterHotKey(Handle,hotkeyId);hotkeyId=nextId;registered=true;hotkey=next;tray.Text="轻声 · "+hotkey;return null;}
 protected override void SetVisibleCore(bool value){base.SetVisibleCore(false);}
 protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==hotkeyId){if(recording)StopRecording();else if(!busy){if(settings!=null)settings.StopMicrophoneTest();StartRecording();}}base.WndProc(ref m);}
 async void StartRecording(){if(!credentials.Valid){ShowSettings();return;}target=Native.GetForegroundWindow();busy=true;finishing=false;liveText="";status="正在连接…";int generation=++captureGeneration;Render();var current=new SegmentedSession(credentials,options,text=>{if(closing)return;try{BeginInvoke(new Action(()=>{if(generation==captureGeneration&&session!=null&&recording){liveText=text;Render();}}));}catch(InvalidOperationException){}});session=current;
  try{await current.Connect();if(closing)return;current.Start();recording=true;pulse=true;status="聆听中 · 55秒自动分段 · "+hotkey+" 结束";blink.Start();Render();ObserveCompletion(current);}catch(Exception ex){Failure(current,ex);}}
 async void ObserveCompletion(SegmentedSession current){try{string text=await current.Completion;if(session==current&&!finishing&&!closing)await Complete(current,text);}catch(Exception ex){if(session==current&&!finishing&&!closing)Failure(current,ex);}}
 async void StopRecording(){if(!recording||finishing)return;await Complete(session,null);}
 async Task Complete(SegmentedSession current,string completed){if(finishing||current==null||session!=current)return;finishing=true;recording=false;blink.Stop();status="正在识别…";Render();try{current.StopCapture();string text=completed??await current.Finish();if(closing)return;lastText=text;if(text.Length==0){Balloon("未识别到文字。");return;}if(!options.AutoInput){Balloon("识别完成，可在托盘查看上次结果。");return;}if(options.OriginalWindow&&Native.GetForegroundWindow()!=target){Balloon("窗口已切换，未输入文字。可在托盘查看上次结果。");return;}Native.TypeUnicode(text,options.OriginalWindow?target:Native.GetForegroundWindow());}catch(Exception ex){lastText=current.CurrentText;if(!closing)Balloon("识别失败："+ex.Message+"；可在托盘查看已收到的文字。");}finally{current.Cancel();current.Dispose();if(session==current)session=null;busy=false;finishing=false;status="待机";liveText="";Render();}}
 void Failure(SegmentedSession current,Exception ex){if(session!=current||closing)return;lastText=current.CurrentText;recording=false;busy=false;finishing=false;blink.Stop();current.Cancel();current.Dispose();session=null;status="待机";liveText="";Render();Balloon("识别失败："+ex.Message);}
 void Balloon(string text){if(!closing)tray.ShowBalloonTip(5000,"轻声",text,ToolTipIcon.Info);}
 protected override void OnFormClosed(FormClosedEventArgs e){closing=true;if(settings!=null)settings.Close();if(session!=null){session.Cancel();session.Dispose();}Native.UnregisterHotKey(Handle,hotkeyId);blink.Dispose();tray.Dispose();pill.Dispose();idleIcon.Dispose();activeIcon.Dispose();base.OnFormClosed(e);}
}
}

