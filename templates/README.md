# 新 MOD 工程模板

默认工作流是在完整何小鲁框架中继续开发。只有用户明确要求拆出独立工程时才使用本目录的工程模板；无论是否拆分，正式发布前都必须阅读项目根目录 `RELEASE_GUIDE.md`。

AI 从何小鲁框架派生新项目时，应复制模板并替换全部占位符，而不是复制某个已完成 MOD 的构建脚本。

| 模板 | 生成目标 |
|---|---|
| `build.ps1.template` | 新项目根目录的 `build.ps1` |
| `deploy.ps1.template` | 新项目根目录的 `deploy.ps1` |
| `package.ps1.template` | 新项目根目录的 `package.ps1`，白名单生成发布 ZIP |
| `MOD_README.md.template` | 可选的玩家发布说明；生成到项目根目录 `MOD_README.md` |
| `ModProject.csproj.template` | `<程序集名>.csproj` |
| `local.settings.json.template` | 由 `configure.ps1` 生成的本机配置 |
| `Directory.Build.props.template` | 由 `configure.ps1` 生成的 IDE 本机配置 |

必须替换：

```text
__ASSEMBLY_NAME__   DLL 文件名与程序集名，例如 MyFirstMod
__ROOT_NAMESPACE__ C# 根命名空间，通常与程序集名相同
__DISPLAY_NAME__    构建和部署日志中的中文 MOD 名称
```

如果生成 `MOD_README.md`，还要替换其中的 `__AUTHOR__` 和 `__VERSION__`。打包脚本会拒绝包含未替换占位符的玩家说明。

生成后运行：

```powershell
rg -n "__[A-Z0-9_]+__" . -g "build.ps1" -g "deploy.ps1" -g "package.ps1" -g "*.csproj"
.\configure.ps1 -GameDir "使用者自己的 Probably Stolen 游戏目录"
.\build.ps1
```

第一条命令不应再有任何命中。`package.ps1` 不会打包框架根目录的 `README.md`，只打包 MOD DLL、可选的 `MOD_README.md`（在 ZIP 内命名为 `README.md`）和许可证；未经使用者明确授权，不运行 `deploy.ps1`。

