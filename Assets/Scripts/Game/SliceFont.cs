using System;
using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 字体解析：优先使用系统中文动态字体（内置 LegacyRuntime/Arial 不含 CJK 字形，
    /// 直接使用会把中文渲染成方框）。取不到时回退内置字体，界面仍可运行（只是中文可能缺字）。
    /// </summary>
    public static class SliceFont
    {
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "PingFang SC",
            "Heiti SC",
            "Arial Unicode MS",
        };

        private static Font? cached;

        public static Font Resolve()
        {
            if (cached != null)
            {
                return cached;
            }

            try
            {
                var installed = Font.GetOSInstalledFontNames();
                foreach (var preferred in PreferredFonts)
                {
                    foreach (var name in installed)
                    {
                        if (string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase) ||
                            name.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            cached = Font.CreateDynamicFontFromOSFont(name, 22);
                            if (cached != null)
                            {
                                return cached;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 取系统字体失败不致命，走内置字体回退。
            }

            cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cached;
        }
    }

}