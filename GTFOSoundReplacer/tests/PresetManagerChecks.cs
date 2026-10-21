using System;
using System.IO;
using System.Linq;
using GTFOSoundReplacer.Audio;
using GTFOSoundReplacer.Presets;

namespace GTFOSoundReplacer {
 public static class DirectoryManager {
  public static string PresetsDirectory;
  public static void EnsureRuntimeDirectories(){Directory.CreateDirectory(PresetsDirectory);}
 }
 public sealed class TestConfigEntry { public string Value; }
 public static class PluginConfig {public static TestConfigEntry DefaultPreset;}
 public sealed class TestLog {public void LogWarning(object value) {}}
 public sealed class Plugin {public static Plugin Instance;public TestLog Log=new TestLog();}
}
public static class PresetManagerChecks
{
 static int count;
 static void Check(bool pass,string label) {if(!pass)throw new Exception("FAIL: "+label);count++;}
 static string Json(string name,string marker="") {return "{\"name\":\""+name+"\",\"entries\":[{\"eventId\":2182337294,\"bankEventId\":3883390140,\"volume\":0.35"+marker+"}]}";}
 static bool Unchanged(ReplacementMap map,ReplacementEntry sentinel) {return map.Count==1 && map.TryGet(99,out var got) && object.ReferenceEquals(got,sentinel);}
 public static int Run()
 {
  count=0;string root=Path.Combine(Path.GetTempPath(),"gtfo-preset-tests-"+Guid.NewGuid().ToString("N"));
  string presets=Path.Combine(root,"presets"),external=Path.Combine(root,"external");Directory.CreateDirectory(presets);Directory.CreateDirectory(external);
  GTFOSoundReplacer.DirectoryManager.PresetsDirectory=presets;
  try {
   var map=new ReplacementMap();var sentinel=new ReplacementEntry{BankEventId=123,Volume=.8f};map.Set(99,sentinel);
   var manager=new PresetManager(map);
   manager.Save("saved");string savedPath=Path.Combine(presets,"saved.json"),saved=File.ReadAllText(savedPath);
   Check(!saved.Contains("sourceType"),"manager Save uses source-free codec");
   map.Clear();Check(manager.Load("saved") && map.Count==1 && map.Contains(99),"saved map loads from real file");
   ReplacementEntry loaded;
   Check(map.TryGet(99,out loaded) && loaded.BankEventId==123 && loaded.Volume==.8f,"saved replacement fields retained");
   map.Clear();map.Set(99,sentinel);
   var invalids=new[]{"{",Json("saved",",\"sourceType\":\"Unsupported\""),Json("saved",",\"sourceType\":0"),"{\"name\":\"saved\",\"entries\":null}"};
   foreach(var invalid in invalids) {
    string bad=Path.Combine(presets,"invalid.json");File.WriteAllText(bad,invalid);
    Check(!manager.Load("invalid") && Unchanged(map,sentinel),"failed Load preserves current map");
    Check(File.ReadAllText(bad)==invalid,"failed Load leaves input file unchanged");
    string imported=Path.Combine(external,"incoming.json");File.WriteAllText(imported,invalid);
    Check(!manager.Import(imported) && Unchanged(map,sentinel),"failed Import preserves current map");
    Check(File.ReadAllText(savedPath)==saved,"failed Import cannot overwrite same-name existing preset");
    Check(File.ReadAllText(imported)==invalid,"failed Import leaves external input unchanged");
   }
   Check(!manager.Load("missing") && Unchanged(map,sentinel),"missing preset preserves map");
   Check(!manager.Import(Path.Combine(external,"missing.json")) && Unchanged(map,sentinel),"missing import preserves map");
   foreach(var marker in new[]{"",",\"sourceType\":\"SoundBank\"",",\"sourceType\":1"}) {
    string incoming=Path.Combine(external,"incoming.json");string json=Json("compatible",marker);File.WriteAllText(incoming,json);
    Check(manager.Import(incoming),"compatible import succeeds");
    Check(manager.LastImportedName=="compatible","import reports saved name rather than source filename");
    Check(!manager.Import(Path.Combine(external,"missing-after-success.json")) && manager.LastImportedName=="","failed import clears old result name");
    Check(manager.Import(((char)34).ToString() + incoming + ((char)34).ToString()) && manager.LastImportedName=="compatible","quoted Windows copy-as-path is accepted");
    Check(Unchanged(map,sentinel),"successful Import saves preset without changing active map");
    Check(!File.ReadAllText(Path.Combine(presets,"compatible.json")).Contains("sourceType"),"imported file is normalized");
    Check(File.ReadAllText(incoming)==json,"successful Import leaves external input unchanged");
   }
   string legacyPath=Path.Combine(presets,"legacy.json");File.WriteAllText(legacyPath,Json("legacy",",\"sourceType\":1"));
   string exported=manager.Export("legacy");
   Check(exported!=legacyPath && Path.IsPathRooted(exported) && !File.ReadAllText(exported).Contains("sourceType"),"Export creates normalized standalone copy");
   Check(File.ReadAllText(legacyPath)==Json("legacy",",\"sourceType\":1"),"Export preserves original compatible file");
   string invalidPath=Path.Combine(presets,"invalid.json"),invalidText=Json("invalid",",\"sourceType\":\"Unsupported\"");File.WriteAllText(invalidPath,invalidText);
   Check(manager.Export("invalid")==null && File.ReadAllText(invalidPath)==invalidText,"failed Export leaves unsupported file intact");
   string invalidExport=Path.Combine(presets,"exports","invalid.json");File.WriteAllText(invalidExport,"preserve previous export");
   Check(manager.Export("invalid")==null && File.ReadAllText(invalidExport)=="preserve previous export","failed Export cannot replace an existing exported copy");
   Check(Unchanged(map,sentinel),"Export never changes active replacement map");   Check(manager.Load("compatible") && map.TryGet(2182337294,out loaded) && loaded.BankEventId==3883390140,"compatible import applies only on explicit Load");
   Check(!map.Contains(99),"successful Load replaces old active map");
   manager.SaveAutosave();Check(!manager.ListPresetNames().Contains("_autosave"),"autosave excluded from visible list");
   map.Clear();GTFOSoundReplacer.PluginConfig.DefaultPreset=null;manager.RestoreState();
   Check(map.Contains(2182337294),"autosave restores saved state");
   File.WriteAllText(Path.Combine(presets,"preferred.json"),"{\"name\":\"preferred\",\"entries\":[{\"eventId\":7,\"bankEventId\":77}]}");
   GTFOSoundReplacer.PluginConfig.DefaultPreset=new GTFOSoundReplacer.TestConfigEntry{Value="preferred"};manager.RestoreState();
   Check(map.Contains(7) && !map.Contains(2182337294),"configured valid default takes precedence over autosave");
   GTFOSoundReplacer.PluginConfig.DefaultPreset.Value="invalid";manager.RestoreState();
   Check(map.Contains(2182337294) && !map.Contains(7),"invalid default preserves existing fallback-to-autosave contract");
   File.WriteAllText(Path.Combine(presets,"_autosave.json"),"{");manager.RestoreState();
   Check(map.Contains(2182337294),"invalid default and autosave leave active map unchanged");   Check(manager.Delete("saved") && !File.Exists(savedPath),"Delete removes only selected preset");
   return count;
  } finally {
   string fullRoot=Path.GetFullPath(root), tempRoot=Path.GetFullPath(Path.GetTempPath());
   if(!fullRoot.StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullRoot).StartsWith("gtfo-preset-tests-",StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test cleanup target");
   Directory.Delete(fullRoot,true);
  }
 }
}
