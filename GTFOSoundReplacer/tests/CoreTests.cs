using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GTFOSoundReplacer.Audio;
using GTFOSoundReplacer.Presets;
using GTFOSoundReplacer.UI;
using GTFOSoundReplacer.Catalog;

public static class CoreTests
{
    private static int count;
    private static void Check(bool pass,string label) {if(!pass)throw new Exception("FAIL: "+label);count++;}
    private static int Size<T>(IEnumerable<T> source) {int n=0;foreach(var v in source)n++;return n;}
    private static string Json(string marker="") {return "{\"name\":\"测试预设\",\"entries\":[{\"eventId\":2182337294,\"enabled\":true,\"volume\":0.35,\"muteOriginal\":false,\"bankEventId\":3883390140"+marker+"}]}";}
    private static void Reject(string json,string label) {
        bool rejected=false;try {PresetCodec.Deserialize(json);} catch(JsonException) {rejected=true;}
        Check(rejected,label);
    }
    public static int Run()
    {
        count=0;var map=new ReplacementMap();
        var entry=new ReplacementEntry {BankEventId=3883390140,Volume=.35f};
        map.Set(2182337294,entry);
        Check(map.Count==1 && map.Contains(2182337294),"map set creates one keyed replacement");
        Check(entry.EventId==2182337294,"map canonicalizes event ID to its key");
        Check(map.TryGet(2182337294,out var got) && object.ReferenceEquals(got,entry),"map retains selected entry");
        map.Set(2182337294,null);Check(map.Count==1,"null assignment cannot erase a replacement");
        map.Set(2182337294,new ReplacementEntry{Enabled=false});
        Check(map.Count==1 && map.TryGet(2182337294,out got) && !got.Enabled,"duplicate key replaces its value");
        Check(!map.TryGet(123,out _) && !map.Remove(123),"missing keys are harmless");
        Check(map.Remove(2182337294) && map.Count==0,"remove removes existing mapping");
        map.Set(1,new ReplacementEntry());map.Set(2,new ReplacementEntry());map.Clear();
        Check(map.Count==0,"clear empties all mappings");
        Check(ReplacementEntry.ClampVolume(-1)==0 && ReplacementEntry.ClampVolume(3)==2,"clamp covers lower and upper limits");
        Check(ReplacementEntry.ClampVolume(.35f)==.35f,"clamp preserves in-range gain");
        Check(new ReplacementEntry{Volume=1.5f}.EffectiveVolume(2)==2,"combined gain cannot exceed two");
        Check(new ReplacementEntry{Volume=-1}.EffectiveVolume(1)==0,"negative item volume silences");
        Check(new ReplacementEntry{Volume=1}.EffectiveVolume(.5f)==.5f,"global multiplier applied");
        Check(new ReplacementEntry{Volume=1}.EffectiveVolume(-1)==0,"negative global volume silences");
        Check(new ReplacementEntry().Enabled,"entry defaults remain enabled");

        var catalog=new SoundCatalog(new CatalogDocument { Events = new List<EventRecord> {
            new EventRecord { Id=2182337294, Name="melee_charged", Category="Weapon" },
            new EventRecord { Id=493397974, Name="ScoutDeath", Category="Enemy" }
        }});
        Check(catalog.Count==2,"catalog uses supplied event set");
        var ids=new HashSet<uint>();bool unique=true;foreach(var e in catalog.Entries)unique &= e.EventId!=0 && ids.Add(e.EventId);
        Check(unique,"catalog has unique nonzero event IDs");
        Check(Size(catalog.Search("Scout"))>0,"event-name search matches Scout");
        Check(Size(catalog.Search("2182337294"))==1,"numeric search finds charged event");
        Check(Size(catalog.Search("no_such_sound_012345"))==0,"unknown query returns no events");
        Check(Size(catalog.Search("  "))==catalog.Count,"blank query preserves complete catalog");
        Check(catalog.FindName(2182337294)=="melee_charged","reverse lookup finds charged event");
        Check(SoundCatalog.LookupName(0)=="","unknown lookup returns empty name");
        Check(new SoundCatalog().Count==0,"no embedded fallback event table");
        Check(new SoundEntry{EventId=1,Name="name",FriendlyName="友好"}.DisplayName=="友好 (name)","display combines friendly and event names");

        Check(PresetCodec.Deserialize(Json(",\"sourceType\":\"soundbank\"")).Entries.Count==1,"compatible string marker is case insensitive");
        var data=PresetCodec.Deserialize(Json());var text=PresetCodec.Serialize(data);var back=PresetCodec.Deserialize(text);
        Check(!text.Contains("sourceType"),"new JSON has no source discriminator");
        Check(!text.Contains("muteOriginal"),"removed mix flag is not serialized even when legacy JSON supplies false");
        Check(typeof(ReplacementEntry).GetProperty("MuteOriginal") == null,"removed mix property absent from runtime model");
        Check(PresetCodec.Deserialize(Json().Replace("\"muteOriginal\":false", "\"muteOriginal\":true")).Entries.Count == 1,"old true flag also accepted as unknown metadata");
        Check(back.Name=="测试预设" && back.Entries.Count==1,"preset name and count round-trip");
        got=back.Entries[0];
        Check(got.EventId==2182337294 && got.BankEventId==3883390140,"full uint event IDs round-trip");
        Check(Math.Abs(got.Volume-.35f)<.000001 && got.Enabled,"gain and enabled flags round-trip");
        Check(PresetCodec.Deserialize(Json(",\"sourceType\":\"SoundBank\"")).Entries[0].BankEventId==3883390140,"compatible string marker accepted");
        Check(PresetCodec.Deserialize(Json(",\"sourceType\":1")).Entries[0].BankEventId==3883390140,"compatible numeric marker accepted");
        Check(!PresetCodec.Serialize(PresetCodec.Deserialize(Json(",\"sourceType\":1"))).Contains("sourceType"),"compatible numeric marker removed on write");
        Check(!PresetCodec.Serialize(PresetCodec.Deserialize(Json(",\"sourceType\":\"SoundBank\""))).Contains("sourceType"),"compatible string marker removed on write");
        Check(PresetCodec.Deserialize(Json(",\"additionalProperty\":42")).Entries.Count==1,"unrelated extension properties are tolerated");
        Reject(Json(",\"sourceType\":\"Unsupported\""),"unsupported string source rejected");
        Reject(Json(",\"sourceType\":0"),"unsupported numeric source rejected");
        Reject(Json(",\"sourceType\":2"),"unknown future numeric source rejected");
        Reject(Json(",\"sourceType\":null"),"null source marker rejected");
        Reject(Json(",\"sourceType\":true"),"boolean source marker rejected");
        Reject("{", "malformed JSON rejected");
        Reject("[]", "array root rejected");
        Reject("null", "null root rejected");
        Reject("{\"entries\":null}","null entries rejected");
        Reject("{\"entries\":{}}","object entries rejected");
        Reject("{\"entries\":[{\"eventId\":-1}]}","negative uint ID rejected");
        Reject(Json(",\"sourceType\":{}"),"object source marker rejected");
        Reject(Json(",\"sourceType\":[]"),"array source marker rejected");
        Reject(Json(",\"sourceType\":1.5"),"fractional source marker rejected");
        Reject(Json(",\"sourceType\":\"Unsupported\",\"sourceType\":\"SoundBank\""),"duplicate valid marker cannot conceal unsupported marker");
        Reject(Json(",\"sourceType\":\"SoundBank\",\"SOURCETYPE\":\"Unsupported\""),"case variation cannot conceal unsupported marker");
        Reject("{\"name\":\"missing\"}","missing entries rejected");
        Reject("{\"entries\":[] , \"entries\":null}","duplicate entries still validates all array types");
        bool invalidModel=false;try {PresetCodec.Serialize(new PresetData{Entries=null});}catch(JsonException){invalidModel=true;}
        Check(invalidModel,"Serialize rejects null entries rather than writing unusable preset");        return count;
    }
}
