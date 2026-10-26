FIELD / STREET — 原创 Low-poly 场景道具

models/trash_bin.glb          垃圾桶：带轮、把手、独立翻盖与轮子。
models/chain_link_fence.glb   铁丝网：3 米模块、实体菱形网格、立柱及底座。
models/street_lamp.glb        路灯：弯臂、底座、检修盖、暖色自发光灯罩。

在浏览器中打开 preview.html，可旋转、缩放、查看线框并下载模型，无需联网。
previews/collection.png 为总览；每个模型另有三维和正视预览。

规格
GLB / glTF 2.0，米制，+Y 向上，+Z 正面，原点在地面中心。
纯色 PBR 材质，平面法线，无贴图依赖。按材质合并网格。
垃圾桶 Lid 节点原点在后方铰链，可绕局部 X 轴转动；轮子原点在轮轴。
铁丝网沿 X 轴排列，相邻模块中心间距为 3 米，提供 Snap_Left / Snap_Right。
铁丝网两端均带立柱；相接模块在公共端重合，可按需要移除重复立柱。
路灯带 LightSocket 节点，灯罩有自发光材质；GLB 本身不带照亮场景的灯光。
可在 Godot 中为 LightSocket 添加向下的 SpotLight3D。所有模型均不带碰撞和动画。

项目资源副本位于 assets/environmentObjects/，可直接拖入 Godot 场景。
source/build_models.py 是可复现源文件，依赖 Python 3、numpy、Pillow。
运行脚本会重新生成模型、预览和压缩包；位于此项目内时同步更新上述资源副本。
模型与预览均由实际几何生成，未使用图片冒充三维模型。
