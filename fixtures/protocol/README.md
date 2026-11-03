# M0协议样例（草案）

- empty-frame.json：bootstrap空帧。当前没有单位/资源/报文，数组仅为空的占位结构；实体模型由2/3/5号共同审查后扩展，不把int数组当正式实体协议。
- move-command.json：字段形状示例，不代表unitId=1当前存在或已被引擎接受。bootstrap不处理动作、移动或计分。
- protocolVersion=1是开发草案编号，尚未冻结或发布正式SDK。

1号只提供基础共享数据，5号在SDK首个PR中补跨语言fixture测试；2号确认可见性字段，3号确认资源/报文及失败原因。不得把全图状态直接当选手Observation。
