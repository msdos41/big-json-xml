# BigJsonXml

[English](README.md) | 简体中文

一个 Windows 上的查看工具，用来打开和搜索 1 GB 以上的 XML、JSON 文件。

普通文本编辑器打开这么大的文件，往往会变得很卡，甚至内存不够。BigJsonXml 把文件显示成可以逐层展开的表格，内存占用很低，文件还没读完就可以滚动和搜索。

发布包只有一个文件，不需要安装，也不需要另装运行时。下载[最新版本](https://github.com/msdos41/big-json-xml/releases/latest)。

<!-- 有演示 GIF 后放在这里：
<p align="center">
  <img src="docs/media/overview.gif" alt="订单以表格打开，展开其中一个单元格得到另一张表" width="880">
</p>
-->

## 特点

- **大文件也很省内存。** 1 GB 的文件，内存峰值约 47-51 MB。
- **读取时窗口不卡。** 文件还在读取时就可以滚动和搜索。
- **搜索在后台进行。** 在已读完的 1 GB 文件里搜索，不到一秒就能完成，窗口始终可以操作。

## 实测数据

| 文件 | 读取速度 | 内存峰值 | 读完后搜索 |
| --- | --- | --- | --- |
| 1 GB XML，记录列表 | 439 MB/s | 51 MB | 0.4-0.6 秒 |
| 1 GB JSON，记录列表 | 406 MB/s | 50 MB | 0.2-0.3 秒 |
| 1 GB XML，深层嵌套 | 374 MB/s | 47 MB | 0.3-0.4 秒 |
| 512 MB JSON，2.68 亿个标量 | 142-162 MB/s | 71 MB | 0.1 秒 |

以上数据来自引擎基准测试（Release 构建，8 核 Linux 机器，文件已在系统缓存中），不是在桌面窗口里测的。搜索时间包括区分和不区分大小写、有结果和无结果的情况。内存峰值指读取过程中进程占用的最高值。

## 文件如何变成表格

```xml
<orders>
  <order id="1001" status="open">
    <customer>Ada</customer>
    <city>London</city>
  </order>
  <order id="1002" status="shipped">
    <customer>Grace</customer>
    <city>New York</city>
  </order>
</orders>
```

文档打开后显示 `orders`。它只有一个字段 `order`，折叠时显示为 `order[2]`。展开这个单元格，两条订单就是一张表，列按文件内容排列：`@id`、`@status`、`customer`、`city`。

```json
{
  "orders": [
    { "id": 1001, "customer": "Ada", "city": "London" },
    { "id": 1002, "customer": "Grace", "city": "New York" }
  ]
}
```

结构相同。根对象是一张表单，`orders` 显示为 `orders[2]`，展开后得到 `id`、`customer`、`city` 三列。JSON 的值按原样显示：`1001` 没有引号，`"Ada"` 保留引号。如果文件的根是数组，字段名为 `#value`。

扩展名只用于文件选择框的筛选。格式由跳过 BOM 和空白后的第一个字节决定：`<` 是 XML，能作为 JSON 值开头的字节就是 JSON。

每打开一个文档就多一个标签页。展开单元格后，嵌套的表单或表格就画在原位。整个文档只有一个竖向滚动条和一个横向滚动条。折叠时，长内容只显示开头，按列宽截断；展开后完整换行显示，行高随之增加。双击列的右边缘可自动适应列宽，拖动右边缘可手动调整。

## 搜索

按 `Ctrl+F` 或点击 Find 按钮。即使是还没展开的表格，其中的名称和值也会被搜索到。找到结果后会自动展开沿途的单元格，并滚动到该单元格。

**Find what** 是要查找的文字，留空则没有结果。**Field name**、**Parent element name**、**Grandfather element name** 和 **Parent attribute name** 是可选的限定条件，留空表示不限。父属性限定只检查父元素是否有这个属性，不比较属性值。

窗口打开时 **Match case** 默认勾选。**Element name**、**Attribute name** 和 **Value** 决定在哪里匹配，每一项都可以取消。**Start with** 和 **Entire cell** 都不勾选时，文字出现在任何位置都算匹配；勾选 **Start with** 只保留以该文字开头的结果，勾选 **Entire cell** 只保留整个内容等于该文字的结果。

**Find Next** 跳到下一个结果，**Find All** 列出全部结果，`Esc` 清除当前标记。

## 当前版本

0.2.0 版本支持读取和搜索文档。

- 大列表采用紧凑存储，即使文件有几亿个条目，内存也保持在 50-70 MB 左右。
- 搜索在独立线程上运行，不会卡住窗口。
- 不区分大小写的搜索、带空格的文字和中文等非 ASCII 文字，都能借助索引跳着找，不必把整个文件重读一遍。

目前只支持 Windows。程序没有签名，第一次运行时 Windows SmartScreen 可能会提示“Windows 已保护你的电脑”。点击**更多信息**，再点**仍要运行**即可。

JSON 必须是一个 UTF-8 编码的值：对象、数组或标量。注释、末尾逗号和第二个值都不在支持的语法内。遇到不合法的字节会停止读取，此前已生成的表格会保留。

不读取外部 DTD 和外部实体，查看器不会为展开它们而联网。XML 元素之间的缩进和空行不算字段。

## 路线图

- [x] 读取和搜索
- [ ] 编辑和保存
- [ ] Linux 和 macOS

## 构建

构建和测试需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

发布单文件可执行程序：

```powershell
dotnet publish src/BigJsonXml.Desktop/BigJsonXml.Desktop.csproj -c Release
```

生成的文件位于 `src/BigJsonXml.Desktop/bin/Release/net10.0/win-x64/publish/BigJsonXml.exe`。

在仓库根目录运行测试：

```powershell
dotnet test BigJsonXml.slnx
```

桌面窗口使用 Avalonia，读取器和表格逻辑在 `src/BigJsonXml.Core` 中。

## 发布

标签和发布文件的规则见 [docs/releasing.md](docs/releasing.md)。

## 许可证

BigJsonXml 基于 [MIT 许可证](LICENSE) 发布。
