using System.IO;
using System.Numerics;

using ImGuiNET;

using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Game.ClientState.Objects.Types;

using Ktisis.Util;
using Ktisis.Overlay;
using Ktisis.Structs.Actor;
using Ktisis.Structs.Poses;
using Ktisis.Localization;
using Ktisis.Interop.Hooks;
using Ktisis.Interface.Components;
using Ktisis.Interface.Windows.ActorEdit;
using Ktisis.Data.Files;
using Ktisis.Data.Serialization;
using Ktisis.Interface.Windows.Toolbar;

using static Ktisis.Data.Files.AnamCharaFile;

namespace Ktisis.Interface.Windows.Workspace
{
    public static class Workspace {
		public static bool Visible = false;
		
		

		public static Vector4 ColGreen = new Vector4(0, 255, 0, 255);
		public static Vector4 ColYellow = new Vector4(255, 250, 0, 255);
		public static Vector4 ColRed = new Vector4(255, 0, 0, 255);

		public static TransformTable Transform = new();

		public static FileDialogManager FileDialogManager = new FileDialogManager();

		// Toggle visibility

		public static void Show() => Visible = true;
		public static void Toggle() => Visible = !Visible;
		
		public static void OnEnterGposeToggle(Structs.Actor.State.ActorGposeState gposeState) {
			if (Ktisis.Configuration.OpenKtisisMethod == OpenKtisisMethod.OnEnterGpose)
				Visible = gposeState == Structs.Actor.State.ActorGposeState.ON;
		}

		public static float PanelHeight => ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetStyle().FramePadding.Y;

		// Draw window

