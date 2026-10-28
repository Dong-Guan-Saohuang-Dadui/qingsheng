using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace XunfeiLite {
static class ContinuousTests {
 class Fake : IRecognitionPart {
  public readonly List<byte> Bytes=new List<byte>(); public bool Sealed;
  readonly int id;public bool SuccessfulClose;readonly TaskCompletionSource<string> done=new TaskCompletionSource<string>();
  public Fake(int id){this.id=id;}
  public Task Connect(){return Task.FromResult(0);}
  public Task<string> Completion {get{return done.Task;}}
  public void PushAudio(byte[] bytes){if(Sealed)throw new Exception("Audio after seal");Bytes.AddRange(bytes);}
  public void Seal(){Sealed=true;if(SuccessfulClose)done.TrySetResult("段"+id);}
  public async Task<string> Finish(){Seal();if(SuccessfulClose)throw new IOException("Connection closed after final result");await Task.Delay(id==0?40:1);done.TrySetResult("段"+id);return await done.Task;}
  public void Cancel(){}public void Dispose(){}
 }
 static void Check(bool ok,string error){if(!ok)throw new Exception(error);}
 public static void Run(){
  foreach(int seconds in new[]{1,55,110,125}){
   var fakes=new List<Fake>();var gate=new object();
   using(var stream=new SegmentedSession(cb=>{lock(gate){var f=new Fake(fakes.Count);fakes.Add(f);return f;}},null,-1)){
    stream.Connect().GetAwaiter().GetResult();stream.StartWorker();
    var original=new List<byte>();
    for(int i=0;i<seconds;i++){var b=Enumerable.Repeat((byte)i,32000).ToArray();original.AddRange(b);stream.Push(b);}
    string text=stream.Finish().GetAwaiter().GetResult();
    var sent=fakes.Where(f=>f.Bytes.Count>0).ToArray();
    Check(sent.Length==(seconds+54)/55,"Segment count / empty tail failed");
    Check(sent.All(f=>f.Bytes.Count<=SegmentedSession.SegmentBytes),"Segment exceeded 55 seconds");
    Check(sent.SelectMany(f=>f.Bytes).SequenceEqual(original),"Missing, replayed or reordered PCM");
    Check(text==string.Concat(Enumerable.Range(0,sent.Length).Select(i=>"段"+i)),"Out-of-order final results");
   }
  }
  using(var closingStream=new SegmentedSession(cb=>new Fake(0){SuccessfulClose=true},null,-1)){
   closingStream.Connect().GetAwaiter().GetResult();closingStream.StartWorker();closingStream.Push(new byte[1280]);
   Check(closingStream.Finish().GetAwaiter().GetResult()=="段0","Final response lost in close race");
  }
  var options=new AppOptions{MicrophoneId=7,MicrophoneName="USB麦克风"};
  var restored=new JavaScriptSerializer().Deserialize<AppOptions>(new JavaScriptSerializer().Serialize(options));
  Check(restored.MicrophoneId==7&&restored.MicrophoneName==options.MicrophoneName,"Microphone persistence");
  Check(Recorder.ResolveDevice(options,new List<MicDevice>{new MicDevice(2,"USB麦克风")})==2,"Microphone ID reorder recovery");
  bool missing=false;try{Recorder.ResolveDevice(options,new List<MicDevice>());}catch{missing=true;}Check(missing,"Missing microphone silently substituted");
  Check(Recorder.Level(new byte[1280])==0,"Silence meter");
  Check(Recorder.Level(new byte[]{255,127,1,128})>90,"Signal meter");
  var samples=new byte[]{232,3,24,252,32,78,224,177};
  var unity=(byte[])samples.Clone();Recorder.ApplyGain(unity,100);Check(unity.SequenceEqual(samples),"Unity gain changed PCM");
  Recorder.ApplyGain(samples,200);
  Check(BitConverter.ToInt16(samples,0)==2000&&BitConverter.ToInt16(samples,2)==-2000,"Gain polarity or scale");
  Check(BitConverter.ToInt16(samples,4)==32767&&BitConverter.ToInt16(samples,6)==-32768,"Gain clipping wrapped PCM");
  Recorder.ApplyGain(samples,0);Check(samples.All(v=>v==0),"Zero gain not silent");
  var legacy=new JavaScriptSerializer().Deserialize<AppOptions>("{}");Check(legacy.MicrophoneGainPercent==100,"Legacy config gain default");
  options.MicrophoneGainPercent=240;restored=new JavaScriptSerializer().Deserialize<AppOptions>(new JavaScriptSerializer().Serialize(options));Check(restored.MicrophoneGainPercent==240,"Gain persistence");
 }
 // Deliberate developer test: uses supplied PCM at real-time speed, not the mic,
 // never types into any window, and never logs credentials or signed URLs.
 public static void MicTest(){
  var devices=Recorder.Devices();var options=AppOptions.Load();int device=Recorder.ResolveDevice(options,devices);int peak=0,frames=0;
  using(var r=new Recorder(b=>{Interlocked.Increment(ref frames);peak=Math.Max(peak,Recorder.Level(b));},device)){r.Start();Thread.Sleep(2000);}
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mic-test.txt"),"PASS: capture opened; frames="+frames+"; peak="+peak+"%\r\n"+string.Join("\r\n",devices.Select(d=>d.Id+": "+d.Name)));
 }
 public static void LiveFile(string file){
  var output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"stream-test.txt");
  var creds=Credentials.Load();if(!creds.Valid)throw new Exception("请先配置讯飞凭据。");
  int requests=0;long bytes=0;var started=DateTime.UtcNow;
  using(var stream=new SegmentedSession(cb=>{Interlocked.Increment(ref requests);return new Session(creds,AppOptions.Load(),cb);},null,-1)){
   stream.Connect().GetAwaiter().GetResult();stream.StartWorker();
   using(var f=File.OpenRead(file)){var block=new byte[1280];int n;while((n=f.Read(block,0,block.Length))>0){if(stream.Completion.IsCompleted)throw new Exception("识别在预期结束前停止。已保留结果："+stream.CurrentText);var copy=new byte[n];Buffer.BlockCopy(block,0,copy,0,n);stream.Push(copy);bytes+=n;int wait=(int)Math.Ceiling(bytes*1000.0/32000-(DateTime.UtcNow-started).TotalMilliseconds);if(wait>0)Thread.Sleep(wait);}}
   string result=stream.Finish().GetAwaiter().GetResult();
   File.WriteAllText(output,"PASS: real-time segmented recognition\r\nAudio seconds: "+bytes/32000.0+"\r\nConnections: "+requests+"\r\nElapsed seconds: "+(DateTime.UtcNow-started).TotalSeconds.ToString("F1")+"\r\nResult: "+result);
  }
 }
}
}
