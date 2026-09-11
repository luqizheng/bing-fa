using System;

namespace ChinaBettle.Foundation.Map
{
    /// <summary>
    /// 战场平面坐标（米，XZ 平面）。纯值类型、无 Unity 依赖——
    /// 仿真层用它与 Unity 表现层解耦（表现层只在自己的边界处转 Vector3）。
    /// 注意：Unity 6 的脚本语言级别是 C# 9，不能用 record struct（C# 10），故手写等价结构。
    /// </summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public MapPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }

        public float Z { get; }

        public static MapPoint Zero => new MapPoint(0f, 0f);

        public static MapPoint operator +(MapPoint a, MapPoint b) => new MapPoint(a.X + b.X, a.Z + b.Z);

        public static MapPoint operator -(MapPoint a, MapPoint b) => new MapPoint(a.X - b.X, a.Z - b.Z);

        public static MapPoint operator *(MapPoint a, float scale) => new MapPoint(a.X * scale, a.Z * scale);

        public float SqrMagnitude => X * X + Z * Z;

        public float Magnitude => MathF.Sqrt(SqrMagnitude);

        public float DistanceTo(MapPoint other) => (this - other).Magnitude;

        /// <summary>朝目标推进至多 distance 米；distance 足以抵达时精确落在目标上（不来回抖动）。</summary>
        public MapPoint MoveTowards(MapPoint target, float distance)
        {
            var delta = target - this;
            float length = delta.Magnitude;
            if (length <= distance || length <= 1e-4f)
            {
                return target;
            }

            return this + delta * (distance / length);
        }

        /// <summary>战场边界夹取（地图外不可行走）。</summary>
        public MapPoint ClampTo(float minX, float maxX, float minZ, float maxZ) =>
            new MapPoint(Math.Clamp(X, minX, maxX), Math.Clamp(Z, minZ, maxZ));

        public bool Equals(MapPoint other) => X.Equals(other.X) && Z.Equals(other.Z);

        public override bool Equals(object? obj) => obj is MapPoint other && Equals(other);

        public override int GetHashCode() => (X, Z).GetHashCode();

        public static bool operator ==(MapPoint left, MapPoint right) => left.Equals(right);

        public static bool operator !=(MapPoint left, MapPoint right) => !left.Equals(right);

        public override string ToString() => $"({X:0.#}, {Z:0.#})";
    }

}