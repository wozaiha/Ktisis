using System;
using System.Linq;
using System.Numerics;

using ImGuiNET;
using ImGuizmoNET;

using Dalamud.Interface;
using Dalamud.Interface.Components;

using Ktisis.Util;
using Ktisis.Overlay;
using Ktisis.Interop.Hooks;
using Ktisis.Structs.Bones;
using Ktisis.Interface.Windows;
using Ktisis.Interface.Windows.Workspace;
using Ktisis.History;

namespace Ktisis.Interface.Components {
	public static class ControlButtons {
		public static Vector2 ButtonSize => new Vector2(ImGui.GetFontSize() * 1.6f) + ImGui.GetStyle().FramePadding;
		private static bool IsSettingsHovered = false;
		private static bool IsSettingsActive = false;

		// utils
		public static void VerticalAlignTextOnButtonSize(float percentage = 0.667f) => ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ButtonSize.Y / 2 - ImGui.GetFontSize() * percentage)); // align text with button size

		public static void DrawExtra() {
			var gizmode = Ktisis.Configuration.GizmoMode;
			if (GuiHelpers.IconButtonTooltip(gizmode == MODE.WORLD ? FontAwesomeIcon.Globe : FontAwesomeIcon.Home, "本地 / 世界坐标模式切换.", ButtonSize))
				Ktisis.Configuration.GizmoMode = gizmode == MODE.WORLD ? MODE.LOCAL : MODE.WORLD;

			ImGui.SameLine();
			var showSkeleton = Ktisis.Configuration.ShowSkeleton;
			if (showSkeleton) ImGui.PushStyleColor(ImGuiCol.Text,GuiHelpers.VisibleCheckmarkColor());
			if (GuiHelpers.IconButton(showSkeleton ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash, ButtonSize))
				Skeleton.Toggle();
			if (showSkeleton) ImGui.PopStyleColor();
			GuiHelpers.Tooltip((showSkeleton ? "隐藏" : "显示") + " 骨骼线和骨骼.");


			ImGui.SameLine();
			DrawSiblingLink();

			ImGui.SameLine();

			var gizmoActive = OverlayWindow.IsGizmoVisible;
			if (!gizmoActive) ImGui.BeginDisabled();
			if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.MinusCircle, "取消选择骨骼", ButtonSize))
				OverlayWindow.DeselectGizmo();
			if (!gizmoActive) ImGui.EndDisabled();

			var canUndo = HistoryManager.CanUndo;
			var canRedo = HistoryManager.CanRedo;

			ImGui.SameLine();

			if (!canUndo) ImGui.BeginDisabled();
			if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.Backward, "撤销更改", ButtonSize)) 
				HistoryManager.Undo();
			if (!canUndo) ImGui.EndDisabled();

			ImGui.SameLine();

			if (!canRedo) ImGui.BeginDisabled();
			if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.Forward, "恢复更改", ButtonSize))
				HistoryManager.Redo();
			if (!canRedo) ImGui.EndDisabled();
		}

		// As these buttons are a bit special and should not be as present as others
		// we remove the border and change the hover behavior.

		private static void DrawInfo() {
			ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 200f);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetFontSize() * 0.25f));

			if (GuiHelpers.IconButton(FontAwesomeIcon.InfoCircle, new(ImGui.GetFontSize() * 1.5f)))
				Information.Toggle();

			ImGui.PopStyleColor();
			ImGui.PopStyleVar(2);

			IsSettingsHovered = ImGui.IsItemHovered();
			IsSettingsActive = ImGui.IsItemActive();

			GuiHelpers.Tooltip("信息");
		}
		private static void DrawSettings() {
			ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 200f);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetFontSize() * 0.25f));

			if (GuiHelpers.IconButton(FontAwesomeIcon.Cog, new(ImGui.GetFontSize() * 1.5f)))
				ConfigGui.Toggle();

			ImGui.PopStyleColor();
			ImGui.PopStyleVar(2);

			IsSettingsHovered = ImGui.IsItemHovered();
			IsSettingsActive = ImGui.IsItemActive();

			GuiHelpers.Tooltip("打开设置");
		}
		public static void PlaceAndRenderSettings() {

			var initialPos = ImGui.GetCursorPos();
			ImGui.PushClipRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), false);

			// A bit complicated formulas to handle any styles values
			ImGui.SetCursorPosX(initialPos.X + ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X - ImGui.GetFontSize() * (3.5f * 1.5f) - (float)Math.Exp(ImGui.GetFontSize() / 18));
			ImGui.SetCursorPosY(initialPos.Y - ImGui.GetStyle().FramePadding.Y - (float)Math.Log2(ImGui.GetTextLineHeight()) * 3.5f - ImGui.GetTextLineHeight()*1.05f);

			DrawInfo();
			ImGui.SameLine();
			DrawSettings();

			ImGui.PopClipRect();
			ImGui.SetCursorPos(initialPos);

		}

		public static void ButtonChangeOperation(OPERATION operation, FontAwesomeIcon icon) {
			var isCurrentOperation = Ktisis.Configuration.GizmoOp.HasFlag(OPERATION.ROTATE_X) ? (Ktisis.Configuration.GizmoOp | OPERATION.ROTATE).HasFlag(operation) : Ktisis.Configuration.GizmoOp.HasFlag(operation);
			if (isCurrentOperation) ImGui.PushStyleColor(ImGuiCol.Text, GuiHelpers.VisibleCheckmarkColor());

			if (GuiHelpers.IconButton(icon, ButtonSize))
				if (!isCurrentOperation)
					Ktisis.Configuration.GizmoOp = ImGui.GetIO().KeyShift ? Ktisis.Configuration.GizmoOp.AddFlag(operation) : operation;
				else if (ImGui.GetIO().KeyCtrl)
					Ktisis.Configuration.GizmoOp = Ktisis.Configuration.GizmoOp.ToggleFlag(OPERATION.ROTATE, OPERATION.ROTATE_X, OPERATION.ROTATE_Y, OPERATION.ROTATE_Z);
				else if (ImGui.GetIO().KeyShift)
					Ktisis.Configuration.GizmoOp = Ktisis.Configuration.GizmoOp.RemoveFlag(operation);
				else
					Ktisis.Configuration.GizmoOp = operation;
			
			if (isCurrentOperation) ImGui.PopStyleColor();

			string help = "";
			if (isCurrentOperation)
				help += "当前坐标轴模式为 ";
			else
				help += "更改坐标轴模式至 ";

			help += operation switch {
				OPERATION.TRANSLATE => "位置",
				OPERATION.ROTATE => "旋转",
				OPERATION.SCALE => "坐标",
				OPERATION.UNIVERSAL => "全局",
				var _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
			};

			GuiHelpers.Tooltip(help + ".");
		}

		// Independant from the others
		public static void DrawPoseSwitch() {
			ImGui.SetCursorPosX(ImGui.CalcTextSize("GPose 已禁用").X + (ImGui.GetFontSize() * 8) + ImGui.GetStyle().ItemSpacing.X + GuiHelpers.CalcIconSize(FontAwesomeIcon.Cog).X); // Prevents text overlap

			ImGui.BeginDisabled(!Ktisis.IsInGPose);
			var pose = PoseHooks.PosingEnabled;
			if (Ktisis.IsInGPose) ImGui.PushStyleColor(ImGuiCol.Text, pose ? Workspace.ColGreen : Workspace.ColRed);
			var label = pose ? "姿态模式中" : "未在姿态模式";
			float toggleWidth = ImGui.GetFrameHeight() * 1.55f;
			float offsetWidth = GuiHelpers.GetRightOffset(toggleWidth);
			GuiHelpers.TextRight(label, offsetWidth);
			if (Ktisis.IsInGPose) ImGui.PopStyleColor();
			ImGui.SameLine();

			if (!Ktisis.IsInGPose)
				ImGuiComponents.DisabledToggleButton("Toggle Posing", false);
			else
				if (GuiHelpers.ToggleButton("Toggle Posing", ref pose, pose ? Workspace.ColGreen : Workspace.ColRed))
				PoseHooks.TogglePosing();

			ImGui.EndDisabled();
		}

		public static void DrawSimplePoseSwitch() {
			var pose = PoseHooks.PosingEnabled;
			if (!Ktisis.IsInGPose)
				ImGuiComponents.DisabledToggleButton("Toggle Posing", false);
			else if (GuiHelpers.ToggleButton("Toggle Posing", ref pose, pose ? Workspace.ColGreen : Workspace.ColRed))
				PoseHooks.TogglePosing();
			GuiHelpers.Tooltip("开关姿态模式");
		}

		public static void DrawSiblingLink() {
			var siblingLink = Ktisis.Configuration.SiblingLink;

			if (siblingLink != SiblingLink.None) ImGui.PushStyleColor(ImGuiCol.Text, GuiHelpers.VisibleCheckmarkColor());
			if (GuiHelpers.IconButton(SiblingLinkToIcon(siblingLink), ButtonSize))
				CircleTroughSiblingLinkModes();
			if (siblingLink != SiblingLink.None) ImGui.PopStyleColor();
			GuiHelpers.Tooltip(SiblingLinkToTooltip(siblingLink));
		}
		public static void CircleTroughSiblingLinkModes() =>
			Ktisis.Configuration.SiblingLink = Ktisis.Configuration.SiblingLink == Enum.GetValues(typeof(SiblingLink)).Cast<SiblingLink>().Last() ? SiblingLink.None : Ktisis.Configuration.SiblingLink + 1;
		private static FontAwesomeIcon SiblingLinkToIcon(SiblingLink siblingLink)
			=> siblingLink switch {
				SiblingLink.Rotation => FontAwesomeIcon.Link,
				SiblingLink.RotationMirrorX => FontAwesomeIcon.Adjust,
				_ => FontAwesomeIcon.ArrowUp
			};
		private static string SiblingLinkToTooltip(SiblingLink siblingLink)
			=> siblingLink switch {
				SiblingLink.Rotation => "链接旋转",
				SiblingLink.RotationMirrorX => "镜像旋转",
				_ => "无子节点"
			};
	}
}
