BLOCK / MOTION — 僵尸行走与攻击动画

基于 assets/characters/zombies 中当前经 Blender 修改的四个文件制作。
原有网格、材质、节点名称、父子关系和静止姿态保持不变。
动画已嵌入各自 GLB，直接导入 Godot 或 Blender 即可查看和编辑。

模型与动作
  zombie_normal.glb  普通僵尸：缓慢踉跄行走 / 右手挥击
  zombie_helmet.glb  工人僵尸：稳步行走 / 双手前推
  zombie_fast.glb    狂奔僵尸：快速步伐 / 左手抓击
  zombie_strong.glb  壮硕僵尸：沉重步伐 / 双臂举起后重砸

每个文件包含两个动画片段
  walk_loop  原地行走，首尾衔接，Godot 自动识别为循环动画。
  attack     单次攻击，包含蓄力、挥击、收势和复位。

时长与击中时刻（秒）
  文件               行走周期   攻击时长   击中时刻
  zombie_normal       1.40       1.00       0.43
  zombie_helmet       1.25       1.10       0.48
  zombie_fast         0.70       0.64       0.25
  zombie_strong       1.65       1.45       0.70
  击中时刻是动作设计的参考时间，可用于后续伤害判定；未添加游戏伤害逻辑。

使用
  项目原目录中的四个 GLB 已更新。
  Godot 导入后会出现 AnimationPlayer，播放 walk_loop 或 attack 即可。
  行走是原地循环，游戏中的实际移动由角色控制器负责。
  可以使用 0.10–0.15 秒的动画混合时间衔接行走与攻击。
  动画沿用角色分件节点，直接驱动躯干、头、上臂、前臂和鞋子，没有额外增加骨骼。
  工人的帽子继承头部动作。
  坐标仍为 Y 向上、-Z 为面朝方向，根节点没有位移轨迹。

预览
  双击 preview.html 可离线播放、暂停、拖动时间轴、调整速度、切换角色和视角。
  攻击默认播放一次；勾选“连续预览”只控制页面上的重播，不改变 GLB 动画循环设置。
  previews/*_walk.gif 为行走动图，*_attack.gif 为重复展示的攻击动图。
  previews/attack_poses.png 展示四款攻击的关键姿态。

原文件备份
  本项目 CharacterStudy/Zombies_Animated/backups/ 下保留了添加动画前的四个 GLB。
  下载包仅包含带动画模型，原文件备份保留在工作目录中。

验证
  已检查原始二进制网格、材质、节点与静止变换均未被改动。
  已检查关键帧数值、旋转单位长度、行走首尾、攻击复位和鞋子落地。
  已在 Godot 4.7.1 实际导入并逐帧播放，两片段均为 11 条有效动画轨道。
  已验证从行走中切换到攻击后，部件会复位到攻击起始姿态。
  循环命名参考官方约定：
  https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/node_type_customization.html#animation-loop

可编辑来源
  source/animate.py：动画曲线生成和 GLB 动画追加；从 backups 读取原文件。
  source/validate.py：资产保留与动画检查。
  source/validation/check.gd：Godot 导入、播放和衔接检查。
  source/render_previews.py：从 GLB 动画生成动图与关键帧预览。
  source/viewer_template.html、preview_data.json：离线播放器与模型数据。
  Python 3 + numpy + Pillow；预览字体使用 Windows 微软雅黑。
