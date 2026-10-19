BLOCK / MOTORS — 原创 Lowpoly 汽车资产 / 01

本包仅包含本次制作的四款原创汽车，不包含之前的武器模型。

模型
  models/sedan.glb       陶土红四门轿车      1348 三角面 / 4 色
  models/hatchback.glb   鼠尾草绿掀背车      1330 三角面 / 5 色
  models/pickup.glb      沙黄色开放货斗皮卡  1468 三角面 / 5 色
  models/van.glb         灰蓝色高顶厢式货车  1366 三角面 / 5 色

预览
  双击 preview.html 可离线查看。没有网络依赖。
  拖动旋转、滚轮缩放；可切换侧视、俯视、线框和自动旋转。
  previews/collection.png 为四车总览，其余 PNG 为单车细节图。

使用
  格式：glTF 2.0 Binary (.glb)，纯色材质内嵌，无外部贴图。
  单位：米；+Y 向上；车头 -Z；车身地面中心为根节点原点。
  模型为风格化比例，尺寸并非某辆真实汽车的精确复刻。
  manifest.json 的 dimensions 顺序为宽、高、长，包含后视镜和保险杠。
  在 Blender 使用“文件 > 导入 > glTF 2.0”；修改后导出为 glTF Binary。
  在 Godot 中将 .glb 拖入项目后实例化即可。已使用 Godot 4.7.1 实际导入验证。

车轮
  Wheel_Front_Left / Wheel_Front_Right / Wheel_Rear_Left / Wheel_Rear_Right
  四个父节点的原点都位于对应车轴中心，其子网格使用本地坐标。
  车轮绕自身 X 轴滚动；转向可再添加父节点绕 Y 轴旋转。
  Left/Right 按驾驶员面向车头的方向定义。

可编辑结构
  车身、车窗、车灯、保险杠、后视镜、轮胎、轮毂等保留分件。
  每个部件都使用平面法线；可以在 Blender 中按材质或位置修改。
  当前交付为外观模型：窗户是不透明色块，无内饰、骨骼、动画或碰撞体。
  用作静态场景物件时，可按材质合并固定车身部件以减少绘制调用。

源文件
  source/build.py：原创车辆造型与预览生成。
  source/geometry.py：几何、GLB 导出及软件渲染。
  source/viewer_template.html：不依赖第三方库的 WebGL 预览器。
  source/mesh_data.json：可供其他工具读取的原始网格。
  重建需要 Python、NumPy、Pillow 和 Windows 字体：运行 python source/build.py。
  校验：python source/validate_glb.py。
  Godot 校验：godot --headless --path source/validation --script check.gd。

没有使用现成汽车网格、外部贴图或商标。
