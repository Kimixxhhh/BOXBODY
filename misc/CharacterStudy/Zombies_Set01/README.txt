BLOCK / UNDEAD — COLLECTION 01
四款原创方块僵尸

参照现有角色的方块头、梯形躯干、分段手臂与独立鞋子结构制作。
美术方向参考 Boxhead，网格为从零创建，不包含游戏中提取的资产。
Boxhead 作者发布页：https://www.newgrounds.com/portal/view/430190

模型
  models/zombie_shambler.glb  普通僵尸  291 三角面 / 4 色 / 8 个网格部件
  models/zombie_worker.glb    工人僵尸  297 三角面 / 5 色 / 9 个网格部件
  models/zombie_runner.glb    狂奔僵尸  337 三角面 / 4 色 / 8 个网格部件
  models/zombie_brute.glb     壮硕僵尸  284 三角面 / 4 色 / 8 个网格部件

查看与使用
  双击 preview.html 离线查看，可旋转、缩放、切换正面与俯视、查看线框。
  previews/collection.png 为总览；每款角色还有三维、正面和游戏视角 PNG。
  项目副本已放入 assets/characters/zombies/。
  GLB 可直接导入 Blender 或 Godot，已通过 Godot 4.7.1 实际导入检查。
  模型使用纯色材质，无外部贴图；现有角色文件没有被修改。
  当前是静态模型，没有骨骼、动画和碰撞体。

部件和轴心
  Y 向上、-Z 为面朝方向，模型原点在脚底地面，含 GroundOrigin 空节点。
  body 为躯干，head 为头部；工人另有 hat。
  armLeft / armRight 是上臂；handLeft / handRight 是前臂和手部。
  ShoeLeft / ShoeRight 是独立鞋子。
  上臂轴心在肩部，前臂轴心在肘部，头部轴心在底部。
  前臂从属于上臂；头与上臂从属于躯干，方便后续制作分件动画。
  四款高度约 1.5–1.9 米，壮硕款更宽大；预览卡片分别自动适配大小。

可编辑来源
  source/build.py / mesh_tools.py 为原创几何及 GLB 导出；raster.py 用于预览渲染。
  source/mesh_data.json 保存实际几何；viewer_template.html 为离线预览模板。
  source/validate_glb.py 检查网格、绕序、法线、层级与坐标。
  source/validation/check.gd 执行 Godot 导入验证。
  Python 3 安装 numpy、Pillow 后运行 source/build.py 可重新生成。
  原项目内运行会同步到 assets/characters/zombies；独立解压后运行则只更新本包。
  渲染图使用 Windows Bahnschrift / 微软雅黑字体。
