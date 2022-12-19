using ImGuiNET;

using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.Havok;

using Ktisis.Interop.Hooks;
using Ktisis.Util;

namespace Ktisis.Interface.Components {
	public static class AnimationControls {


		public static unsafe void Draw(GameObject? target) {
			// Animation control
			if (ImGui.CollapsingHeader("��������")) {
				var control = PoseHooks.GetAnimationControl(target);
				if (PoseHooks.PosingEnabled || !Ktisis.IsInGPose || PoseHooks.IsGamePlaybackRunning(target) || control == null) {
					ImGui.Text("��������ֻ����������������:");
					ImGui.BulletText("��Ϸ��������ͣ");
					ImGui.BulletText("��̬ģʽ�ѽ���");
				} else
					AnimationSeekAndSpeed(control);
			}

		}
		public static unsafe void AnimationSeekAndSpeed(hkaDefaultAnimationControl* control) {
			var duration = control->hkaAnimationControl.Binding.ptr->Animation.ptr->Duration;
			var durationLimit = duration - 0.05f;

			if (control->hkaAnimationControl.LocalTime >= durationLimit)
				control->hkaAnimationControl.LocalTime = 0f;

			ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - GuiHelpers.WidthMargin() - GuiHelpers.GetRightOffset(ImGui.CalcTextSize("�ٶ�").X));
			ImGui.SliderFloat("Ѱ��", ref control->hkaAnimationControl.LocalTime, 0, durationLimit);
			ImGui.SliderFloat("�ٶ�", ref control->PlaybackSpeed, 0f, 0.999f);
			ImGui.PopItemWidth();
		}

	}
}
