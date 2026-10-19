using System;
using System.Collections.Generic;
using System.Reflection;
using GTFOSoundReplacer.Audio;

namespace UnityEngine {
 public struct Vector3 { public float x,y,z; }
 public class Transform { public Vector3 position; }
 public class Object { public static void DontDestroyOnLoad(Object o) {} }
 public class GameObject : Object {
  static ulong next=100; public ulong Id; public Transform transform=new Transform();
  public GameObject(string name) { Id=next++; }
 }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {
 public class Il2CppStructArray<T> { }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { }
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyPriority : Attribute { public HarmonyPriority(int p){} }
 public static class Priority { public const int First=800, Last=0; }
}
public enum AKRESULT { AK_Success=1, AK_Fail=2 }
public static class PlayerEngine {
 public static readonly List<string> Calls=new List<string>();
 public static ulong LastGainEmitter, LastGainListener; public static float LastGain;
 public static bool AllMutationsLocked=true; public static void AssertLocked(){AllMutationsLocked &= System.Threading.Monitor.IsEntered(ListenerRouting.Gate);}
 public static uint PostResult=99; public static AKRESULT RegisterResult=AKRESULT.AK_Success;
 public static void Clear(){Calls.Clear();}
}
public static class AkSoundEngine {
 public static ulong GetAkGameObjectID(UnityEngine.GameObject go) { return go==null ? ulong.MaxValue : go.Id; }
 public static AKRESULT LoadBank(IntPtr p,uint n,out uint bank) {bank=1;return AKRESULT.AK_Success;}
 public static AKRESULT UnloadBank(uint bank,IntPtr p) {return AKRESULT.AK_Success;}
 public static AKRESULT RegisterGameObj(UnityEngine.GameObject go) {PlayerEngine.Calls.Add("register");return PlayerEngine.RegisterResult;}
 public static uint PostEvent(uint ev,UnityEngine.GameObject go) {PlayerEngine.AssertLocked();PlayerEngine.Calls.Add("post");return PlayerEngine.PostResult;}
 public static AKRESULT SetRTPCValueByPlayingID(uint rtpc,float gain,uint id) {PlayerEngine.Calls.Add("rtpc:"+rtpc+":"+id);return AKRESULT.AK_Success;}
 public static AKRESULT SetDefaultListeners(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<ulong> a,uint n) {PlayerEngine.AssertLocked();PlayerEngine.Calls.Add("defaults");return AKRESULT.AK_Success;}
}
public static class AkSoundEnginePINVOKE {
 public static int CSharp_ResetListenersToDefault(ulong e) {PlayerEngine.AssertLocked();PlayerEngine.Calls.Add("reset");return 1;}
 public static int CSharp_SetGameObjectOutputBusVolume(ulong e,ulong l,float g) {
  PlayerEngine.AssertLocked();PlayerEngine.Calls.Add("gain");PlayerEngine.LastGainEmitter=e;PlayerEngine.LastGainListener=l;PlayerEngine.LastGain=g;return 1;
 }
}
namespace GTFOSoundReplacer {
 public class ReplacementEntry {public uint BankEventId;}
 public class FakeLog {public void LogInfo(object o) {}}
 public class Plugin {public static Plugin Instance;public FakeLog Log=new FakeLog();}
}
public static class PlayerBoundaryChecks {
 static int count;
 static void Check(bool yes,string label) {if(!yes)throw new Exception("FAIL: "+label);count++;}
 static void Calls(string wanted,string label) {Check(string.Join(",",PlayerEngine.Calls)==wanted,label+" actual="+string.Join(",",PlayerEngine.Calls));}
 static SoundBankPlayer Fresh() {
  typeof(ListenerRouting).GetField("Owned",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)
    .GetType().GetMethod("Clear").Invoke(typeof(ListenerRouting).GetField("Owned",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),null);
  SoundBankPlayer.VolumeRTPC=0;PlayerEngine.RegisterResult=AKRESULT.AK_Success;PlayerEngine.PostResult=99;
  PlayerEngine.Clear();return new SoundBankPlayer();
 }
 static void DefaultBoundary() {
  var hook=typeof(DefaultListenerHook);object[] args={false};
  hook.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
  try {
   AkSoundEngine.SetDefaultListeners(null,1);
   hook.GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{AKRESULT.AK_Success});
  } finally {hook.GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{null,args[0]});}
 }
 public static int Run() {
  count=0;var entry=new GTFOSoundReplacer.ReplacementEntry{BankEventId=123};var pos=new UnityEngine.Vector3();
  var p=Fresh();DefaultBoundary();Calls("defaults","default changes do not create an emitter");
  PlayerEngine.Clear();Check(p.Play(entry,pos,.35f),"valid replacement posts");
  Calls("register,post,gain","original initial Post then output gain order retained");
  var emitter=PlayerEngine.LastGainEmitter;
  Check(PlayerEngine.LastGainListener==ulong.MaxValue,"default listener sentinel passed unchanged");
  PlayerEngine.Clear();DefaultBoundary();Calls("defaults,reset,gain","actual hook submits repair after original default command");
  Check(PlayerEngine.LastGainEmitter==emitter && PlayerEngine.LastGain==.35f,"backend repairs same owned object at cached gain");
  PlayerEngine.Clear();p.PlayRaw(456,pos);Calls("post","preview preserves shared object and does not reset per post");
  PlayerEngine.Clear();p.Play(entry,pos,1);Calls("post","original near-unit early return retained");
  Check(PlayerEngine.LastGain==.35f,"unit playback does not silently change historical shared gain behavior");
  PlayerEngine.Clear();p.Play(entry,pos,-1);Calls("post","negative volume bypass retained");
  SoundBankPlayer.VolumeRTPC=1000;PlayerEngine.Clear();p.Play(entry,pos,.2f);
  Calls("post,rtpc:1000:99","RTPC goes to current playing id and bypasses output gain");
  PlayerEngine.Clear();DefaultBoundary();Calls("defaults,reset,gain","RTPC branch does not erase earlier object route state");
  Check(PlayerEngine.LastGain==.35f,"RTPC does not overwrite cached object gain");

  p=Fresh();SoundBankPlayer.VolumeRTPC=8;p.Play(entry,pos,.4f);PlayerEngine.Clear();DefaultBoundary();
  Calls("defaults","RTPC-only object remains default following without copied connections");
  p=Fresh();p.SetListener(new UnityEngine.GameObject("explicit"));p.Play(entry,pos,.4f);
  Check(PlayerEngine.LastGainListener!=ulong.MaxValue,"explicit listener forwarded to output volume");
  PlayerEngine.Clear();DefaultBoundary();Calls("defaults","hook never overwrites explicit-listener mode");
  p.SetListener(null);PlayerEngine.Clear();p.PlayRaw(456,pos);
  Calls("reset,gain,post","return to default route repairs before preview post");

  p=Fresh();PlayerEngine.PostResult=0;Check(!p.Play(entry,pos,.4f),"failed post reported false");
  Calls("register,post","failed post cannot apply output gain");
  p=Fresh();Check(!p.Play(null,pos,.4f),"invalid entry rejected");Calls("","invalid entry creates no emitter");
  Check(PlayerEngine.AllMutationsLocked,"Post, default, reset and gain submissions share the same gate");
  var hook=typeof(DefaultListenerHook);var flags=BindingFlags.NonPublic|BindingFlags.Static;
  var target=(MethodBase)hook.GetMethod("TargetMethod",flags).Invoke(null,null);
  Check(target.DeclaringType==typeof(AkSoundEngine) && target.Name=="SetDefaultListeners", "hook resolves exact high-level default setter");
  object[] state={false};hook.GetMethod("Prefix",flags).Invoke(null,state);
  Check(System.Threading.Monitor.IsEntered(ListenerRouting.Gate),"prefix owns queue-order gate");
  var failure=new Exception("fixture native setter exception");
  var returned=hook.GetMethod("Finalizer",flags).Invoke(null,new object[]{failure,state[0]});
  Check(object.ReferenceEquals(returned,failure),"finalizer preserves underlying setter exception");
  Check(!System.Threading.Monitor.IsEntered(ListenerRouting.Gate),"exception finalizer releases queue-order gate");  bool workerAcquired=false;
  var worker=new System.Threading.Thread(() => {
   bool entered=System.Threading.Monitor.TryEnter(ListenerRouting.Gate,1000);
   try {workerAcquired=entered;} finally {if(entered)System.Threading.Monitor.Exit(ListenerRouting.Gate);}
  });
  worker.Start();Check(worker.Join(2000) && workerAcquired,"another thread can submit after exception finalizer releases gate");
  PlayerEngine.Clear();DefaultBoundary();Calls("defaults","default setter remains usable after exception");

  p=Fresh();p.Play(entry,pos,.35f);PlayerEngine.Clear();ListenerRouting.Shutdown();
  var route=(RouteBindingState)typeof(SoundBankPlayer).GetField("_route",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
  Check(!route.IsOwned && !route.NeedsRepair && route.OutputGain==1f,"shutdown releases object ownership and cached route state");
  Check(!p.Play(entry,pos,.35f) && p.PlayRaw(456,pos)==0,"shutdown refuses replacement and original preview posts");
  Calls("","shutdown playback does not touch native APIs");
  DefaultBoundary();Calls("defaults","shutdown preserves game default setter but suppresses owned route writes");
  PlayerEngine.Clear();var late=new SoundBankPlayer();
  Check(!late.Play(entry,pos,.4f) && late.PlayRaw(456,pos)==0,"late player created after shutdown also refuses playback");
  Calls("","late player never creates or registers a native emitter after shutdown");  return count;
 }
}
