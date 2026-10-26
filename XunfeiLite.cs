using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XunfeiLite {
static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Contains("--self-test")){ try{ SelfTest.Run(); }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),ex.GetType().FullName+" "+ex.Message+"\r\n"+ex.StackTrace);Environment.ExitCode=1;} return; }
  bool fresh; using(var mutex=new Mutex(true,"Local\\XunfeiLite.Singleton",out fresh)) {
   if(!fresh)return; Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Host());
  }
 }
}
class Credentials {
 public string AppId="", ApiKey="", ApiSecret="";
 static string PathName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XunfeiLite","credentials.bin"); } }
 public static Credentials Load(){try{return new JavaScriptSerializer().Deserialize<Credentials>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(PathName),null,DataProtectionScope.CurrentUser)));}catch{return new Credentials();}}
 public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName));File.WriteAllBytes(PathName,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(this)),null,DataProtectionScope.CurrentUser));}
 public bool Valid {get{return !string.IsNullOrWhiteSpace(AppId)&&!string.IsNullOrWhiteSpace(ApiKey)&&!string.IsNullOrWhiteSpace(ApiSecret);}}
}
class HotkeyConfig {
 public int Key=(int)Keys.F5; public uint Modifiers=0;
 static string FileName {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XunfeiLite","hotkey.json");}}
 public static HotkeyConfig Load(){try{var c=new JavaScriptSerializer().Deserialize<HotkeyConfig>(File.ReadAllText(FileName));if(c.Key<(int)Keys.F1||c.Key>(int)Keys.F12||(c.Modifiers&~7u)!=0)throw new Exception();return c;}catch{return new HotkeyConfig();}}
 public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FileName));File.WriteAllText(FileName,new JavaScriptSerializer().Serialize(this));}
 public override string ToString(){return ((Modifiers&2)!=0?"Ctrl+":"")+((Modifiers&1)!=0?"Alt+":"")+((Modifiers&4)!=0?"Shift+":"")+((Keys)Key).ToString();}
}
class Session : IDisposable {
 Credentials c;AppOptions options;Action<string> partial;ClientWebSocket ws=new ClientWebSocket();CancellationTokenSource cancel=new CancellationTokenSource();BlockingCollection<byte[]> queue=new BlockingCollection<byte[]>(200);Recorder recorder;Task sender;Task<string> receiver;
 public Session(Credentials c,AppOptions options,Action<string> partial){this.c=c;this.options=options;this.partial=partial;cancel.CancelAfter(TimeSpan.FromSeconds(75));}
 public static string Url(Credentials c,string date){const string host="iat-api.xfyun.cn";string source="host: "+host+"\ndate: "+date+"\nGET /v2/iat HTTP/1.1";string sig;using(var h=new HMACSHA256(Encoding.UTF8.GetBytes(c.ApiSecret)))sig=Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(source)));string auth="api_key=\""+c.ApiKey+"\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\""+sig+"\"";return "wss://"+host+"/v2/iat?authorization="+Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(auth)))+"&date="+Uri.EscapeDataString(date)+"&host="+host;}
 public Task<string> Completion {get{return receiver;}}
 public void StopCapture(){if(recorder!=null){recorder.Dispose();recorder=null;}if(!queue.IsAddingCompleted)queue.CompleteAdding();}
 public async Task Connect(){var connect=ws.ConnectAsync(new Uri(Url(c,DateTime.UtcNow.ToString("r"))),cancel.Token);if(await Task.WhenAny(connect,Task.Delay(10000))!=connect){cancel.Cancel();throw new Exception("连接超时，请检查网络。");}await connect;receiver=Receive();sender=Task.Run(()=>Send());}
 public void Start(){recorder=new Recorder(bytes=>{if(!queue.TryAdd(bytes))cancel.Cancel();});recorder.Start();}
 async Task Send(){bool first=true;foreach(var bytes in queue.GetConsumingEnumerable(cancel.Token)){await Frame(first?0:1,bytes);first=false;}if(first)await Frame(0,new byte[0]);await Frame(2,new byte[0]);}
 async Task Frame(int status,byte[] bytes){var payload=new Dictionary<string,object>();if(status==0){payload["common"]=new{app_id=c.AppId};payload["business"]=options.Business();}payload["data"]=new{status=status,format="audio/L16;rate=16000",encoding="raw",audio=Convert.ToBase64String(bytes)};byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(payload));await ws.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Text,true,cancel.Token);}
 async Task<string> Receive(){var segments=new SortedDictionary<int,string>();var buffer=new byte[8192];while(true){using(var mem=new MemoryStream()){WebSocketReceiveResult frame;do{frame=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),cancel.Token);if(frame.MessageType==WebSocketMessageType.Close)throw new Exception("服务端提前关闭连接。");mem.Write(buffer,0,frame.Count);if(mem.Length>1048576)throw new Exception("响应过大。");}while(!frame.EndOfMessage);var root=new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(mem.ToArray())) as Dictionary<string,object>;int code=Convert.ToInt32(root["code"]);if(code!=0)throw new Exception("讯飞 "+code+"："+Convert.ToString(root["message"]));if(!root.ContainsKey("data"))continue;var data=(Dictionary<string,object>)root["data"];if(data.ContainsKey("result")){Apply(segments,(Dictionary<string,object>)data["result"]);if(partial!=null)partial(string.Concat(segments.Values));}if(Convert.ToInt32(data["status"])==2)return string.Concat(segments.Values);}}}
 public static void Apply(SortedDictionary<int,string> segments,Dictionary<string,object> r){if(r.ContainsKey("pgs")&&Convert.ToString(r["pgs"])=="rpl"&&r.ContainsKey("rg")){var rg=(System.Collections.IList)r["rg"];foreach(int key in segments.Keys.Where(k=>k>=Convert.ToInt32(rg[0])&&k<=Convert.ToInt32(rg[1])).ToArray())segments.Remove(key);}var text=new StringBuilder();foreach(Dictionary<string,object> word in (System.Collections.IList)r["ws"]){var cw=(System.Collections.IList)word["cw"];if(cw.Count>0)text.Append(((Dictionary<string,object>)cw[0])["w"]);}segments[Convert.ToInt32(r["sn"])]=text.ToString();}
 public async Task<string> Finish(){StopCapture();if(sender==null)throw new Exception("连接尚未完成，请稍后重试。");var all=Task.WhenAll(sender,receiver);if(await Task.WhenAny(all,Task.Delay(12000))!=all){Cancel();throw new Exception("等待结果超时。");}await all;return receiver.Result;}
 public void Cancel(){cancel.Cancel();ws.Abort();}
 public void Dispose(){if(recorder!=null){recorder.Dispose();recorder=null;}cancel.Cancel();ws.Dispose();}
}
class Recorder : IDisposable {
 [StructLayout(LayoutKind.Sequential)] struct Format{public ushort tag,channels;public uint rate,bytes;public ushort align,bits,extra;}
 [StructLayout(LayoutKind.Sequential)] struct Header{public IntPtr data;public uint length,recorded;public UIntPtr user;public uint flags,loops;public IntPtr next;public UIntPtr reserved;}
 delegate void Callback(IntPtr h,uint msg,IntPtr instance,IntPtr p1,IntPtr p2);
 [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr h,uint id,ref Format f,Callback callback,IntPtr instance,uint flags);
 [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern int waveInStart(IntPtr h);
 [DllImport("winmm.dll")] static extern int waveInReset(IntPtr h);
 [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern int waveInClose(IntPtr h);
 IntPtr handle;Callback callback;Action<byte[]> consume;List<IntPtr> headers=new List<IntPtr>();volatile bool closing;uint size=(uint)Marshal.SizeOf(typeof(Header));
 public Recorder(Action<byte[]> consume){this.consume=consume;callback=OnData;}
 static void Check(int code){if(code!=0)throw new Exception("麦克风错误 "+code+"，请检查默认录音设备及麦克风权限。");}
 public void Start(){var f=new Format{tag=1,channels=1,rate=16000,bytes=32000,align=2,bits=16};Check(waveInOpen(out handle,0xffffffff,ref f,callback,IntPtr.Zero,0x30000));try{for(int i=0;i<8;i++){var h=new Header{data=Marshal.AllocHGlobal(1280),length=1280};IntPtr p=Marshal.AllocHGlobal((int)size);Marshal.StructureToPtr(h,p,false);headers.Add(p);Check(waveInPrepareHeader(handle,p,size));Check(waveInAddBuffer(handle,p,size));}Check(waveInStart(handle));}catch{Dispose();throw;}}
 void OnData(IntPtr h,uint msg,IntPtr instance,IntPtr p1,IntPtr p2){if(msg!=0x3c0)return;try{var head=(Header)Marshal.PtrToStructure(p1,typeof(Header));if(head.recorded>0){var bytes=new byte[head.recorded];Marshal.Copy(head.data,bytes,0,bytes.Length);consume(bytes);}if(!closing)waveInAddBuffer(handle,p1,size);}catch{closing=true;}}
 public void Dispose(){if(handle==IntPtr.Zero)return;closing=true;waveInReset(handle);foreach(var p in headers){waveInUnprepareHeader(handle,p,size);var h=(Header)Marshal.PtrToStructure(p,typeof(Header));Marshal.FreeHGlobal(h.data);Marshal.FreeHGlobal(p);}headers.Clear();waveInClose(handle);handle=IntPtr.Zero;GC.KeepAlive(callback);}
}
static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct Input {public uint type;public InputUnion data;}
 [StructLayout(LayoutKind.Explicit)] public struct InputUnion {[FieldOffset(0)] public KeyboardInput keyboard;[FieldOffset(0)] public MouseInput mouse;}
 [StructLayout(LayoutKind.Sequential)] public struct KeyboardInput {public ushort key,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct MouseInput {public int x,y;public uint mouseData,flags,time;public UIntPtr extra;}
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
 public static Input[] TextInputs(string text){var result=new List<Input>();foreach(char ch in text){if(ch=='\r')continue;bool special=ch=='\n'||ch=='\t';ushort vk=special?(ushort)(ch=='\n'?13:9):(ushort)0;ushort scan=special?(ushort)0:(ushort)ch;uint flag=special?0u:4u;result.Add(new Input{type=1,data=new InputUnion{keyboard=new KeyboardInput{key=vk,scan=scan,flags=flag}}});result.Add(new Input{type=1,data=new InputUnion{keyboard=new KeyboardInput{key=vk,scan=scan,flags=flag|2u}}});}return result.ToArray();}
 public static void TypeUnicode(string text,IntPtr expectedWindow){var inputs=TextInputs(text);for(int i=0;i<inputs.Length;i+=64){if(GetForegroundWindow()!=expectedWindow)throw new Exception("输入期间窗口已切换，已停止。可在托盘查看完整结果。");var batch=inputs.Skip(i).Take(64).ToArray();uint sent=SendInput((uint)batch.Length,batch,Marshal.SizeOf(typeof(Input)));if(sent!=(uint)batch.Length)throw new Exception("直接输入被目标程序拦截。请检查目标窗口权限；可在托盘查看识别结果。");}}
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
static class SelfTest {
 public static void Run(){FeatureTests.Run();if(Marshal.SizeOf(typeof(Native.Input))!=40)throw new Exception("x64 INPUT layout failed");var typed=Native.TextInputs("中A😀\r\n\t");if(typed.Length!=12||typed[0].data.keyboard.scan!='中'||typed[0].data.keyboard.flags!=4||typed[1].data.keyboard.flags!=6||typed[8].data.keyboard.key!=13||typed[10].data.keyboard.key!=9)throw new Exception("Unicode input test failed");var j=new JavaScriptSerializer();var s=new SortedDictionary<int,string>();Session.Apply(s,j.Deserialize<Dictionary<string,object>>("{\"sn\":0,\"ws\":[{\"cw\":[{\"w\":\"你\"}]}]}"));Session.Apply(s,j.Deserialize<Dictionary<string,object>>("{\"sn\":1,\"pgs\":\"rpl\",\"rg\":[0,0],\"ws\":[{\"cw\":[{\"w\":\"你好。\"}]}]}"));if(string.Concat(s.Values)!="你好。")throw new Exception("Replacement test failed");var c=new Credentials{AppId="test",ApiKey="key",ApiSecret="secret"};string url=Session.Url(c,"Tue, 10 Jul 2019 07:35:43 GMT");if(!url.StartsWith("wss://iat-api.xfyun.cn/v2/iat?authorization="))throw new Exception("URL test failed");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"PASS: ASR parameter mapping (Mandarin/English/dialect), settings round-trip, geometry recovery, validation, microphone icon; Unicode input events (Chinese, ASCII, surrogate pair, newline, tab), x64 INPUT layout, dynamic replacement, WebSocket authentication URL\r\n");}
}
}





