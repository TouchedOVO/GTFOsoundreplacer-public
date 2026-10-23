using System;
using System.Collections.Generic;
using System.Reflection;
using GTFOSoundReplacer.Audio;

// Boundary fixtures, not a Unity/Harmony/Wwise integration test.
namespace UnityEngine {
 public struct Vector3 { public float x,y,z; public static Vector3 zero=>default; }
 public class Transform { public Vector3 position; }
 public class Camera { public static Camera main; public Transform transform=new Transform(); }
 public static class Debug { public static void Log(object x){} }
}
namespace Il2CppInterop.Runtime.InteropTypes {
 public class Il2CppObjectBase { private static long next=100; public IntPtr Pointer=new IntPtr(System.Threading.Interlocked.Increment(ref next)); public T TryCast<T>() where T:class {return this as T;} }
}
namespace Il2CppSystem { public class Object:Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase {} }
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n,Type[] p){} }
 public class HarmonyMethod {public HarmonyMethod(Type t,string n){} }
 public class Harmony {public Harmony(string id){} public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null,HarmonyMethod finalizer=null){} public void UnpatchSelf(){} }
}
public enum AkCallbackType { AK_EndOfEvent=1 }
public class AkCallbackInfo:Il2CppSystem.Object {}
public class AkEventCallbackInfo:AkCallbackInfo {public uint eventID,playingID;}
public static class AkCallbackManager {
 public sealed class EventCallback:Il2CppSystem.Object {
  private Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo> fn;
  public static implicit operator EventCallback(Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo> f)=>new EventCallback{fn=f};
  public void Invoke(Il2CppSystem.Object c,AkCallbackType t,AkCallbackInfo i)=>fn(c,t,i);
 }
}
public class CellSoundPlayer:Il2CppSystem.Object {
 public uint Post(uint id,UnityEngine.Vector3 p,uint f,AkCallbackManager.EventCallback cb,Il2CppSystem.Object c){ReplacementVoiceChecks.FallbackCalls++;return ReplacementVoiceChecks.FallbackId;}
}
public class PlayerVoice:Il2CppSystem.Object {
 public bool m_isSetup=true;public CellSoundPlayer m_sound=new CellSoundPlayer();
 public void SayLine(uint a,uint b,uint c,uint d,uint e){} public void VoiceCallback(Il2CppSystem.Object c,AkCallbackType k,Il2CppSystem.Object i){} public void OnDestroy(){}
}
namespace Player {public static class PlayerDialogManager {public static void OnDialogLineFinished(uint a,uint b){} } }
public static class AkSoundEngine {public static uint GetIDFromString(string name)=>1;}
namespace GTFOSoundReplacer {
 public class Cfg<T>{public T Value;}
 public static class PluginConfig {public static Cfg<float> GlobalVolume=new Cfg<float>{Value=1};public static Cfg<bool> LogEventIds=new Cfg<bool>{Value=true};}
 public class FakeLog {public List<string> Lines=new List<string>();public void LogInfo(object s){Lines.Add(s.ToString());}public void LogWarning(object s){Lines.Add(s.ToString());}}
 public class Plugin {public const string GUID="fixture";public static Plugin Instance=new Plugin();public FakeLog Log=new FakeLog();}
}
namespace GTFOSoundReplacer.UI {public static class SoundCatalog {public static string LookupName(uint id)=>"fixture";} }
namespace GTFOSoundReplacer.Audio {
 internal static class PreviewService {public static bool LifecycleReady=>true;public static void Notify(string s,bool failed){} public static void Stop(string s){} }
 internal static class ListenerRouting {public static bool IsShutdown;}
 public class SoundBankPlayer {public bool Result=true,Fail;public int Calls;public uint Target;public float Volume;public bool Play(ReplacementEntry e,UnityEngine.Vector3 p,float v){Calls++;Target=e.BankEventId;Volume=v;if(Fail)throw new Exception("fixture");return Result;} }
}
public static class ReplacementVoiceChecks
{
 private static int Count;
 public static int FallbackCalls;public static uint FallbackId=900;
 private static readonly Type Service=typeof(VoiceReplacement);
 private static object Call(string method,params object[] args) {try{return Service.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 private static void Check(bool pass,string name){if(!pass)throw new Exception("FAIL replacement/voice: "+name);Count++;}
 private static void Reset(){Call("CallbackManagerGone");foreach(var name in new[]{"_ready","_disabled","_shutdown","_callbacksGone"})Service.GetField(name,BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);Service.GetField("Current",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);Service.GetField("Callback",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);SoundReplacer.Initialize();GTFOSoundReplacer.PluginConfig.GlobalVolume.Value=1;Call("Initialize");FallbackCalls=0;ListenerRouting.IsShutdown=false;}
 private static object Enter(PlayerVoice voice,uint id,uint dialog){var args=new object[]{voice,id,dialog,null};Call("SayBefore",args);return args[3];}
 private static void Leave(object previous){Call("SayAfter",previous,null);}
 private static object[] Prepare(PlayerVoice voice,uint id,AkCallbackManager.EventCallback cb){var args=new object[]{voice.m_sound,id,1u,cb,null};Call("PostBefore",args);return args;}
 private static uint Posted(PlayerVoice voice,object ticket,uint id){var args=new object[]{voice.m_sound,default(UnityEngine.Vector3),1u,new Il2CppSystem.Object(),ticket,id};Call("PostAfter",args);return (uint)args[5];}
 public static int Run()
 {
  Count=0;Reset();var map=SoundReplacer.Instance.Map;var player=SoundReplacer.Instance.SoundBank;
  Check(AudioHook.PrefixCore(7,default),"no mapping lets original run");
  map.Set(7,new ReplacementEntry{BankEventId=99});
  Check(!AudioHook.PrefixCore(7,default)&&player.Calls==1&&player.Target==99,"successful replacement always suppresses original");
  map.TryGet(7,out var entry);entry.Enabled=false;int before=player.Calls;
  Check(AudioHook.PrefixCore(7,default)&&player.Calls==before,"disabled replacement does not submit");
  entry.Enabled=true;entry.BankEventId=0;
  Check(AudioHook.PrefixCore(7,default)&&player.Calls==before,"unassigned target lets original run");
  entry.BankEventId=99;entry.Volume=0;
  Check(!AudioHook.PrefixCore(7,default)&&player.Calls==before,"zero volume suppresses both without posting");
  entry.Volume=1;player.Result=false;
  Check(AudioHook.PrefixCore(7,default),"rejected post lets original run");
  player.Result=true;player.Fail=true;
  Check(AudioHook.PrefixCore(7,default),"post exception falls back to original");player.Fail=false;
  entry.Volume=.5f;GTFOSoundReplacer.PluginConfig.GlobalVolume.Value=.5f;
  Check(!AudioHook.PrefixCore(7,default)&&player.Volume==.25f,"ordinary volume multiplication preserved");
  Check(typeof(ReplacementEntry).GetProperty("MuteOriginal")==null,"runtime mixing option absent");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});
  var voice=new PlayerVoice();var other=new PlayerVoice();var notifications=new List<uint>();var cookies=new List<Il2CppSystem.Object>();int deliveries=0;
  Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo> fn=(cookie,kind,info)=>{
   deliveries++;cookies.Add(cookie);var start=new object[]{voice,0L};Call("CallbackBefore",start);
   try{var args=new object[]{((AkEventCallbackInfo)info).eventID,20u};Call("DialogBefore",args);notifications.Add((uint)args[0]);}
   finally{Call("CallbackAfter",start[1],null);}
  };
  AkCallbackManager.EventCallback original=fn;
  var unrelated=Prepare(voice,10,original);
  Check((uint)unrelated[1]==10&&unrelated[4]==null,"callback post outside SayLine remains original");
  var prev=Enter(voice,10,20);
  var wrong=Prepare(other,10,original);Check(wrong[4]==null,"other sound is not classified as voice request");
  var prepared=Prepare(voice,10,original);var ticket=prepared[4];var wrapper=(AkCallbackManager.EventCallback)prepared[3];
  Check((uint)prepared[1]==100&&ticket!=null&&!object.ReferenceEquals(wrapper,original),"matching request changes event and wraps callback");
  Check(Posted(voice,ticket,77)==77,"real playing ID is retained");Leave(prev);
  var cookie1=new Il2CppSystem.Object();wrapper.Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=77});
  Check(deliveries==1&&notifications[0]==10&&object.ReferenceEquals(cookies[0],cookie1),"original identity restored only at completion; original cookie retained");
  wrapper.Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=77});
  Check(deliveries==1,"duplicate end does not double-notify");
  var raw=new object[]{100u,20u};Call("DialogBefore",raw);Check((uint)raw[0]==100,"unrelated completion ID is never globally remapped");
  prev=Enter(voice,10,20);var early=Prepare(voice,10,original);((AkCallbackManager.EventCallback)early[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=78});
  Check(Posted(voice,early[4],78)==78&&notifications[1]==10,"callback before Post returns is correctly associated");Leave(prev);

  map.Set(11,new ReplacementEntry{BankEventId=100});prev=Enter(voice,10,20);var old=Prepare(voice,10,original);Posted(voice,old[4],80);Leave(prev);
  prev=Enter(voice,11,20);var newer=Prepare(voice,11,original);Posted(voice,newer[4],81);Leave(prev);
  ((AkCallbackManager.EventCallback)old[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=80});
  ((AkCallbackManager.EventCallback)newer[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=81});
  Check(notifications[2]==10&&notifications[3]==11,"same target event preserves old and new original identities separately");

  prev=Enter(voice,10,20);var nested=Enter(other,11,20);Leave(nested);var restored=Prepare(voice,10,original);Check(restored[4]!=null,"nested SayLine restores outer identity");Posted(voice,restored[4],82);Leave(prev);
  ((AkCallbackManager.EventCallback)restored[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=82});
  Check(Prepare(voice,10,original)[4]==null,"leaving SayLine clears context");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100,Volume=0});prev=Enter(voice,10,20);Check(Prepare(voice,10,original)[4]!=null,"legacy voice zero volume now remains a fixed-unit replacement");Leave(prev);
  map.TryGet(10,out entry);entry.Volume=.5f;prev=Enter(voice,10,20);Check(Prepare(voice,10,original)[4]!=null,"legacy nonunit voice volume no longer falls back to original");Leave(prev);
  entry.Volume=1;prev=Enter(voice,10,20);var failed=Prepare(voice,10,original);Check(Posted(voice,failed[4],0)==900&&FallbackCalls==1,"zero Post returns original fallback ID exactly once");Leave(prev);
  prev=Enter(voice,10,20);Check(Prepare(voice,10,original)[4]==null,"failed submission disables further replacement allocations");Leave(prev);
  Call("CallbackManagerGone");Check((int)Call("Count")==0,"callback teardown releases retained failure record");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});prev=Enter(voice,10,20);var uncertain=Prepare(voice,10,original);var error=new Exception("native fixture");Check(object.ReferenceEquals(Call("PostFinally",uncertain[4],error),error)&&FallbackCalls==0,"unknown native exception preserved without blind replay");Leave(prev);
  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});Call("BeforeTerm");prev=Enter(voice,10,20);Check(Prepare(voice,10,original)[4]==null,"Term prohibits new replacement submissions");Leave(prev);
  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});
  prev=Enter(voice,10,20);var immutable=Prepare(voice,10,original);Posted(voice,immutable[4],85);Leave(prev);
  var originalInfo=new AkEventCallbackInfo{eventID=100,playingID=85};((AkCallbackManager.EventCallback)immutable[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,originalInfo);
  Check(originalInfo.eventID==100&&originalInfo.playingID==85,"native callback information is never rewritten");
  Check((int)Call("Count")==0,"normal completion releases module request root");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});
  for(int i=0;i<128;i++){prev=Enter(voice,10,20);var retained=Prepare(voice,10,original);if(retained[4]==null)throw new Exception("capacity reached early");Leave(prev);}
  prev=Enter(voice,10,20);var overflow=Prepare(voice,10,original);Leave(prev);
  Check(overflow[4]==null&&(int)Call("Count")==128,"capacity is bounded and overflow leaves original event");
  Call("CallbackManagerGone");Check((int)Call("Count")==0,"teardown clears every retained record at capacity");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});
  AkCallbackManager.EventCallback throwing=(Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo>)((c,k,i)=>throw new InvalidOperationException("game callback fixture"));
  prev=Enter(voice,10,20);var thrown=Prepare(voice,10,throwing);Posted(voice,thrown[4],86);Leave(prev);bool propagated=false;
  try{((AkCallbackManager.EventCallback)thrown[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=86});}catch(InvalidOperationException){propagated=true;}
  Check(propagated&&(int)Call("Count")==0,"game callback error is preserved while module root is released");
  raw=new object[]{100u,20u};Call("DialogBefore",raw);Check((uint)raw[0]==100,"callback exception cannot leave identity conversion context behind");

  Reset();map=SoundReplacer.Instance.Map;map.Set(10,new ReplacementEntry{BankEventId=100});
  uint wrongNotification=0;
  AkCallbackManager.EventCallback wrongDialog=(Action<Il2CppSystem.Object,AkCallbackType,AkCallbackInfo>)((c,k,i)=>{var oldVoice=new object[]{voice,0L};Call("CallbackBefore",oldVoice);try{var a=new object[]{100u,999u};Call("DialogBefore",a);wrongNotification=(uint)a[0];}finally{Call("CallbackAfter",oldVoice[1],null);}});
  prev=Enter(voice,10,20);var mismatch=Prepare(voice,10,wrongDialog);Posted(voice,mismatch[4],87);Leave(prev);
  ((AkCallbackManager.EventCallback)mismatch[3]).Invoke(cookie1,AkCallbackType.AK_EndOfEvent,new AkEventCallbackInfo{eventID=100,playingID=87});
  Check(wrongNotification==100,"different dialog notification is never remapped");
  prev=Enter(voice,10,20);Check(Prepare(voice,10,original)[4]==null,"protocol mismatch disables subsequent replacement");Leave(prev);

  var request=new VoiceRequest(10,100,20,1,2);
  Check(!request.Bind(0)&&request.Bind(50)&&!request.Bind(51),"playing identity rejects zero and conflicting handles");
  Check(request.MatchesCompletion(100,20)&&!request.MatchesCompletion(100,21),"completion matches both target event and dialog");
  var fixedDecision=ReplacementResolver.Resolve(map,10,0f,fixedUnitVolume:true);
  Check(fixedDecision.Kind==ReplacementKind.Replace&&fixedDecision.Volume==1f,"fixed voice policy ignores both stored and global gain without mutating the source");
  var ordinaryDecision=ReplacementResolver.Resolve(map,10,0f);
  Check(ordinaryDecision.Kind==ReplacementKind.Silent&&ordinaryDecision.Volume==0f,"ordinary global zero volume remains silent");
  return Count;
 }
}
