using System;
using System.Drawing;
using System.Web.Script.Serialization;
namespace XunfeiLite {
static class FeatureTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var options=new AppOptions();options.Validate();var b=options.Business();Assert(b.ContainsKey("eos")&&!b.ContainsKey("vad_eos")&&Convert.ToInt32(b["eos"])==10000,"Silence parameter mapping failed");Assert(Convert.ToString(b["dwa"])=="wpgs","Mandarin correction failed");
  options.Punctuation=false;options.Numbers=false;options.Script="zh-hk";b=options.Business();Assert(Convert.ToInt32(b["ptt"])==0&&Convert.ToInt32(b["nunum"])==0&&Convert.ToString(b["rlang"])=="zh-hk","Chinese options mapping failed");
  options.Language="en_us";b=options.Business();Assert(!b.ContainsKey("dwa")&&!b.ContainsKey("ptt")&&!b.ContainsKey("rlang"),"English unsupported option filtering failed");
  options.Language="zh_cn";options.Accent="custom_authorized_accent";b=options.Business();Assert(!b.ContainsKey("dwa")&&Convert.ToString(b["accent"])==options.Accent,"Dialect mapping failed");
  options.Domain="xfime-mianqie";options.Accent="mandarin";b=options.Business();Assert(!b.ContainsKey("dwa")&&Convert.ToString(b["domain"])=="xfime-mianqie","Mixed dialect mapping failed");
  options.Width=350;options.Height=100;options.X=31000;options.Y=31000;var bounds=options.Bounds();Assert(bounds.Size==new Size(350,100)&&bounds.X!=31000,"Off-screen geometry recovery failed");
  options.X=0;options.Y=0;options.ShowWindow=false;options.AutoInput=false;var restored=new JavaScriptSerializer().Deserialize<AppOptions>(new JavaScriptSerializer().Serialize(options));Assert(!restored.ShowWindow&&!restored.AutoInput&&restored.Width==350&&restored.X==0,"Settings round-trip failed");
  options.Eos=0;bool invalid=false;try{options.Validate();}catch{invalid=true;}Assert(invalid,"Invalid silence value accepted");
  using(var mic=MicArt.Create(Color.Blue)){Assert(mic.Width>0,"Microphone icon generation failed");}
 }
}
}