		public static void Draw() {
			if (!Visible)
				return;

			var gposeOn = Ktisis.IsInGPose;

			var size = new Vector2(-1, -1);
			ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);

			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));

			if (ImGui.Begin($"Ktisis ({Ktisis.Version})", ref Visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)) {

				ControlButtons.PlaceAndRenderSettings();

				ImGui.BeginGroup();
				ImGui.AlignTextToFramePadding();

				ImGui.TextColored(
					gposeOn ? ColGreen : ColRed,
					gposeOn ? "GPose 已启用" : "GPose 已禁用"
				);

				if (PoseHooks.AnamPosingEnabled) {
					ImGui.TextColored(
						ColYellow,
						"Anamnesis 已启用"	
					);
				}

				ImGui.EndGroup();

				ImGui.SameLine();

				// Pose switch
				ControlButtons.DrawPoseSwitch();

				var target = Ktisis.GPoseTarget;
				if (target == null) return;

				// Selection info
				ImGui.Spacing();
				SelectInfo(target);

				// Actor control

				ImGui.Spacing();
				ImGui.Separator();

				if (ImGui.BeginTabBar(Locale.GetString("Workspace"))) {
					if (ImGui.BeginTabItem(Locale.GetString("Actor")))
						ActorTab(target);
					/*if (ImGui.BeginTabItem(Locale.GetString("Scene")))
						SceneTab();*/
					if (ImGui.BeginTabItem(Locale.GetString("Pose")))
						PoseTab(target);
				}
			}

			ImGui.PopStyleVar();
			ImGui.End();
		}

		// Actor tab (Real)

		private unsafe static void ActorTab(GameObject target) {
			var cfg = Ktisis.Configuration;

			if (target == null) return;

			var actor = (Actor*)target.Address;
			if (actor->Model == null) return;

			// Actor details

			ImGui.Spacing();

			// Customize button
			if (ImGuiComponents.IconButton(FontAwesomeIcon.UserEdit)) {
				if (EditActor.Visible)
					EditActor.Hide();
				else
					EditActor.Show();
			}
			ImGui.SameLine();
			ImGui.Text("编辑对象外观");

			ImGui.Spacing();

			// Actor list
			ActorsList.Draw();

			// Animation control
			AnimationControls.Draw(target);

			// Gaze control
			if (ImGui.CollapsingHeader("目光控制")) {
				if (PoseHooks.PosingEnabled)
					ImGui.TextWrapped("摆拍模式下无法使用目光控制.");
				else
					EditGaze.Draw(actor);
			}

			// Import & Export
			if (ImGui.CollapsingHeader("导入 & 导出"))
				ImportExportChara(actor);

			ImGui.EndTabItem();
		}

		// Pose tab

		public static PoseContainer _TempPose = new();

		private unsafe static void PoseTab(GameObject target) {
			var cfg = Ktisis.Configuration;

			if (target == null) return;

			var actor = (Actor*)target.Address;
			if (actor->Model == null) return;

			// Extra Controls
			ControlButtons.DrawExtra();

			// Parenting

			var parent = cfg.EnableParenting;
			if (ImGui.Checkbox("Parenting", ref parent))
				cfg.EnableParenting = parent;

			// Transform table
			TransformTable(actor);

			ImGui.Spacing();

			// Bone categories
			if (ImGui.CollapsingHeader("骨骼分类")) {

				if (!Categories.DrawToggleList(cfg)) {
					ImGui.Text("未找到骨骼.");
					ImGui.Text("点击显示骨骼 (");
					ImGui.SameLine();
					GuiHelpers.Icon(FontAwesomeIcon.EyeSlash);
					ImGui.SameLine();
					ImGui.Text(") 来填满此处.");
				}
			}

			// Bone tree
			BoneTree.Draw(actor);

			// Import & Export
			if (ImGui.CollapsingHeader("导入 & 导出"))
				ImportExportPose(actor);

			// Advanced
			if (ImGui.CollapsingHeader("高级 (Debug)")) {
				DrawAdvancedDebugOptions(actor);
			}

			ImGui.EndTabItem();
		}
		
		public static unsafe void DrawAdvancedDebugOptions(Actor* actor) {
			if(ImGui.Button("重设当前姿势") && actor->Model != null)
				actor->Model->SyncModelSpace();

			if(ImGui.Button("设置为参考姿势") && actor->Model != null)
				actor->Model->SyncModelSpace(true);

			if(ImGui.Button("存储姿势") && actor->Model != null)
				_TempPose.Store(actor->Model->Skeleton);
			ImGui.SameLine();
			if(ImGui.Button("应用姿势") && actor->Model != null)
				_TempPose.Apply(actor->Model->Skeleton);

			if(ImGui.Button("强制重绘"))
				actor->Redraw();
		}

		// Transform Table actor and bone names display, actor related extra

		private static unsafe bool TransformTable(Actor* target) {
			var select = Skeleton.BoneSelect;
			var bone = Skeleton.GetSelectedBone();

			if (!select.Active) return Transform.Draw(target);
			if (bone == null) return false;

			return Transform.Draw(bone);
		}

		// Selection details

		private unsafe static void SelectInfo(GameObject target) {
			var actor = (Actor*)target.Address;

			var select = Skeleton.BoneSelect;
			var bone = Skeleton.GetSelectedBone();

			var frameSize = new Vector2(ImGui.GetContentRegionAvail().X - GuiHelpers.WidthMargin(), PanelHeight);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, ImGui.GetStyle().FramePadding.Y / 2));
			if (ImGui.BeginChildFrame(8, frameSize, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar)) {
				GameAnimationIndicator();

				ImGui.BeginGroup();

				// display target name
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetStyle().FramePadding.Y / 2));
				ImGui.Text(actor->GetNameOrId());

				// display selected bone name
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (ImGui.GetStyle().ItemSpacing.Y / 2) - (ImGui.GetStyle().FramePadding.Y / 2));
				if (select.Active && bone != null) {
					ImGui.Text($"{bone.LocaleName}");
				} else {
					ImGui.BeginDisabled();
					ImGui.Text("未选择骨骼");
					ImGui.EndDisabled();
				}

				ImGui.EndGroup();

				ImGui.EndChildFrame();
			}
			ImGui.PopStyleVar();
		}

		private static void GameAnimationIndicator() {
			var target = Ktisis.GPoseTarget;
			if (target == null) return;

			var isGamePlaybackRunning = PoseHooks.IsGamePlaybackRunning(target);
			var icon = isGamePlaybackRunning ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;

			var size = GuiHelpers.CalcIconSize(icon).X;

			ImGui.SameLine(size / 1.5f);

			ImGui.BeginGroup();

			ImGui.Dummy(new Vector2(size, size) / 2);

			GuiHelpers.Icon(icon);
			GuiHelpers.Tooltip(isGamePlaybackRunning ? "此目标正在播放游戏动作." + (PoseHooks.PosingEnabled ? "\n动作可能会周期性重置." : "") : "目标的游戏动作已暂停." + (!PoseHooks.PosingEnabled ? "\n动画控制将无法使用." : ""));

			ImGui.EndGroup();

			ImGui.SameLine(size * 2.5f);
		}

		public unsafe static void ImportExportPose(Actor* actor) {
			ImGui.Spacing();
			ImGui.Text("Transforms");

			// Transforms

			var trans = Ktisis.Configuration.PoseTransforms;

			var rot = trans.HasFlag(PoseTransforms.Rotation);
			if (ImGui.Checkbox("旋转##ImportExportPose", ref rot))
				trans = trans.ToggleFlag(PoseTransforms.Rotation);

			var pos = trans.HasFlag(PoseTransforms.Position);
			var col = pos;
			ImGui.SameLine();
			if (col) ImGui.PushStyleColor(ImGuiCol.Text, 0xff00fbff);
			if (ImGui.Checkbox("位置##ImportExportPose", ref pos))
				trans = trans.ToggleFlag(PoseTransforms.Position);
			if (col) ImGui.PopStyleColor();

			var scale = trans.HasFlag(PoseTransforms.Scale);
			col = scale;
			ImGui.SameLine();
			if (col) ImGui.PushStyleColor(ImGuiCol.Text, 0xff00fbff);
			if (ImGui.Checkbox("缩放##ImportExportPose", ref scale))
				trans = trans.ToggleFlag(PoseTransforms.Scale);
			if (col) ImGui.PopStyleColor();

			if (trans > PoseTransforms.Rotation) {
				ImGui.TextColored(
					ColYellow,
					"* 可能会出现意想不到的结果."
				);
			}

			Ktisis.Configuration.PoseTransforms = trans;

			ImGui.Spacing();
			ImGui.Text("模式");

			// Modes

			var modes = Ktisis.Configuration.PoseMode;

			var body = modes.HasFlag(PoseMode.Body);
			if (ImGui.Checkbox("身体##ImportExportPose", ref body))
				modes = modes.ToggleFlag(PoseMode.Body);

			var face = modes.HasFlag(PoseMode.Face);
			ImGui.SameLine();
			if (ImGui.Checkbox("表情##ImportExportPose", ref face))
				modes = modes.ToggleFlag(PoseMode.Face);

			Ktisis.Configuration.PoseMode = modes;

			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();

			var isUseless = trans == 0 || modes == 0;

			if (isUseless) ImGui.BeginDisabled();
			if (ImGui.Button("导入##ImportExportPose")) {
				KtisisGui.FileDialogManager.OpenFileDialog(
					"导入姿势文件",
					"姿势文件 (.pose|.cmp){.pose,.cmp}",
					(success, path) => {
						if (!success) return;

						var content = File.ReadAllText(path[0]);
                        bool isCMP = path[0].EndsWith(".cmp");
                        bool recoverState = false;

                        var pose = isCMP ? CMPFile.Upgrade(content) : JsonParser.Deserialize<PoseFile>(content);
                        if (pose == null) return;

						if (actor->Model == null) return;

                        if (isCMP && trans.HasFlag(PoseTransforms.Position))
                        {
                            // Temporary disable the position flag (cuz cmp doesnt have one)
                            trans = trans.ToggleFlag(PoseTransforms.Position);
                            recoverState = true;
                        }

                        var skeleton = actor->Model->Skeleton;
						if (skeleton == null) return;

						pose.ConvertLegacyBones();

						if (pose.Bones != null) {
							for (var p = 0; p < skeleton->PartialSkeletonCount; p++) {
								switch (p) {
									case 0:
										if (!body) continue;
										break;
									case 1:
										if (!face) continue;
										break;
								}

								pose.Bones.ApplyToPartial(skeleton, p, trans);
							}
						}

                        if (isCMP && recoverState)
                        {
                            // Turn it back on if needed
                            trans = trans.ToggleFlag(PoseTransforms.Position);
                        }
                    },
					1,
					null
				);
			}
			if (isUseless) ImGui.EndDisabled();
			ImGui.SameLine();
			if (ImGui.Button("导出##ImportExportPose")) {
				KtisisGui.FileDialogManager.SaveFileDialog(
					"导出姿势文件至",
					"姿势文件 (.pose){.pose}",
					"Untitled.pose",
					".pose",
					(success, path) => {
						if (!success) return;

						var model = actor->Model;
						if (model == null) return;

						var skeleton = model->Skeleton;
						if (skeleton == null) return;

						var pose = new PoseFile();

						pose.Position = model->Position;
						pose.Rotation = model->Rotation;
						pose.Scale = model->Scale;

						pose.Bones = new PoseContainer();
						pose.Bones.Store(skeleton);

						var json = JsonParser.Serialize(pose);
						using (var file = new StreamWriter(path))
							file.Write(json);
					}
				);
			}

			ImGui.Spacing();
		}

		public unsafe static void ImportExportChara(Actor* actor) {
			var mode = Ktisis.Configuration.CharaMode;

			// Equipment

			ImGui.BeginGroup();
			ImGui.Text("装备");

			var gear = mode.HasFlag(SaveModes.EquipmentGear);
			if (ImGui.Checkbox("护甲##ImportExportChara", ref gear))
				mode ^= SaveModes.EquipmentGear;

			var accs = mode.HasFlag(SaveModes.EquipmentAccessories);
			if (ImGui.Checkbox("附件##ImportExportChara", ref accs))
				mode ^= SaveModes.EquipmentAccessories;

			var weps = mode.HasFlag(SaveModes.EquipmentWeapons);
			if (ImGui.Checkbox("武器##ImportExportChara", ref weps))
				mode ^= SaveModes.EquipmentWeapons;

			ImGui.EndGroup();

			// Appearance

			ImGui.SameLine();
			ImGui.BeginGroup();
			ImGui.Text("外观");

			var body = mode.HasFlag(SaveModes.AppearanceBody);
			if (ImGui.Checkbox("身体##ImportExportChara", ref body))
				mode ^= SaveModes.AppearanceBody;

			var face = mode.HasFlag(SaveModes.AppearanceFace);
			if (ImGui.Checkbox("脸型##ImportExportChara", ref face))
				mode ^= SaveModes.AppearanceFace;

			var hair = mode.HasFlag(SaveModes.AppearanceHair);
			if (ImGui.Checkbox("头发##ImportExportChara", ref hair))
				mode ^= SaveModes.AppearanceHair;

			ImGui.EndGroup();

			// Import & Export buttons

			Ktisis.Configuration.CharaMode = mode;

			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();

			var isUseless = mode == SaveModes.None;
			if (isUseless) ImGui.BeginDisabled();

			if (ImGui.Button("导入##ImportExportChara")) {
				KtisisGui.FileDialogManager.OpenFileDialog(
					"导入角色数据",
					"Anamnesis 角色数据 (.chara){.chara}",
					(success, path) => {
						if (!success) return;

						var content = File.ReadAllText(path[0]);
						var chara = JsonParser.Deserialize<AnamCharaFile>(content);
						if (chara == null) return;

						chara.Apply(actor, mode);
					},
					1,
					null
				);
			}

			ImGui.SameLine();

			if (ImGui.Button("导出##ImportExportChara")) {
				KtisisGui.FileDialogManager.SaveFileDialog(
					"导出角色数据至",
					"Anamnesis 角色数据 (.chara){.chara}",
					"Untitled.chara",
					".chara",
					(success, path) => {
						if (!success) return;

						var chara = new AnamCharaFile();
						chara.WriteToFile(*actor, mode);

						var json = JsonParser.Serialize(chara);
						using (var file = new StreamWriter(path))
							file.Write(json);
					}
				);
			}

			if (isUseless) ImGui.EndDisabled();

			ImGui.Spacing();
		}
	}
}
