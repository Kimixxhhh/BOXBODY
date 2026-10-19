武器装备箱 + 打开的笔记本电脑 / Low-poly 模型

主文件：models/weapon_box_laptop.glb
GLB 已内嵌 1024×1024 PNG 贴图，无需额外查找贴图文件。
项目资源副本：assets/environmentObjects/weapon_box_laptop.glb

备选格式：models/weapon_box_laptop.obj + weapon_box_laptop.mtl
使用 OBJ 时，请保持 models 与 textures 文件夹的相对位置不变。

三角面：4,804
尺寸：约 1.64 × 1.305 × 0.943 米（含提手与打开的电脑）
坐标：Y 轴向上，正面朝 +Z，原点位于地面中心。
屏幕打开角度：105°。

GLB 分为四个节点：
Crate_Body：箱体、加强筋、侧提手和底脚。
Crate_Lid：箱盖、顶部 X 形加强筋、锁扣和后铰链。
Laptop_Base：电脑底座、键盘、触控板、侧接口。
Laptop_Screen：屏幕，局部原点位于铰链处，可独立调整角度。
这是静态模型，不包含开合动画、碰撞体或游戏逻辑。

previews/hero.png：正面三分之四视角。
previews/rear.png：背面三分之四视角。
previews/front.png：正面视角。
预览使用与导出文件一致的几何结构、法线和 UV。

source/build.py：可重复生成模型与贴图的源文件，需要 Python、NumPy、Pillow。
source/raster.py：预览渲染辅助文件。
manifest.json：尺寸、面数及校验结果。

根据用户提供的箱体参考图重新构建低多边形造型；迷彩和屏幕界面为程序绘制。
