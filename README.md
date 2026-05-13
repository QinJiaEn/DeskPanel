# DeskPanel

一个 Windows 桌面快捷收纳面板，按 `Alt+`` 随时呼出，把文件拖进去即可归类管理。

> 基于 .NET 10 WPF，支持 Windows 10/11 的 Mica 材质效果。

## ✨ 功能

- **快捷键呼出** — `Alt+`` 弹出/隐藏面板，Esc 快速关闭
- **拖拽收纳** — 从桌面/资源管理器拖入文件，自动复制到分类目录
- **分类管理** — 自定义分类名称和颜色，右键分类可编辑/删除
- **一键收集桌面** — 自动扫描桌面所有文件并归入分类
- **归还桌面** — 将已收纳的文件一键归还到桌面
- **AI 智能分类** — 调用 OpenAI 兼容接口分析文件名，自动推荐分类（彩蛋开启）
- **文件操作** — 右键菜单打开文件、复制路径、重命名、移动分类、删除
- **实时搜索** — 输入关键字即时过滤文件列表
- **真实图标** — 调用 Windows Shell API 显示文件关联图标（非扩展名猜测）
- **毛玻璃界面** — 使用 Mica 系统背景材质，圆角 + 阴影
- **背景图片自定义** — 设置中选择图片作为面板背景
- **系统托盘驻留** — 最小化到托盘，左键托盘图标也可呼出
- **边缘吸附隐藏** — 窗口拖到屏幕边缘自动收起，鼠标移到边缘可反复弹出/隐藏

## 🛠️ 技术栈

| 项目 | 说明 |
|------|------|
| .NET 10 | 目标框架 `net10.0-windows` |
| WPF | Windows Presentation Foundation |
| System.Drawing.Common | 图标提取 |
| Win32 P/Invoke | Shell 图标、DWM 材质、全局热键 |

## 📦 项目结构

```
DeskPanel/
├── App.xaml / App.xaml.cs        # 应用入口：单实例、热键、托盘
├── MainWindow.xaml               # 主窗口布局（XAML）
├── MainWindow.xaml.cs            # 主窗口逻辑（UI 渲染、拖放、对话框）
├── Controls/                     # 自定义控件
├── Converters/                   # WPF 值转换器
├── Models/                       # 数据模型（Category, FileEntry）
├── ViewModels/                   # MVVM ViewModel
├── Services/                     # 业务服务（数据持久化、文件操作、热键）
├── Native/                       # Win32 P/Invoke 声明
├── Resources/                    # 样式资源
└── data.json                     # 分类与文件索引数据（不上传 Git）
```

## 🔧 构建运行

### 前提

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 版本 2004+ 或 Windows 11

### 克隆 & 构建

```bash
git clone https://github.com/QinJiaEn/DeskPanel.git
cd DeskPanel
dotnet build
```

### 运行

```bash
dotnet run
```

首次运行会在 `F:\DeskPanel\files\` 创建文件存储目录（可在 `App.xaml.cs` 中修改路径）。

## ⚙️ 快捷键

| 快捷键 | 操作 |
|--------|------|
| `Alt+`` ` | 呼出/隐藏面板 |
| `Esc` | 隐藏面板 |

### 其实最开始想在github上找一个直接用来着,但是忘了之前用的那个叫啥了,就让ai写了一个😅

## 📄 License

MIT License

---

Made with ❤️ by [QinJiaEn](https://github.com/QinJiaEn)
