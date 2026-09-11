using ChinaBettle.Foundation.Stratagems;

namespace ChinaBettle.Intel
{
    /// <summary>
    /// 欺骗痕迹生命周期（情报规格 §4.2；真源 DECP-02/03/06/08）。
    /// 施计窗口（90 战场秒）内生成假情报对象；窗口结束后假炊烟痕迹存续 10 战场分钟，
    /// 对进入区域的【远观新采样】持续生效；近距 50 米清点任何阶段都看到真值。
    /// 存续期内同源重复释放不延长、不重置计时（DECP-08，防无缝永续）。
    /// 已被斥候带回的情报条目不随痕迹消散而纠正（DECP-06②）。
    /// </summary>
    public sealed class DeceptionTrace
    {
        private readonly StratagemDefinition definition;

        public DeceptionTrace(StratagemDefinition definition, float castAtSeconds, string areaId, string topic)
        {
            this.definition = definition;
            CastAtSeconds = castAtSeconds;
            AreaId = areaId;
            Topic = topic;
        }

        public string AreaId { get; }

        /// <summary>该痕迹所属的技能定义（篡改系数/CD/窗口等配置数据来源，DECP-01）。</summary>
        public StratagemDefinition Definition => definition;

        public string Topic { get; }

        public float CastAtSeconds { get; }

        /// <summary>施计窗口结束时刻。</summary>
        public float CastWindowEndSeconds => CastAtSeconds + definition.CastWindowSeconds;

        /// <summary>痕迹最终消散时刻。</summary>
        public float TraceEndSeconds => CastWindowEndSeconds + definition.TraceDurationSeconds;

        /// <summary>痕迹是否仍在目标区域存续（含施计窗口本身）。</summary>
        public bool IsActiveAt(float nowSeconds) => nowSeconds >= CastAtSeconds && nowSeconds < TraceEndSeconds;

        /// <summary>
        /// 对远观采样应用载荷篡改；近距清点（识破路径①）不被篡改，返回原值。
        /// </summary>
        public float ApplyToFarObservation(float trueValue, float observerDistanceMeters, float nowSeconds)
        {
            var c = DeceptionDetectionConfig.Default;
            if (!IsActiveAt(nowSeconds) || observerDistanceMeters <= c.ProximityMeters)
            {
                return trueValue;
            }

            return trueValue * definition.TroopEstimateMultiplier;
        }

        /// <summary>DECP-08：同源痕迹仍存续时拒绝刷新。返回是否接受新释放。</summary>
        public bool AcceptRecast(float nowSeconds) => !IsActiveAt(nowSeconds);
    }

}