# ScpSlDamageDisplay

基于 EXILED 与 HintServiceMeow 的 SCP: Secret Laboratory 击杀及伤害显示插件。

## 默认行为

- 命中敌人时，在屏幕中间 `X = 0`、`Y = 550` 显示白色、加粗、20 号累计伤害数字。
- 同时命中多个敌人时，每个目标占一行；再次命中同一目标会把新伤害累加到原行。
- 命中 SCP 休谟护盾时以浅蓝色独立累计；开始损失真实生命后，同一行切换回白色并从真实伤害重新累计。若护盾恢复，则下一轮护盾和真实伤害会再次按阶段切换与重新计数。
- 目标死亡时，击杀者对应行变为浅红色击杀统计；30 秒内命中过目标的其他玩家对应行变为浅红色助攻统计。
- 伤害贡献百分比以本次死亡结算时的有效贡献者伤害总和为分母。
- 普通伤害默认显示 3 秒，击杀/助攻结果默认显示 6 秒，均可在 EXILED 配置中调整。

## 构建

项目默认引用本机的 SCP:SL 服务端、EXILED 9.14.2 和 HintServiceMeow 6.0.0 程序集。

```powershell
dotnet build .\ScpSlDamageDisplay.csproj -c Release
```

输出文件为 `bin\Release\ScpSlDamageDisplay.dll`。
