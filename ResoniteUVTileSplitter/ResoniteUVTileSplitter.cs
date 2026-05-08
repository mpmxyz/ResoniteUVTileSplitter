using Elements.Assets;

using FrooxEngine;
using FrooxEngine.UIX;

using HarmonyLib;

using ResoniteModLoader;

using ResoniteUVTileSplitter.UVSplitting;

namespace ResoniteUVTileSplitter;

public class ResoniteUVTileSplitter : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "ResoniteUVTileSplitter";
	public override string Author => "mpmxyz";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/mpmxyz/ResoniteUVTileSplitter/";

	public override void OnEngineInit() {
		Harmony harmony = new("mpmxyz.ResoniteUVTileSplitter");
		harmony.PatchAll();
	}

	[HarmonyPatch]
	class MeshRenderer_PrivateMethods {
		[HarmonyPatch(typeof(MeshRenderer), "RunProcessingTask")]
		[HarmonyReversePatch]
		public static Task RunProcessingTask(MeshRenderer __instance, IButton button, Func<MeshX, Slot, Task> process) => throw new NotImplementedException("Filled by Harmony");

		[HarmonyPatch(typeof(MeshRenderer), "AttachSplitMesh")]
		[HarmonyReversePatch]
		public static MeshRenderer AttachSplitMesh(MeshRenderer __instance, Slot slot, MeshX mesh) => throw new NotImplementedException("Filled by Harmony");
	}

	//Example of how a HarmonyPatch can be formatted, Note that the following isn't a real patch and will not compile.
	[HarmonyPatch(typeof(MeshRenderer), "BuildInspectorUI")]
	class MeshRenderer_BuildInspectorUI_Patch {
		public static void Postfix(MeshRenderer __instance, UIBuilder ui) {
			AddSplitButton(__instance, ui, 0);
			AddSplitButton(__instance, ui, 1);
			AddSplitButton(__instance, ui, 2);
			AddSplitButton(__instance, ui, 3);
		}

		private static void AddSplitButton(MeshRenderer __instance, UIBuilder uiBuilder, int uvChannel) {
			bool previousPressed = false;
			Button splitMeshButton = uiBuilder.Button($"Split UV Tiles (UV Channel: {uvChannel})");
			splitMeshButton.Slot.DestroyWhenUserLeaves(__instance.LocalUser);
			//listening to IsPressed allows other users to execute the action
			splitMeshButton.IsPressed.OnValueChange += (isPressed) => {
				if (isPressed.Value == previousPressed) {
					return;
				}
				previousPressed = isPressed.Value;
				if (isPressed.Value) {
					MeshRenderer_PrivateMethods.RunProcessingTask(__instance, splitMeshButton, (original, assetTarget) => {
						return SplitMeshByUVTilesTask(__instance, original, assetTarget, uvChannel);
					});
				}
			};
		}
	}

	/// <summary>
	/// This will split the <paramref name="originalMesh"/> into tiles and ensure a MeshRenderer based on <paramref name="originalRenderer"/> for each tile.
	/// The original renderer will be moved and used to display tile (0,0).
	/// The enabled field and blendshape values of duplicated renderers will be driven based on the original renderer.
	/// </summary>
	/// <param name="originalRenderer">The (Skinned-)MeshRenderer that will be duplicated to display all tiles</param>
	/// <param name="originalMesh">The mesh that should be split</param>
	/// <param name="assetTarget">Slot that created assets are attached to</param>
	/// <param name="uvChannel">UV-Channel that decides on how to split</param>
	/// <returns></returns>
	static async Task SplitMeshByUVTilesTask(MeshRenderer originalRenderer, MeshX originalMesh, Slot assetTarget, int uvChannel) {
		Dictionary<UVTileKey, MeshX> tiledMeshes = MeshXSplitting.Algorithm.SplitMeshByUVTiles(originalMesh, uvChannel);
		if (tiledMeshes.Count == 0) {
			DebugHelpers.Debug("No meshes in split result -> nothing to do");
			return;
		}
		//tiledMeshes.ForEach((mesh) => mesh.StripEmptyBlendshapes());
		//tiledMeshes.ForEach((mesh) => mesh.StripEmptyBones());
		DebugHelpers.Debug("Saving split mesh assets...");
		Dictionary<UVTileKey, Uri> meshURIs = [];
		foreach ((UVTileKey key, MeshX mesh) in tiledMeshes) {
			meshURIs[key] = await originalRenderer.Engine.LocalDB.SaveAssetAsync(mesh).ConfigureAwait(continueOnCapturedContext: false);
		}
		DebugHelpers.Debug("Split meshes are saved!");
		await default(ToWorld);
		DebugHelpers.Debug("Attaching MeshRenderers for split meshes...");
		MeshCollider meshCollider = originalRenderer.Slot.GetComponent<MeshCollider>((c) => c.Mesh.Target == originalRenderer.Mesh.Target);
		IOrderedEnumerable<KeyValuePair<UVTileKey, Uri>> sortedMeshURIs = meshURIs.OrderBy((kv) => kv.Key.V).ThenBy((kv) => kv.Key.U);
		UVTileKey tileClosestToOrigin = meshURIs.MinBy((kv) => Math.Abs(kv.Key.U) + Math.Abs(kv.Key.V)).Key;
		Slot tileParentSlot = originalRenderer.Slot;
		foreach ((UVTileKey key, Uri meshURI) in sortedMeshURIs) {
			StaticMesh staticMesh = assetTarget.AttachStaticMesh(meshURI);
			Slot targetSlot = tileParentSlot.AddSlot($"{originalRenderer.Slot.Name} - {1001 + key.U + 10 * key.V} / ({key.U},{key.V})");
			MeshRenderer newRenderer;
			if (key == tileClosestToOrigin) {
				//reuse existing MeshRenderer to maintain blendshape drives
				originalRenderer = (MeshRenderer)targetSlot.MoveComponent((Component)originalRenderer);
				newRenderer = originalRenderer;
				if (meshCollider != null) {
					targetSlot.MoveComponent(meshCollider);
				}
			} else {
				//CopyComponent((Component)...) makes this agnostic to the underlying renderer type.
				newRenderer = (MeshRenderer)targetSlot.CopyComponent((Component)originalRenderer);
				newRenderer.Mesh.Target = staticMesh;
				if (meshCollider != null) {
					MeshCollider newCollider = targetSlot.CopyComponent(meshCollider);
					newCollider.Mesh.Target = staticMesh;
				}
				//TODO: make drives optional
				newRenderer.EnabledField.DriveFrom(originalRenderer.EnabledField);
				if (originalRenderer is SkinnedMeshRenderer originalSkinned && newRenderer is SkinnedMeshRenderer newSkinned) {
					int blendshapeCount = Math.Min(originalSkinned.BlendShapeWeights.Count, newSkinned.BlendShapeWeights.Count);
					for (int i = 0; i < blendshapeCount; i++) {
						//TODO: option to only copy driven blendshapes
						//TODO: option to duplicate drivers (?)
						newSkinned.BlendShapeWeights.GetField(i).DriveFrom(originalSkinned.BlendShapeWeights.GetField(i));
					}
				}
			}
		}
		DebugHelpers.Debug("Successfully created MeshRenderers for split meshes!");
	}

}
