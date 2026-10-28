using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XunfeiLite {
interface IRecognitionPart : IDisposable {
 Task Connect(); Task<string> Completion {get;}
 void PushAudio(byte[] bytes); void Seal(); Task<string> Finish(); void Cancel();
}

// A single recorder owns capture for the entire utterance. PCM is partitioned,
// never replayed or heuristically deduplicated, so intentional repetitions survive.
class SegmentedSession : IDisposable {
 public const int SegmentBytes=55*16000*2;
 const int PrepareBytes=54*16000*2;
 class Part {public IRecognitionPart Wire;public string Text="";}
 readonly object gate=new object(),captureGate=new object();
 readonly BlockingCollection<byte[]> audio=new BlockingCollection<byte[]>(250);
 readonly CancellationTokenSource cancellation=new CancellationTokenSource();
 readonly List<Part> parts=new List<Part>();
 readonly List<Task> finals=new List<Task>();
 readonly Func<Action<string>,IRecognitionPart> factory;
 readonly Action<string> partial;
 int device;
 volatile int gainPercent=100;
 public void SetGain(int percent){gainPercent=Math.Max(0,Math.Min(400,percent));}
 AppOptions deviceOptions;
 Part active,preparing;
 Task<Part> prepared;
 Task<string> worker;
 Recorder recorder;
 Exception captureError;
 bool disposed;
 public SegmentedSession(Credentials c,AppOptions options,Action<string> partial)
  :this(callback=>new Session(c,options.Copy(),callback),partial,-1){deviceOptions=options.Copy();SetGain(options.MicrophoneGainPercent);}
 internal SegmentedSession(Func<Action<string>,IRecognitionPart> factory,Action<string> partial,int device){this.factory=factory;this.partial=partial;this.device=device;}
 public string CurrentText {get{lock(gate)return string.Concat(parts.Select(p=>p.Text));}}
 void Publish(Part p,string text){string all;lock(gate){p.Text=text;all=string.Concat(parts.Select(x=>x.Text));}if(partial!=null)partial(all);}
 async Task<Part> Open(){
  var p=new Part();p.Wire=factory(text=>Publish(p,text));lock(gate)preparing=p;
  try{await p.Wire.Connect();cancellation.Token.ThrowIfCancellationRequested();return p;}
  catch{p.Wire.Dispose();throw;}
 }
 public async Task Connect(){if(deviceOptions!=null)device=Recorder.ResolveDevice(deviceOptions,Recorder.Devices());active=await Open();lock(gate){parts.Add(active);preparing=null;}}
 public Task<string> Completion {get{return worker;}}
 public void Start(){lock(captureGate){recorder=new Recorder(bytes=>{Recorder.ApplyGain(bytes,gainPercent);Push(bytes);},device);try{recorder.Start();}catch{recorder.Dispose();recorder=null;throw;}}StartWorker();}
 internal void StartWorker(){worker=Task.Run(()=>Run());}
 internal void Push(byte[] bytes){
  try{if(!audio.TryAdd(bytes)){captureError=new Exception("录音缓存已满，已停止以避免音频丢失。请检查网络。");cancellation.Cancel();}}
  catch(InvalidOperationException){/* Capture has already been stopped. */}
 }
 public void StopCapture(){lock(captureGate){if(recorder!=null){recorder.Dispose();recorder=null;}if(!audio.IsAddingCompleted)audio.CompleteAdding();}}
 public async Task<string> Finish(){StopCapture();return await worker;}
 async Task FinalizePart(Part p){try{string text;try{text=p.Wire.Completion.Status==TaskStatus.RanToCompletion?await p.Wire.Completion:await p.Wire.Finish();}catch{if(p.Wire.Completion.Status!=TaskStatus.RanToCompletion)throw;text=p.Wire.Completion.Result;}Publish(p,text);}finally{p.Wire.Dispose();}}
 void Prepare(){if(prepared==null){prepared=Open();prepared.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}}
 async Task Activate(){
  if(prepared==null)Prepare();active=await prepared;prepared=null;
  lock(gate){parts.Add(active);preparing=null;}
 }
 async Task<string> Run(){
  int sent=0;
  try{
   while(true){
    cancellation.Token.ThrowIfCancellationRequested();
    foreach(var final in finals)if(final.IsFaulted)await final;
    // Server silence detection still ends the utterance as before.
    if(active!=null&&active.Wire.Completion.IsCompleted){
     string text=await active.Wire.Completion;Publish(active,text);StopCapture();active.Wire.Cancel();active=null;break;
    }
    byte[] bytes;
    if(!audio.TryTake(out bytes,100,cancellation.Token)){if(audio.IsCompleted)break;continue;}
    int offset=0;
    while(offset<bytes.Length){
     if(active==null){await Activate();sent=0;}
     int take=Math.Min(SegmentBytes-sent,bytes.Length-offset);
     var piece=new byte[take];Buffer.BlockCopy(bytes,offset,piece,0,take);
     active.Wire.PushAudio(piece);sent+=take;offset+=take;
     if(sent>=PrepareBytes)Prepare();
     if(sent==SegmentBytes){
      // Close the old audio stream immediately at 55 seconds, even if opening
      // the next socket is slow. The recorder keeps feeding the bounded queue.
      var old=active;active=null;old.Wire.Seal();finals.Add(FinalizePart(old));
     }
    }
   }
   if(active!=null){var last=active;active=null;last.Wire.Seal();finals.Add(FinalizePart(last));}
   await Task.WhenAll(finals);
   if(captureError!=null)throw captureError;
   return CurrentText;
  }catch(OperationCanceledException){if(captureError!=null)throw captureError;throw;}
  finally{StopCapture();ClosePrepared();}
 }
 void ClosePrepared(){Part p;lock(gate){p=preparing;preparing=null;}if(p!=null){try{p.Wire.Cancel();p.Wire.Dispose();}catch(ObjectDisposedException){}}}
 public void Cancel(){
  cancellation.Cancel();StopCapture();Part[] all;lock(gate)all=parts.ToArray();
  foreach(var p in all)try{p.Wire.Cancel();}catch(ObjectDisposedException){}
  ClosePrepared();
 }
 public void Dispose(){if(disposed)return;disposed=true;Cancel();Part[] all;lock(gate)all=parts.ToArray();foreach(var p in all)p.Wire.Dispose();}
}
}
