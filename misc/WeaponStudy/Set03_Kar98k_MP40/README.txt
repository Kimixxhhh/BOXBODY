LOW-POLY WEAPON STUDY / 03
Kar98k + MP40

本包仅包含本次新增的两款模型，按提供的侧视参考图原创建模。
沿用之前的 lowpoly 风格，使用纯色材质和平面法线，不依赖外部贴图。

模型
  models/kar98k.glb  — 808 个三角面 / 4 色
  models/mp40.glb    — 1112 个三角面 / 3 色

预览
  双击 preview.html：离线旋转、滚轮缩放、侧视、俯视、线框。
  数字 1、2 切换模型；双击画面复位。
  previews/collection.png：本次两款模型总览。
  previews/kar98k.png、previews/mp40.png：各自的三维和侧面展示。

使用与编辑
  将 GLB 导入 Blender 即可编辑，或复制到 Godot 项目中导入。
  已通过 Godot 4.7.1 的网格数量、三角面数量、法线、材质与挂点检查。
  两款均为静态外观资产，未添加动画、骨骼或碰撞体。
  Kar98k 保留长木托、弯柄枪栓、机械瞄具。
  MP40 采用参考图中的折叠枪托姿态；枪口朝向已统一。
  枪栓、弹匣、枪托等部件分别命名，可在 Blender 中单独选择。
  所有网格共用握把原点；制作部件旋转动画前，需设置对应的局部轴心。

坐标与挂点
  单位：米（游戏造型比例，不用于精密尺寸复刻）。
  Y 向上，枪口朝 -Z，原点位于主握把。
  每件含 MuzzleSocket、GripSocket、SupportSocket。

调色板（sRGB）
  炭黑 #283238
  钢灰 #626e70
  木棕 #ad7046
  深棕 #694b37
  不同棱面的明暗由光照产生。

可编辑来源
  source/build.py：两款模型的参数化几何。
  source/model_tools.py：通用几何、GLB 导出与预览渲染工具。
  source/mesh_data.json：本次两款模型的网格数据。
  source/viewer_template.html：离线预览模板。
  source/validation/check.gd：Godot 导入校验。
  Python 3 安装 numpy、Pillow 后运行 source/build.py 可重新生成。
  渲染图字体默认使用 Windows 的 Bahnschrift 和微软雅黑。
