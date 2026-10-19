namespace GTFOSoundReplacer.UI
{
    /// <summary>音效分类（对应 GUI 列表分组）。</summary>
    public enum SoundCategory
    {
        Weapon = 0,   // 武器（枪械/近战/可部署武器/命中）
        Enemy = 1,    // 怪物（敌人语音/攻击/脚步/受击/死亡）
        Door = 2,     // 门与设施（门/电梯/反应堆/电池站/消毒站等设施机械）
        UI = 3,       // 界面（HUD/菜单/终端/扫描/谜题/地图）
        Ambience = 4, // 环境（环境音/警报/氛围发射器/感染）
        Player = 5,   // 玩家（脚步/呼吸/受击/动作 foley/植入物）
        Voice = 6,    // 对白（旁白/角色语音 VO）
        Item = 7,     // 道具与工具（消耗品/工具/拾取物/特殊道具/收集录音/弹壳）
        Music = 8,    // 音乐
        Other = 9,    // 其他（开场/预告片/关卡特定/占位）
    }
}
