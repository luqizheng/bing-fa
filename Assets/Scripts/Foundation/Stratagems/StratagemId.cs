namespace ChinaBettle.Foundation.Stratagems;

/// <summary>
/// 首发 12 计技能标识（真源 SKILL-12）。
/// 切片只允许实现 ReduceStove/AddStove/FireAttack（SKILL-10）；
/// 标 Draft 的 5 计为机制草案，SKILL-13 禁止进实现。
/// </summary>
public enum StratagemId
{
    /// <summary>减灶示弱（赵·阵营计，切片）。</summary>
    ReduceStove,

    /// <summary>增灶示强（赵·阵营计，切片）。</summary>
    AddStove,

    /// <summary>火攻粮道（通用池，切片；浓烟产 skill_reveal 真实情报）。</summary>
    FireAttack,

    /// <summary>烽火疑兵（秦·白起阵营计，二期）。</summary>
    BeaconDecoy,

    /// <summary>暗度陈仓（通用池，二期；COMBAT-07 唯一临时穿山手段）。</summary>
    SecretMarch,

    /// <summary>焚粮断道（赵·赵王亲率，全局特殊不占 4 槽，每场 1 次，二期）。</summary>
    BurnGranary,

    /// <summary>合纵连横（苏代·幕僚计，唯一战略层 CD=1 旬，二期）。</summary>
    VerticalAlliance,

    /// <summary>反间计（通用池，二期）——机制草案（盲释放候选），SKILL-13 禁止实现。</summary>
    [System.Obsolete("机制草案未定案，SKILL-13：禁止进实现")]
    Counterspy,

    /// <summary>声东击西（通用池，二期草案）。</summary>
    [System.Obsolete("机制草案未定案，SKILL-13：禁止进实现")]
    FeintEast,

    /// <summary>断粮道（通用池，二期草案）。</summary>
    [System.Obsolete("机制草案未定案，SKILL-13：禁止进实现")]
    CutSupply,

    /// <summary>伪传军令（通用池，二期草案）。</summary>
    [System.Obsolete("机制草案未定案，SKILL-13：禁止进实现")]
    FalseOrders,

    /// <summary>坚壁清野（廉颇·幕僚计，二期草案）。</summary>
    [System.Obsolete("机制草案未定案，SKILL-13：禁止进实现")]
    ScorchedDefense,
}

/// <summary>技能槽位（真源 SKILL-08：4 槽 = 1 阵营 + 2 通用 + 1 幕僚；开战锁定）。</summary>
public enum StratagemSlot
{
    Faction,
    Universal1,
    Universal2,
    Advisor,
}

/// <summary>技能大类（GDD §2.3）。</summary>
public enum StratagemCategory
{
    /// <summary>欺骗类：向情报系统注入假情报对象（篡改载荷，不扣可信度）。</summary>
    Deception,

    /// <summary>突袭类：改变战场物理现实，痕迹本身为真实信源。</summary>
    Raid,

    /// <summary>外交/防御类：绑定幕僚在场。</summary>
    Diplomacy,
}
