namespace GTFOSoundReplacer.UI
{
    /// <summary>
    /// GUI 列表中的一个可替换音效条目。纯数据类（无 Unity 依赖）。
    /// EventId 为规范主键（Wwise event ID），Name 为事件名（可空），
    /// FriendlyName 为内置/curated 友好名（可空）。
    /// </summary>
    public class SoundEntry
    {
        public uint EventId { get; set; }
        public string Name { get; set; }
        public string FriendlyName { get; set; }
        public SoundCategory Category { get; set; }

        /// <summary>用于列表显示的名称。</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(FriendlyName) && !string.IsNullOrEmpty(Name))
                    return FriendlyName + " (" + Name + ")";
                if (!string.IsNullOrEmpty(FriendlyName))
                    return FriendlyName;
                if (!string.IsNullOrEmpty(Name))
                    return Name;
                return EventId.ToString();
            }
        }
    }
}
