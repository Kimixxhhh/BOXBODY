Lowpoly 子弹 / Field Kit

黄铜色弹壳、铜色弹头、浅金属底火。12 边截面，平面法线，无外部贴图。
这是用于游戏的风格化美术模型，比例经过简化。

已放入工程 assets/weapons/ammo/：
  bullet_ak_triple.glb    三枚完整子弹横放、等距并排，弹尖统一朝 +X。
  bullet_ak.glb          完整子弹，弹壳 / 弹头 / 底火为独立节点。
  bullet_projectile.glb  独立弹头，方便后续飞行动画。
  bullet_casing.glb      弹壳和底火，方便后续抛壳动画。

使用
将 GLB 拖进 Godot 3D 场景即可实例化，材质已内嵌。
单位为米，完整子弹长度 0.06 米；弹尖朝 -Z，原点位于尾部中心。
直立展示时绕 X 轴旋转 +90 度；用于拾取物或俯视展示时可适当放大。
三枚组合模型原点位于底面中心，可直接平放在地面；三枚沿 Z 轴排列。
组合模型共 1554 个三角面，整体尺寸 0.06 × 0.0124 × 0.0444 米。
本次仅添加模型，没有添加射击、弹药或抛壳逻辑。

models/ 内同时保留 GLB 与完整模型的 OBJ + MTL 版本。
source/build.py 可重新生成模型，需要 Python、NumPy。
source/preview.gd 用于在 Godot 中检查导入并渲染展示图。
