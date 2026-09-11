// Unity 6 语言/API 兼容垫片（C# 9 init 访问器）。
//
// 背景：本项目语言级别 = C# 9，API 面 = .NET Standard 2.1。
// 但 `init` 访问器（C# 9 特性）在编译期依赖 System.Runtime.CompilerServices.IsExternalInit，
// 而该类型是在 .NET 5 才加入 BCL 的，**不属于 .NET Standard 2.1**，
// 因此 Unity 的 netstandard2.1 profile 不提供它，凡是用 `get; init;` 的程序集都会报
// 285 个 CS0518「预定义类型 IsExternalInit 未定义或未导入」。
//
// 处理：在依赖图最底层的 Foundation 程序集里公开声明一次。
// Foundation 被 AI / Battle / Game / Intel / Stratagems / Time / Units / Tests / Editor 全部引用，
// 故一处声明即可覆盖所有使用 init 的程序集（C# 规范要求该类型对当前编译单元可见）。
//
// 语言级别升到 C# 10+ 或 API 面切到 .NET 5+ 之后，本文件即可删除
// （届时 BCL 自带该类型，重复声明会报 CS0101）。

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C# 9 <c>init</c> 访问器所需的编译期标记类型（.NET Standard 2.1 缺失，按规范补齐）。
    /// </summary>
    public static class IsExternalInit
    {
    }
}
