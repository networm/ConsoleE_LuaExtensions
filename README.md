# ConsoleE Lua Extensions

## 环境

- Windows 7 + Unity 5.6.6f2 + Intellij Idea 2019.2 + xLua
- Windows 10 + Unity 2018.4.28f1 + Intellij Idea 2021.2 + ToLua

逻辑上来说也支持 macOS，不过需要测试一下。

## 使用方法

执行 `Tools/ConsoleE/Create Option` 创建配置文件 `Assets/Editor/ConsoleE_LuaExtensions/ConsoleE_Extensions_Option.asset`。

工具提供三种打开文件方式：

1. Unity 指定的编辑器
2. 扩展名关联的可执行文件
3. 自定义程序路径与打开参数

默认情况下使用 Unity 设置中指定的编辑器打开文件。

默认配置是 Idea 的配置，根据实际的路径修改 Idea 可执行文件路径。

`LuaPathPattern` 需要根据项目实际路径修改。

## 多工程支持

> When you specify the path to a file, IntelliJ IDEA opens it in LightEdit mode, unless it belongs to a project that is already open or there is special logic to automatically open or create a project (for example, in case of Maven or Gradle files).
>
> [Open files from the command line | IntelliJ IDEA](https://www.jetbrains.com/help/idea/opening-files-from-command-line.html)

默认情况下开启多个 Idea 时，会自动在已打开此文件所在工程的 Idea 窗口中打开此文件。

其他编辑器需要看看文档，了解如何设置类似的行为。
