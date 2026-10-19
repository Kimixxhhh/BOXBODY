LOW-POLY WEAPON STUDY / 05
棒球棍、斧头、闪光弹、手雷、沙漠之鹰、M1911

仅包含本次六件新模型。根据用户提供的图片原创建模，使用棱面几何与少量纯色材质，不依赖外部模型或贴图。

模型
  models/baseball_bat.glb  棒球棍       212 三角面 / 2 色
  models/axe.glb           斧头         188 三角面 / 4 色
  models/flashbang.glb     闪光弹       428 三角面 / 4 色
  models/grenade.glb       手雷         372 三角面 / 4 色
  models/deagle.glb        沙漠之鹰     556 三角面 / 3 色
  models/m1911.glb         M1911        644 三角面 / 4 色

查看与编辑
  双击 preview.html，可离线旋转、缩放、侧视、俯视、查看线框。
  数字 1–6 切换模型，双击画面复位。每件模型有自己的 GLB 下载入口。
  previews/collection.png 为总览；同名 PNG 为三维、侧面和俯视展示。
  GLB 可直接导入 Blender 或 Godot。已通过 Godot 4.7.1 实际导入检查。
  当前均为静态外观模型，无骨骼、动画和碰撞体。
  部件分开命名。闪光弹侧面的黑色六边形是外观装饰，没有制作真实穿孔。

坐标与挂点
  单位：米，游戏造型比例。Y 向上。
  手枪枪口朝 -Z；棒球棍、斧头从握柄向前沿 -Z 延伸；投掷物沿 Y 竖直。
  全部模型原点位于 GripSocket。
  手枪包含 MuzzleSocket、GripSocket、SupportSocket。
  近战武器包含 HitSocket、GripSocket、SupportSocket。
  投掷物包含 ThrowSocket、GripSocket。
  各部件共用握持原点；在 Blender 中制作独立转动动画前，可重新设置部件轴心。

项目中的副本
  assets/weapons/melee/       baseball_bat.glb、axe.glb
  assets/weapons/throwables/  flashbang.glb、grenade.glb
  assets/weapons/pistol/      deagle.glb、m1911.glb

可编辑来源
  source/build.py 是原创几何定义；model_tools.py、detail_helpers.py 为几何工具。
  source/mesh_data.json 保存全部网格；viewer_template.html 为离线预览模板。
  source/validate_glb.py 检查数组、法线、绕序、坐标及项目副本。
  source/validation/check.gd 执行 Godot 导入验证。
  Python 3 安装 numpy、Pillow 后运行 source/build.py 可以重新生成。
  原项目内运行会同步写入上述 assets 文件夹；独立解压后运行则只更新本包。
  渲染图使用 Windows Bahnschrift / 微软雅黑字体。
