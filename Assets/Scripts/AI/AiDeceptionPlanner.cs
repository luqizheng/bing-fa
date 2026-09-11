using System.Collections.Generic;
using ChinaBettle.Foundation.AI;
using ChinaBettle.Foundation.Stratagems;
using ChinaBettle.Intel;
using ChinaBettle.Stratagems;

namespace ChinaBettle.AI;

/// <summary>一次已成立的 AI 欺骗释放（AI-05）。Trace 由调用方交给情报/感知链路消费。</summary>
public sealed record AiDeceptionCast(
    StratagemDefinition Definition,
    DeceptionTrace Trace,
    /// <summary>AI 灶区的真实兵力（篡改前的真值；玩家近距清点看到的就是它）。</summary>
    float TrueTroopCount,
    int SpentPoints)
{
    /// <summary>篡改后载荷：远观新采样看到的数值（×1.5 / ×0.3，DECP-01 配置数据）。</summary>
    public float FabricatedTroopCount => TrueTroopCount * Definition.TroopEstimateMultiplier;
}

/// <summary>
/// AI 欺骗行为（真源 AI-05）：秦军 AI 在切片中释放欺骗技能，与玩家同规则——
/// 同样花 30 谋略点、吃 90s 战场 CD、注入假情报走 DECP 识破路径。
/// ①兵力劣势（ai_strength &lt; 0.7×player_strength）→ 增灶示强（×1.5）；
/// ②评估可设伏诱敌 → 减灶示弱（×0.3）。
/// AI 欺骗**不依赖预养玩家桶**（玩家无信任桶，靠综合可信度+经验自行判断）；
/// 玩家识破后 +15 谋略点由 <c>StrategyPointWallet.AwardDeceptionDetected</c> 承接（SKILL-11 回收渠道）。
/// 施计窗口/痕迹存续/近距真值/同源拒重全由 <c>DeceptionTrace</c> 承载，本类只做触发判定与记账。
/// </summary>
public sealed class AiDeceptionPlanner
{
    private readonly StrategyPointWallet wallet;
    private readonly AiDeceptionConfig config;

    /// <summary>各技能最近一次释放的战役时刻，用于与玩家同规则的战场 CD 校验。</summary>
    private readonly Dictionary<StratagemId, float> lastCastSeconds = new();

    public AiDeceptionPlanner(StrategyPointWallet wallet, AiDeceptionConfig? config = null)
    {
        this.wallet = wallet;
        this.config = config ?? AiDeceptionConfig.Default;
    }

    /// <summary>
    /// 尝试释放欺骗技能。返回 null 表示本次不施计（无条件命中 / CD 中 / 点数不足）。
    /// </summary>
    /// <param name="aiStrength">AI 当前兵力评估值。</param>
    /// <param name="playerStrength">玩家当前兵力评估值（走 AI 情报链路，AI-01）。</param>
    /// <param name="canAmbush">AI 评估"可设伏诱敌"（地形/态势评估，切片由上层行为树给布尔）。</param>
    /// <param name="topic">情报主题（与玩家侧情报池同主题键，如 "秦军_西线兵力"）。</param>
    /// <param name="areaId">施计区域（痕迹作用域）。</param>
    /// <param name="trueTroopCount">AI 真实兵力（只进权威端，永不下发接收方，NET-02）。</param>
    /// <param name="nowSeconds">当前战役时钟（秒）。</param>
    public AiDeceptionCast? TryCast(
        float aiStrength,
        float playerStrength,
        bool canAmbush,
        string topic,
        string areaId,
        float trueTroopCount,
        float nowSeconds)
    {
        var definition = SelectStratagem(aiStrength, playerStrength, canAmbush);
        if (definition is null)
        {
            return null;
        }

        if (lastCastSeconds.TryGetValue(definition.Id, out float last) &&
            nowSeconds - last < definition.BattleCooldownSeconds)
        {
            return null;
        }

        if (!wallet.TrySpend(definition.StrategyPointCost))
        {
            return null;
        }

        lastCastSeconds[definition.Id] = nowSeconds;
        var trace = new DeceptionTrace(definition, nowSeconds, areaId, topic);
        return new AiDeceptionCast(definition, trace, trueTroopCount, definition.StrategyPointCost);
    }

    /// <summary>技能选择（AI-05①②）：劣势优先增灶撑场面，其次可设伏才减灶诱敌。</summary>
    private StratagemDefinition? SelectStratagem(float aiStrength, float playerStrength, bool canAmbush)
    {
        if (aiStrength < config.DisadvantageStrengthRatio * playerStrength)
        {
            return StratagemDefinition.AddStove;
        }

        return canAmbush ? StratagemDefinition.ReduceStove : null;
    }
}
