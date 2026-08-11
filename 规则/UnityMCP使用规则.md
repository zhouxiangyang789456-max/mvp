# Unity MCP 使用规则

## 适用范围

本规则约束本项目通过 MCP（Model Context Protocol）操作 Unity 编辑器的方式。MCP for Unity 插件允许 AI 客户端直接控制 Unity 编辑器，执行创建/修改场景对象、组件、材质、Prefab、UI、脚本、资源导入等操作。

## Claude-only 策略（重要）

**本项目只允许为 Claude 配置 Unity MCP，禁止为 ChatGPT、Codex 等其他 AI 模型配置 Unity MCP。**

- Claude 的 MCP 配置位于项目根目录 `D:\prounity\mvp\.mcp.json`，只包含两个服务器：`unity-mcp`（HTTP 8080）和 `blender`（Blender MCP）。
- **禁止**修改或新增 Codex 的 `.codex\config.toml` 来加入 unity-mcp。当前 `.codex\config.toml` 仅包含 Blender MCP，不得为其加入 Unity 相关配置。
- 若其他 AI 工具需要操作 Unity，必须重新说明并评估后再决定，不得自行添加。

## 插件安装情况

- 包名：`com.coplaydev.unity-mcp`（MCP for Unity，v10.1.2）。
- 安装方式：作为**嵌入式包（embedded package）**放入 `D:\prounity\mvp\mvp\Packages\com.coplaydev.unity-mcp\`，并在 `Packages\manifest.json` 中以 `file:com.coplaydev.unity-mcp` 引用。
- 采用嵌入式而非 Git URL 的原因：本机 Unity 的 Git 子进程访问 GitHub 超时（443），而嵌入式包不依赖网络，解析更稳定。
- 更新方式：需要升级时，从 GitHub 仓库 `CoplayDev/unity-mcp` 下载新版本源码替换 `Packages\com.coplaydev.unity-mcp` 目录内容，保持包名和目录结构不变。

## 连接架构

- Unity 编辑器内通过 MCP for Unity 插件与本地 Python 桥接服务建立连接。
- MCP 服务端点：`http://127.0.0.1:8080/mcp`（HTTP transport）。
- Unity 插件（编辑器内）连接桥接服务后，AI 客户端即可通过该端点调用工具。

## 开始前检查

1. 确认 Unity 编辑器已打开且加载了本工程（`D:\prounity\mvp\mvp`）。
2. 确认 MCP for Unity 桥接服务已运行（端口 8080）。可通过访问 `http://127.0.0.1:8080/mcp` 或查看桥接日志确认。
3. 确认当前任务已加载 unity-mcp 工具（如 `manage_gameobject`、`manage_scene`、`create_script` 等）。
4. 通过 `mcpforunity://instances` 资源读取当前已连接的 Unity 实例，确认实例名与版本正确。
5. 如果桥接服务未运行或 Unity 未连接（`instance_count` 为 0），应停止 Unity 相关操作，明确告诉用户启动 Unity 与桥接服务，不得声称已经完成编辑器操作。

## 标准使用流程

1. 明确需求：要创建的场景对象、组件、材质、Prefab、脚本或资源类型，以及目标目录。
2. 先读取 Unity 实例状态和当前场景信息，确认不会覆盖已有对象或破坏既有结构。
3. 使用 MCP 工具按步骤执行：创建对象 → 设置变换/组件 → 配置材质 → 组织层级 → 保存场景。
4. 需要修改脚本时，优先用工具读取脚本当前内容，再应用精确编辑，避免整体覆盖。
5. 每个阶段后读取对象信息或场景层级，确认结果正确。
6. 涉及资源导入时，将源文件放入 `Assets` 下合适目录，并确认导入结果和 `.meta` 生成。
7. 结束后报告创建/修改的对象、组件、脚本、资源路径及仍需人工确认的事项。

调用工具时应以当前任务实际暴露的 MCP 工具名称和参数结构为准，不得猜测不存在的工具或参数。

## 使用规范

- 遵循项目已有的目录结构约定（`Assets/Scripts/{Shared,CommanderSelect,Battle}`、`Assets/Prefabs`、`Assets/Art` 等）。
- 场景、Prefab、脚本、资源使用清晰的英文命名；脚本使用项目约定的命名空间（`Mvp.Shared`、`Mvp.CommanderSelect`、`Mvp.Battle` 等）。
- 不删除或破坏用户已有对象、脚本、Prefab、场景和资源，除非用户明确要求。
- 需要较大改动（如重构、批量修改）时，先在文档或对话中说明方案再执行。
- MCP 工具直接修改场景与资源，具有持久影响；修改前确认目标对象，必要时记录改动以便回退。

## 与其他工具的分工

- Unity MCP 负责 Unity 编辑器内的操作：场景对象、组件、UI、Prefab、脚本、资源导入与项目配置。
- Blender MCP 负责 3D 模型与动画资产制作，遵循《BlenderMCP工作规则.md》。
- 两者互不替代：Unity 内不得手工伪造 3D 资产；Blender 产出必须经正常导入流程进入 Unity。
