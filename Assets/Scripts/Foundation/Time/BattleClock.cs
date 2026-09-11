
namespace ChinaBettle.Foundation.Time
{
    /// <summary>
    /// 战役层时钟（真源 TIME-02）。游戏 1 秒 = 现实 1 秒；暂停冻结全部战役计时
    /// （情报年龄、技能持续/CD、断粮计时，见 GDD §2.1 时钟冻结规则）。
    /// 本类型为纯逻辑、不依赖 UnityEngine，供权威侧与（未来）服务端复用。
    /// </summary>
    public sealed class BattleClock
    {
        private float elapsedSeconds;
        private bool paused;

        public float ElapsedSeconds => elapsedSeconds;

        public float ElapsedMinutes => elapsedSeconds / 60f;

        public bool IsPaused => paused;

        public void Advance(float deltaRealSeconds)
        {
            if (deltaRealSeconds < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(deltaRealSeconds), "delta must be non-negative");
            }

            if (!paused)
            {
                elapsedSeconds += deltaRealSeconds;
            }
        }

        public void Pause() => paused = true;

        public void Resume() => paused = false;
    }

}