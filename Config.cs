using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ScpSlDamageDisplay
{
    public sealed class Config : IConfig
    {
        [Description("是否启用击杀及伤害显示插件。")]
        public bool IsEnabled { get; set; } = true;

        [Description("是否在服务器控制台输出调试信息。")]
        public bool Debug { get; set; } = false;

        [Description("提示的水平坐标。")]
        public float XCoordinate { get; set; } = 0f;

        [Description("提示的垂直坐标。")]
        public float YCoordinate { get; set; } = 650f;

        [Description("提示文字大小。")]
        public int FontSize { get; set; } = 15;

        [Description("提示颜色，使用十六进制 RGB 或 RGBA。")]
        public string Color { get; set; } = "#FFFFFF";

        [Description("SCP 休谟护盾伤害文字颜色，使用十六进制 RGB 或 RGBA。")]
        public string ShieldColor { get; set; } = "#80D8FF";

        [Description("击杀或助攻结算文字颜色，使用十六进制 RGB 或 RGBA。")]
        public string ResultColor { get; set; } = "#FF7F7F";

        [Description("普通伤害数字保留的秒数。")]
        public float DamageDisplaySeconds { get; set; } = 2f;

        [Description("击杀或助攻结果保留的秒数。")]
        public float ResultDisplaySeconds { get; set; } = 3f;

        [Description("助攻判定窗口（秒）。")]
        public float AssistWindowSeconds { get; set; } = 30f;

        [Description("伤害数字保留的小数位数，允许范围 0-3。")]
        public int DecimalPlaces { get; set; } = 1;
    }
}
