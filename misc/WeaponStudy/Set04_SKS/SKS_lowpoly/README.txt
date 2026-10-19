LOW-POLY WEAPON STUDY / 04 — SKS

根据本次提供的 SKS 侧视图原创建模，本包仅包含 SKS。
沿用此前的低多边形、纯色材质和平面法线风格。

模型
  models/sks.glb — 1302 个三角面 / 61 个网格零件 / 4 种材质色。
  外形包含长木托、短上护木、固定弹仓、侧向枪机拉柄、机械瞄具和折叠刺刀。
  刺刀为折叠状态；当前是静态外观资产，不含动画、骨骼或碰撞体。

预览
  双击 preview.html 可离线旋转、缩放、切换侧视/俯视和线框。
  双击模型画面复位。
  previews/sks.png：三维与正侧面展示图。
  previews/collection.png：本次模型概览。

导入与编辑
  models/sks.glb 可导入 Blender 编辑，或导入 Godot 使用。
  已通过 Godot 4.7.1 的网格数、三角面数、材质、顶点、法线和挂点检查。
  主要部件已分别命名，便于单独修改、隐藏或删除。
  所有部件共用握把原点；制作部件旋转动画前，需要设置对应的局部轴心。
  单位为米，比例服务于游戏造型，不作精密尺寸复刻。
  Y 轴向上，枪口朝 -Z，原点位于主握把。
  含 MuzzleSocket、GripSocket、SupportSocket 挂点。

四种基础色（sRGB）
  炭黑 #283238
  钢灰 #626e70
  木棕 #ad7046
  深棕 #694b37
  棱面的明暗由光照产生，不依赖外部贴图。

可编辑来源
  source/build.py：SKS 参数化几何。
  source/model_tools.py、source/detail_helpers.py：几何、GLB 导出、预览工具。
  source/mesh_data.json：本次模型网格数据。
  source/viewer_template.html：离线预览模板。
  source/validation/check.gd：Godot 导入验证。
  Python 3 安装 numpy 和 Pillow 后运行 source/build.py 可重新生成。
  展示图默认使用 Windows 的 Bahnschrift 和微软雅黑字体。
