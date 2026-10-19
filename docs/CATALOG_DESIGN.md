# 音效目录

运行时目录由UI/SoundCatalogData.generated.cs内嵌，包含从游戏事件数据提取的事件ID、名称和分类。SoundCatalog构建索引，支持名称和ID搜索。界面按武器、怪物、门与设施、UI、环境、玩家、对白、道具、音乐及其他分类展示。

## 数据生成

生成工具为tools/gen-catalog.ps1。TxtpDir参数指向wwiser导出的txtp目录，OutFile指定输出源码。游戏数据和生成过程中的中间文件不随仓库分发。

工具从主CAkEvent记录读取事件ID，并提取同层hashname和objpath。ID按uint保存；遇到只有媒体Source而没有主事件的文件时不生成条目。重复ID合并后按ID排序，确保生成结果稳定。

分类根据Wwise对象路径的前缀匹配；无法匹配的事件放入其他分类。分类表位于生成脚本中，修改规则后需要重新生成并检查差异。

## 校验

目录测试检查关键事件、分类、名称和ID搜索。ReferenceJson可用于与外部参考数据比较；它是可选参数，正常构建使用已提交的生成源码，不要求再次解包游戏。

生成目录用于查找游戏原事件。分配替换时仍需输入自定义SoundBank中的事件ID，两者用途不同。
