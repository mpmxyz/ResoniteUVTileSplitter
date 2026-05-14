using Elements.Assets;
using Elements.Core;

using FrooxEngine;

using ResoniteUVTileSplitter.UVSplitting;

namespace ResoniteUVTileSplitter.MeshRendererSplitting;

internal static class Algorithm {

	/// <summary>
	/// This will split the <paramref name="originalMesh"/> into tiles and ensure a MeshRenderer based on <paramref name="originalRenderer"/> for each tile.
	/// The original renderer will be moved and used to display tile (0,0).
	/// The enabled field and blendshape values of duplicated renderers will be driven based on the original renderer.
	/// </summary>
	/// <param name="originalRenderer">The (Skinned-)MeshRenderer that will be duplicated to display all tiles</param>
	/// <param name="originalMesh">The mesh that should be split</param>
	/// <param name="assetTarget">Slot that created assets are attached to</param>
	/// <param name="uvChannel">UV-Channel that decides on how to split</param>
	/// <param name="setStatus">Action that outputs information about current state to the user</param>
	/// <returns>a task representing this action</returns>
	internal static async Task SplitMeshByUVTilesTask(MeshRenderer originalRenderer, MeshX originalMesh, int uvChannel, bool createDrives, Slot assetTarget, Action<string> setStatus) {
		setStatus("Splitting meshes into tiles...");
		Dictionary<UVTileKey, MeshX> tiledMeshes = MeshXSplitting.Algorithm.SplitMeshByUVTiles(originalMesh, uvChannel, out var trianglesWithIssues);
		if (tiledMeshes.Count == 0) {
			setStatus("No meshes in split result -> nothing to do");
			return;
		}
		DebugHelpers.Debug($"Saving {tiledMeshes.Count} UV tiles...");
		Dictionary<UVTileKey, Uri> meshURIs = [];

		int saveProgress = 0;
		foreach ((UVTileKey key, MeshX mesh) in tiledMeshes) {
			saveProgress++;
			setStatus($"Saving UV Tile {key.U},{key.V}... ({saveProgress}/{tiledMeshes.Count})");
			Uri uri = await originalRenderer.Engine.LocalDB.SaveAssetAsync(mesh).ConfigureAwait(continueOnCapturedContext: false);
			meshURIs[key] = uri;
			DebugHelpers.Debug($"Saved UV Tile {key.U},{key.V} as {uri}.");
		}
		DebugHelpers.Debug($"Saved {tiledMeshes.Count} UV tiles. Creating MeshRenderers...");

		await default(ToWorld);

		if (originalRenderer.IsRemoved) {
			setStatus("Original MeshRenderer has been removed!");
			return;
		}

		setStatus($"Creating MeshRenderers...");
		MeshCollider originalCollider = originalRenderer.Slot.GetComponent<MeshCollider>((c) => c.Mesh.Target == originalRenderer.Mesh.Target);
		IOrderedEnumerable<KeyValuePair<UVTileKey, Uri>> sortedMeshURIs = meshURIs.OrderBy((kv) => kv.Key.V).ThenBy((kv) => kv.Key.U);
		UVTileKey tileClosestToOrigin = meshURIs.MinBy((kv) => Math.Abs(kv.Key.U) + Math.Abs(kv.Key.V)).Key;
		Slot tileParentSlot = originalRenderer.Slot;

		foreach ((UVTileKey key, Uri meshURI) in sortedMeshURIs) {
			DebugHelpers.Debug($"Attaching tile {key.U},{key.V}: {meshURI}...");
			if (assetTarget.IsRemoved) {
				DebugHelpers.Debug("SplitMeshByUVTilesTask: assetTarget.IsRemoved ?!");
				assetTarget = originalRenderer.World.AssetsSlot.AddSlot($"{originalRenderer.Slot}");
			}
			StaticMesh staticMesh = assetTarget.AttachStaticMesh(meshURI, false);
			Slot targetSlot = tileParentSlot.AddSlot($"{tileParentSlot.Name} - {1001 + key.U + 10 * key.V} / ({key.U},{key.V})");
			MeshRenderer newRenderer;
			MeshCollider? newCollider;
			if (key == tileClosestToOrigin) {
				//reuse existing MeshRenderer to maintain blendshape drives
				originalRenderer = (MeshRenderer)targetSlot.MoveComponent((Component)originalRenderer);
				newRenderer = originalRenderer;
				if (originalCollider != null) {
					newCollider = targetSlot.MoveComponent(originalCollider);
				} else {
					newCollider = null;
				}
			} else {
				//CopyComponent((Component)...) makes this agnostic to the underlying renderer type.
				newRenderer = (MeshRenderer)targetSlot.CopyComponent((Component)originalRenderer);
				if (originalCollider != null) {
					newCollider = targetSlot.CopyComponent(originalCollider);
				} else {
					newCollider = null;
				}
				if (createDrives) {
					newRenderer.EnabledField.DriveFrom(originalRenderer.EnabledField);
					if (originalRenderer is SkinnedMeshRenderer originalSkinned && newRenderer is SkinnedMeshRenderer newSkinned) {
						int blendshapeCount = Math.Min(originalSkinned.BlendShapeWeights.Count, newSkinned.BlendShapeWeights.Count);
						for (int i = 0; i < blendshapeCount; i++) {
							newSkinned.BlendShapeWeights.GetField(i).DriveFrom(originalSkinned.BlendShapeWeights.GetField(i));
						}
					}
				}
			}
			newRenderer.Mesh.Target = staticMesh;
			if (newCollider != null) {
				newCollider.Mesh.Target = staticMesh;
			}
		}
		if (trianglesWithIssues.Count > 0) {
			string statistics = string.Join("\n", trianglesWithIssues.GroupBy((kv) => kv.Value).Select((kv) => $"{kv.Key} - {kv.Count()} triangles"));
			var reportSlot = originalRenderer.LocalUserSpace.AddSlot("UV Tile Splitting Errors");
			reportSlot.PositionInFrontOfUser(float3.Backward);
			UniversalImporter.SpawnText(reportSlot, "UV Tile Splitting Errors", $"Statistics:\n{statistics}\n\nCheck the Resonite log file for a list of triangles!");
		}
		setStatus($"Done! ({meshURIs.Count} tiles)");
		DebugHelpers.Msg($" {trianglesWithIssues.Count} errors");
	}
}
