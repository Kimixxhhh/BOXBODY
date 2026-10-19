LOW-POLY WEAPON STUDY / 01

根据提供的侧视参考图从零建立的三件游戏用 lowpoly 武器模型。
所有模型采用纯色材质和平面法线，整套共用 5 种基础色，无外部贴图依赖。

查看效果
  双击 preview.html，可离线旋转、缩放、切换侧视/俯视、显示线框。
  previews/collection.png：三件模型总览。
  previews/ak47.png、m870.png、rpg7.png：各模型的三维及正侧面展示图。

交付模型
  models/ak47.glb  — 564 个三角面 / 4 色
  models/m870.glb  — 584 个三角面 / 4 色
  models/rpg7.glb  — 736 个三角面 / 5 色

Godot 导入
  将 models 中的 GLB 复制到 Godot 项目文件夹，等待导入后拖入 3D 场景即可。
  已用本机 Godot 4.7.1 验证三件 GLB 均可导入，网格、法线、三角面数及挂点检查通过。
  文件使用米为单位，Y 向上，枪口朝 -Z，原点位于主握把。
  每件含 MuzzleSocket、GripSocket、SupportSocket；RPG 另含 ProjectileSocket。
  零件分别命名，便于后续编辑；当前是静态外观模型，不含动画、骨骼或碰撞体。
  所有部件的原点共用握把原点，若需部件旋转动画，应先设置对应部件的局部轴心。

五色调色板（sRGB）
  炭黑 #283238
  钢灰 #626e70
  木棕 #ad7046
  深棕 #694b37
  橄榄绿 #737953
  展示图中的深浅变化来自光照，而不是额外材质色或照片贴图。

可编辑来源
  source/build_models.py：参数化模型、标准 GLB 导出、展示图生成。
  source/mesh_data.json：原始网格数据。
  source/viewer_template.html：离线交互预览模板。
  source/package_preview.py：将网格数据打包进单文件预览。
  source/validation/check.gd：Godot 导入检查。
  使用 Python 3、numpy、Pillow 运行 build_models.py，再运行 package_preview.py 可重新生成。
  展示图字体使用 Windows 的 Bahnschrift 和微软雅黑，其他系统可在 font() 中替换。

本组用于观察造型与风格，细节按游戏可读性做了概括；不是精密尺寸复刻。
