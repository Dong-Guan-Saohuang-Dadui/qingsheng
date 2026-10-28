using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace XunfeiLite {
class AppOptions {
 public int MicrophoneId=-1;public string MicrophoneName="";public int MicrophoneGainPercent=100;
 public int Eos=10000;public bool Punctuation=true,Numbers=true;public string Language="zh_cn",Accent="mandarin",Domain="iat",Script="zh-cn";
 public bool ShowWindow=true,ShowIdle=true,LiveText=true,Blink=true,AutoInput=true,OriginalWindow=true;
 public int Width=260,Height=64,X=int.MinValue,Y=int.MinValue,Opacity=92;
 public static string FileName {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XunfeiLite","options.json");}}
 public static AppOptions Load(){try{var o=new JavaScriptSerializer().Deserialize<AppOptions>(File.ReadAllText(FileName));o.Validate();return o;}catch{return new AppOptions();}}
 public AppOptions Copy(){return new JavaScriptSerializer().Deserialize<AppOptions>(new JavaScriptSerializer().Serialize(this));}
 public void Validate(){if(MicrophoneGainPercent<0||MicrophoneGainPercent>400)throw new Exception("麦克风增益应在 0～400% 之间。");if(MicrophoneId < -1)throw new Exception("麦克风设备无效。");if(Eos<500||Eos>10000)throw new Exception("停顿时间应在 500～10000 毫秒之间。");if(Width<120||Width>1600||Height<40||Height>800)throw new Exception("浮窗宽度应为 120～1600，高度应为 40～800。");if(Opacity<30||Opacity>100)throw new Exception("不透明度应在 30～100 之间。");if(Language!="zh_cn"&&Language!="en_us")throw new Exception("语种无效。");if(string.IsNullOrWhiteSpace(Accent))throw new Exception("方言参数不能为空。");if(Script!="zh-cn"&&Script!="zh-hk")throw new Exception("字体参数无效。");if(Domain!="iat"&&Domain!="xfime-mianqie")throw new Exception("识别领域无效。");}
 public void Save(){Validate();Directory.CreateDirectory(Path.GetDirectoryName(FileName));File.WriteAllText(FileName,new JavaScriptSerializer().Serialize(this));}
 public Dictionary<string,object> Business(){var b=new Dictionary<string,object>{{"language",Language},{"domain",Domain},{"accent",Accent},{"eos",Eos}};if(Language=="zh_cn"){b["ptt"]=Punctuation?1:0;b["nunum"]=Numbers?1:0;b["rlang"]=Script;if(Domain=="iat"&&Accent=="mandarin")b["dwa"]="wpgs";}return b;}
 public Rectangle Bounds(){Rectangle area=Screen.PrimaryScreen.WorkingArea;var proposed=new Rectangle(X==int.MinValue?area.Right-Width-24:X,Y==int.MinValue?area.Bottom-Height-24:Y,Width,Height);bool visible=false;foreach(var screen in Screen.AllScreens)if(Rectangle.Intersect(screen.WorkingArea,proposed).Width>=40&&Rectangle.Intersect(screen.WorkingArea,proposed).Height>=30)visible=true;if(!visible)proposed.Location=new Point(area.Right-Width-24,area.Bottom-Height-24);return proposed;}
}
static class Startup {
 const string RunPath="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 public static bool Enabled {get{using(var key=Registry.CurrentUser.OpenSubKey(RunPath))return key!=null&&Convert.ToString(key.GetValue("XunfeiLite"))==Command;}}
 static string Command {get{return "\""+Application.ExecutablePath+"\"";}}
 public static void Set(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(RunPath)){if(enabled)key.SetValue("XunfeiLite",Command);else if(key.GetValue("XunfeiLite")!=null)key.DeleteValue("XunfeiLite");}}
}
class Settings : Form {
 Action stopMicrophoneTest;
 public Func<bool> RecognitionBusy;
 public void StopMicrophoneTest(){if(stopMicrophoneTest!=null)stopMicrophoneTest();}
 TableLayoutPanel table;int row;
 static ComboBox Choice(params string[] values){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=290};c.Items.AddRange(values);c.SelectedIndex=0;return c;}
 static NumericUpDown Number(int value,int min,int max){return new NumericUpDown{Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value)),Width=130};}
 static CheckBox Check(string text,bool value){return new CheckBox{Text=text,Checked=value,AutoSize=true};}
 void Page(TabControl tabs,string name){var p=new TabPage(name){AutoScroll=true,Padding=new Padding(14)};tabs.TabPages.Add(p);table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=0};table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.Controls.Add(table);row=0;}
 void Add(string label,Control control){table.RowCount=++row;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));var l=new Label{Text=label,AutoSize=true,MaximumSize=new Size(120,0),Margin=new Padding(0,9,5,8)};control.Margin=new Padding(0,5,0,8);table.Controls.Add(l,0,row-1);table.Controls.Add(control,1,row-1);}
 void Note(string text){var l=new Label{Text=text,AutoSize=true,MaximumSize=new Size(490,0),ForeColor=Color.DimGray,Margin=new Padding(0,10,0,12)};table.RowCount=++row;table.Controls.Add(l,0,row-1);table.SetColumnSpan(l,2);}
 public Settings(Credentials c,HotkeyConfig hotkey,AppOptions current,Func<HotkeyConfig,string> apply,Action<AppOptions> preview,Func<Rectangle> geometry,Action<AppOptions> commit){
  Text="轻声 · 讯飞设置";Font=new Font("Microsoft YaHei UI",9);ClientSize=new Size(570,540);MinimumSize=new Size(580,570);StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;
  var tabs=new TabControl{Dock=DockStyle.Fill};var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10)};var close=new Button{Text="关闭",Width=88,Height=30,DialogResult=DialogResult.OK};var feedback=new Label{Text="自动保存 · 修改后立即生效",Width=420,Height=32,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.DimGray};bottom.Controls.Add(close);bottom.Controls.Add(feedback);Controls.Add(tabs);Controls.Add(bottom);CancelButton=close;
  close.Click+=(s,e)=>Close();
  Recorder micTest=null;int micLevel=0;var meterTimer=new System.Windows.Forms.Timer{Interval=80};
  Page(tabs,"麦克风");var micChoice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=360};Add("输入设备",micChoice);
  var refreshMics=new Button{Text="刷新设备列表",AutoSize=true};Add("设备列表",refreshMics);
  var gain=Number(current.MicrophoneGainPercent,0,400);gain.Increment=10;Add("麦克风增益 %",gain);
  Note("100% 为原始音量，200% 为 2 倍，0% 静音。仅调整轻声收到的声音，不改变系统音量；放大也会增加底噪，过高可能失真。");
  var testMic=new Button{Text="测试麦克风",AutoSize=true};Add("本地测试",testMic);
  var meter=new ProgressBar{Minimum=0,Maximum=100,Width=340,Height=20};Add("输入音量",meter);
  var meterText=new Label{Text="点击测试后说话，确认音量条有变化",AutoSize=true,MaximumSize=new Size(360,0)};Add("状态",meterText);
  Note("测试仅在本地显示音量，不上传、不保存音频。更换设备后自动保存，下次录音使用所选麦克风；设备断开会提示，不会悄悄改用其他设备。");
  Action stopMicTest=()=>{meterTimer.Stop();if(micTest!=null){micTest.Dispose();micTest=null;}micLevel=0;meter.Value=0;testMic.Text="测试麦克风";meterText.Text="测试已停止";};
  stopMicrophoneTest=stopMicTest;
  bool loadingMics=false;
  Action loadMics=()=>{stopMicTest();loadingMics=true;try{micChoice.Items.Clear();foreach(var d in Recorder.Devices())micChoice.Items.Add(d);int found=-1;for(int i=0;i<micChoice.Items.Count;i++){var d=(MicDevice)micChoice.Items[i];if(current.MicrophoneId<0&&d.Id<0||current.MicrophoneId>=0&&d.Id==current.MicrophoneId&&d.Name==current.MicrophoneName){found=i;break;}}if(found<0&&current.MicrophoneId>=0){for(int i=0;i<micChoice.Items.Count;i++){var d=(MicDevice)micChoice.Items[i];if(d.Id>=0&&d.Name==current.MicrophoneName){found=i;break;}}}if(found<0){micChoice.Items.Add(new MicDevice(current.MicrophoneId,current.MicrophoneName+"（已断开）"));found=micChoice.Items.Count-1;}micChoice.SelectedIndex=found;}finally{loadingMics=false;}};
  loadMics();refreshMics.Click+=(s,e)=>loadMics();
  meterTimer.Tick+=(s,e)=>{meter.Value=Math.Max(0,Math.Min(100,micLevel));meterText.Text=micLevel>3?"收到声音 · 输入音量 "+micLevel+"%":"未收到明显声音，请检查设备、静音开关和麦克风权限";};
  testMic.Click+=(s,e)=>{if(micTest!=null){stopMicTest();return;}if(RecognitionBusy!=null&&RecognitionBusy()){meterText.Text="语音输入正在使用麦克风，结束输入后可测试";return;}try{var d=(MicDevice)micChoice.SelectedItem;var o=current.Copy();o.MicrophoneId=d.Id;o.MicrophoneName=d.Id<0?"":d.Name;int device=Recorder.ResolveDevice(o,Recorder.Devices());micTest=new Recorder(bytes=>{Recorder.ApplyGain(bytes,current.MicrophoneGainPercent);micLevel=Recorder.Level(bytes);},device);micTest.Start();meterTimer.Start();testMic.Text="停止测试";}catch(Exception ex){stopMicTest();meterText.Text=ex.Message;}};
  tabs.SelectedIndexChanged+=(s,e)=>stopMicTest();FormClosed+=(s,e)=>{stopMicTest();meterTimer.Dispose();};
  Page(tabs,"语音识别");var mode=Choice("中文普通话","英语","普通话 / 方言免切（需授权）","指定方言（需授权）");mode.SelectedIndex=current.Language=="en_us"?1:current.Domain=="xfime-mianqie"?2:current.Accent!="mandarin"?3:0;Add("识别模式",mode);
  var accent=new TextBox{Text=current.Accent,Width=290};Add("方言 accent 参数",accent);Note("指定方言：填写讯飞控制台已开通方言的 accent 值。免切和指定方言需要讯飞授权；没有授权会提示错误。");
  var eos=Number(current.Eos,500,10000);eos.Increment=500;Add("停顿结束（毫秒）",eos);Note("500～10000 毫秒。停顿达到此时长时由讯飞结束本次识别；也可按快捷键结束。连续录音按 55 秒自动分段拼接，每段分别计入接口调用次数。");
  var punctuation=Check("自动添加标点",current.Punctuation);Add("标点",punctuation);var numbers=Check("数字规范化为阿拉伯数字",current.Numbers);Add("数字格式",numbers);Note("关闭数字规范化后由识别引擎决定原始表达，并不保证全部转为汉字数字。");var script=Choice("简体中文","香港繁体");script.SelectedIndex=current.Script=="zh-hk"?1:0;Add("输出字体",script);
  Action updateMode=()=>{accent.Enabled=mode.SelectedIndex==3;punctuation.Enabled=numbers.Enabled=script.Enabled=mode.SelectedIndex!=1;};mode.SelectedIndexChanged+=(s,e)=>updateMode();updateMode();
  Page(tabs,"浮窗与输入");var show=Check("显示话筒浮窗",current.ShowWindow);Add("浮窗",show);var idle=Check("空闲时也显示",current.ShowIdle);Add("空闲显示",idle);var live=Check("边说边在浮窗显示识别文字",current.LiveText);Add("实时文字",live);var blink=Check("录音时话筒闪动（托盘 + 浮窗）",current.Blink);Add("录音提示",blink);
  var width=Number(current.Width,120,1600);var height=Number(current.Height,40,800);Add("宽度（像素）",width);Add("高度（像素）",height);var rect=current.Bounds();var x=Number(rect.X,-32000,32000);var y=Number(rect.Y,-32000,32000);Add("X（屏幕坐标）",x);Add("Y（屏幕坐标）",y);var opacity=Number(current.Opacity,30,100);Add("不透明度 %",opacity);
  var position=new FlowLayoutPanel{AutoSize=true};var right=new Button{Text="放到右下角",AutoSize=true};var read=new Button{Text="读取浮窗位置",AutoSize=true};var demo=new Button{Text="预览 / 拖动调整",AutoSize=true};position.Controls.AddRange(new Control[]{right,read,demo});Add("位置调整",position);
  Note("浮窗可拖动；拖动边缘或右下角可缩放。拖动和缩放后会立即保存。屏幕变化后会恢复到可见区域。");
  var input=Check("识别结束后直接输入到光标处",current.AutoInput);Add("自动输入",input);var original=Check("只输入到开始录音时的窗口",current.OriginalWindow);Add("输入目标",original);Note("取消窗口限制后，会输入到结束识别时的当前光标。实时文字显示在浮窗中，识别完成后再输入最终文字；不使用剪贴板。");
  Func<AppOptions> collect=()=>{var o=current.Copy();o.MicrophoneGainPercent=(int)gain.Value;var selectedMic=micChoice.SelectedItem as MicDevice;if(selectedMic!=null&&!selectedMic.Name.EndsWith("（已断开）")){o.MicrophoneId=selectedMic.Id;o.MicrophoneName=selectedMic.Id<0?"":selectedMic.Name;}o.Language=mode.SelectedIndex==1?"en_us":"zh_cn";o.Domain=mode.SelectedIndex==2?"xfime-mianqie":"iat";o.Accent=mode.SelectedIndex==3?accent.Text.Trim():"mandarin";o.Eos=(int)eos.Value;o.Punctuation=punctuation.Checked;o.Numbers=numbers.Checked;o.Script=script.SelectedIndex==1?"zh-hk":"zh-cn";o.ShowWindow=show.Checked;o.ShowIdle=idle.Checked;o.LiveText=live.Checked;o.Blink=blink.Checked;o.Width=(int)width.Value;o.Height=(int)height.Value;o.X=(int)x.Value;o.Y=(int)y.Value;o.Opacity=(int)opacity.Value;o.AutoInput=input.Checked;o.OriginalWindow=original.Checked;return o;};

  Page(tabs,"快捷键与启动");var key=Choice(EnumerableKeys());key.SelectedIndex=hotkey.Key-(int)Keys.F1;Add("录音快捷键",key);var mods=new FlowLayoutPanel{AutoSize=true};var ctrl=Check("Ctrl",(hotkey.Modifiers&2)!=0);var alt=Check("Alt",(hotkey.Modifiers&1)!=0);var shift=Check("Shift",(hotkey.Modifiers&4)!=0);mods.Controls.AddRange(new Control[]{ctrl,alt,shift});Add("组合键",mods);Note("按一次开始，再按一次结束。运行时会独占所选按键；快捷键冲突时保留原设置。");var startup=Check("登录 Windows 后自动启动轻声",Startup.Enabled);Add("开机自启动",startup);Note("自启动使用当前用户启动项；开关修改后立即生效。");
  Page(tabs,"讯飞凭据");var id=new TextBox{Text=c.AppId,Width=320};var api=new TextBox{Text=c.ApiKey,Width=320,UseSystemPasswordChar=true};var secret=new TextBox{Text=c.ApiSecret,Width=320,UseSystemPasswordChar=true};Add("APPID",id);Add("APIKey",api);Add("APISecret",secret);Note("凭据在本机使用 Windows DPAPI 加密保存。音频只发送给讯飞语音听写 API，不保存录音。");
  bool syncing=false;Rectangle lastGeometry=geometry();var numericTimer=new System.Windows.Forms.Timer{Interval=350};var credentialTimer=new System.Windows.Forms.Timer{Interval=600};var geometryTimer=new System.Windows.Forms.Timer{Interval=250};
  Action<string,bool> notify=(message,ok)=>{feedback.Text=message;feedback.ForeColor=ok?Color.FromArgb(30,125,75):Color.Firebrick;};
  Func<string> optionJson=()=>new JavaScriptSerializer().Serialize(current);string savedOptions=optionJson();
  NumericUpDown[] numeric={gain,eos,width,height,x,y,opacity};
  Action saveOptions=()=>{if(syncing)return;try{foreach(var n in numeric){decimal parsed;if(!decimal.TryParse(n.Text,out parsed)||parsed<n.Minimum||parsed>n.Maximum)throw new Exception("数值未保存：请输入范围内的完整数字。");if(parsed!=decimal.Truncate(parsed))throw new Exception("数值未保存：请输入整数。");n.Value=parsed;}var o=collect();o.Validate();string json=new JavaScriptSerializer().Serialize(o);if(json==savedOptions)return;o.Save();current=o;commit(o);lastGeometry=geometry();savedOptions=json;notify("✓ 已保存并生效",true);}catch(Exception ex){notify(ex.Message,false);}};
  micChoice.SelectedIndexChanged+=(s,e)=>{if(loadingMics)return;stopMicTest();saveOptions();};
  Action saveCredentials=()=>{credentialTimer.Stop();if(syncing)return;try{if(string.IsNullOrWhiteSpace(id.Text)||string.IsNullOrWhiteSpace(api.Text)||string.IsNullOrWhiteSpace(secret.Text))throw new Exception("凭据未保存：请填写完整三项。");if(c.AppId==id.Text.Trim()&&c.ApiKey==api.Text.Trim()&&c.ApiSecret==secret.Text.Trim())return;var nc=new Credentials{AppId=id.Text.Trim(),ApiKey=api.Text.Trim(),ApiSecret=secret.Text.Trim()};nc.Save();c.AppId=nc.AppId;c.ApiKey=nc.ApiKey;c.ApiSecret=nc.ApiSecret;notify("✓ 凭据已加密保存并生效",true);}catch(Exception ex){notify(ex.Message,false);}};
  numericTimer.Tick+=(s,e)=>{numericTimer.Stop();saveOptions();};credentialTimer.Tick+=(s,e)=>saveCredentials();
  Action scheduleNumbers=()=>{if(syncing)return;numericTimer.Stop();numericTimer.Start();feedback.Text="正在应用…";feedback.ForeColor=Color.DimGray;};
  foreach(var n in numeric){n.ValueChanged+=(s,e)=>scheduleNumbers();n.TextChanged+=(s,e)=>scheduleNumbers();}
  foreach(var check in new[]{punctuation,numbers,show,idle,live,blink,input,original})check.CheckedChanged+=(s,e)=>saveOptions();
  foreach(var combo in new[]{mode,script})combo.SelectedIndexChanged+=(s,e)=>saveOptions();
  accent.TextChanged+=(s,e)=>scheduleNumbers();
  foreach(var box in new[]{id,api,secret})box.TextChanged+=(s,e)=>{if(syncing)return;credentialTimer.Stop();credentialTimer.Start();feedback.Text="正在保存凭据…";feedback.ForeColor=Color.DimGray;};
  HotkeyConfig activeHotkey=hotkey;Action saveHotkey=()=>{if(syncing)return;try{var next=new HotkeyConfig{Key=(int)Keys.F1+key.SelectedIndex,Modifiers=(uint)((ctrl.Checked?2:0)|(alt.Checked?1:0)|(shift.Checked?4:0))};string error=apply(next);if(error!=null){syncing=true;try{key.SelectedIndex=activeHotkey.Key-(int)Keys.F1;ctrl.Checked=(activeHotkey.Modifiers&2)!=0;alt.Checked=(activeHotkey.Modifiers&1)!=0;shift.Checked=(activeHotkey.Modifiers&4)!=0;}finally{syncing=false;}throw new Exception(error);}activeHotkey=next;notify("✓ 快捷键已保存并生效："+next,true);}catch(Exception ex){notify(ex.Message,false);}};
  key.SelectedIndexChanged+=(s,e)=>saveHotkey();foreach(var mod in new[]{ctrl,alt,shift})mod.CheckedChanged+=(s,e)=>saveHotkey();
  startup.CheckedChanged+=(s,e)=>{if(syncing)return;try{Startup.Set(startup.Checked);notify(startup.Checked?"✓ 已启用开机自启动":"✓ 已关闭开机自启动",true);}catch(Exception ex){syncing=true;try{startup.Checked=Startup.Enabled;}finally{syncing=false;}notify("自启动未保存："+ex.Message,false);}};
  Action updateGeometry=()=>{var r=geometry();syncing=true;try{x.Value=Math.Max(x.Minimum,Math.Min(x.Maximum,r.X));y.Value=Math.Max(y.Minimum,Math.Min(y.Maximum,r.Y));width.Value=r.Width;height.Value=r.Height;}finally{syncing=false;}lastGeometry=r;};
  right.Click+=(s,e)=>{syncing=true;try{var a=Screen.PrimaryScreen.WorkingArea;x.Value=a.Right-width.Value-24;y.Value=a.Bottom-height.Value-24;}finally{syncing=false;}saveOptions();};
  read.Click+=(s,e)=>{updateGeometry();saveOptions();};
  demo.Text="显示调整辅助窗";demo.Click+=(s,e)=>{try{saveOptions();preview(current);lastGeometry=geometry();notify("拖动或拉伸浮窗，松开后自动保存",true);}catch(Exception ex){notify(ex.Message,false);}};
  geometryTimer.Tick+=(s,e)=>{if(syncing||Control.MouseButtons!=MouseButtons.None)return;if(geometry()!=lastGeometry){updateGeometry();saveOptions();}};geometryTimer.Start();
  FormClosing+=(s,e)=>{if(numericTimer.Enabled){numericTimer.Stop();saveOptions();}if(credentialTimer.Enabled)saveCredentials();};
  FormClosed+=(s,e)=>{numericTimer.Dispose();credentialTimer.Dispose();geometryTimer.Dispose();};
 }
 static string[] EnumerableKeys(){var list=new List<string>();for(int i=1;i<=12;i++)list.Add("F"+i);return list.ToArray();}
}
}

