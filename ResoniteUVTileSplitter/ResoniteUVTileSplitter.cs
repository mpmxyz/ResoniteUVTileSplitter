using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

using HarmonyLib;

using ResoniteModLoader;

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

	static async Task SplitMeshByUVTilesTask(MeshRenderer renderer, MeshX original, Slot assetTarget, int uvChannel) {
		Dictionary<UVTileKey, MeshX> tiledMeshes = SplitMeshByUVTiles(original, uvChannel);
		//tiledMeshes.ForEach((mesh) => mesh.StripEmptyBlendshapes());
		//tiledMeshes.ForEach((mesh) => mesh.StripEmptyBones());
		Dictionary<UVTileKey, Uri> meshURIs = [];
		foreach ((UVTileKey key, MeshX mesh) in tiledMeshes) {
			meshURIs[key] = await renderer.Engine.LocalDB.SaveAssetAsync(mesh).ConfigureAwait(continueOnCapturedContext: false);
		}
		await default(ToWorld);
		MeshCollider meshCollider = renderer.Slot.GetComponent<MeshCollider>((c) => c.Mesh.Target == renderer.Mesh.Target);
		//TODO: sort by UV tile
		foreach ((UVTileKey key, Uri meshURI) in meshURIs) {
			//TODO: move original mesh to tile closest to 0/0 instead of copying
			StaticMesh staticMesh = assetTarget.AttachStaticMesh(meshURI);
			Slot targetSlot = renderer.Slot.AddSlot($"{renderer.Slot.Name} - {1001 + key.U + 10 * key.V} / ({key.U},{key.V})");
			
			//CopyComponent((Component)...) makes this agnostic to the underlying renderer type.
			MeshRenderer newRenderer = (MeshRenderer)targetSlot.CopyComponent((Component)renderer);
			newRenderer.Mesh.Target = staticMesh;
			//TODO: option to ValueCopy blendshapes from reused mesh
			if (meshCollider != null) {
				MeshCollider newCollider = targetSlot.CopyComponent(meshCollider);
				newCollider.Mesh.Target = staticMesh;
			}
		}
		meshCollider?.Destroy();
		renderer.Destroy();
	}

	static Dictionary<UVTileKey, MeshX> SplitMeshByUVTiles(MeshX original, int uvChannel) {
		Dictionary<UVTileKey, MeshX> mappedMeshes = [];

		MeshX GetMeshForKey(UVTileKey key) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($"GetMeshForKey {key}");
			}
			if (mappedMeshes.TryGetValue(key, out MeshX? value)) return value;
			var newMesh = new MeshX(original);
			var submeshCount = newMesh.SubmeshCount;
			foreach (Submesh subMesh in newMesh.Submeshes) {
				subMesh.Clear();
			}
			if (newMesh.BlendShapeCount != original.BlendShapeCount) {
				throw new Exception($"Assertion failed: {newMesh.BlendShapeCount} == {original.BlendShapeCount}");
			}
			foreach (Vertex vertex in newMesh.Vertices) {
				vertex.SetUV(uvChannel, vertex.GetUV(uvChannel) - key.UV, true);
			}

			mappedMeshes.Add(key, newMesh);
			return newMesh;
		}

		void MapPointSubmesh(PointSubmesh sourcePoints) {
			int subMeshIndex = sourcePoints.Index;
			int pointCount = sourcePoints.Count;
			for (int i = 0; i < pointCount; i++) {
				Point point = sourcePoints.GetPoint(i);
				UVTileKey key = UVTileKey.FromUV(point.Vertex.GetUV(uvChannel));
				MeshX targetMesh = GetMeshForKey(key);
				PointSubmesh targetPoints = targetMesh.TryGetSubmesh<PointSubmesh>(subMeshIndex);
				targetPoints.AddPoint(point.VertexIndex);
			}
		}

		//reused triangle lists to avoid allocations
		List<UVTriangle> halfCutU = [], fullCutU = [], halfCutV = [], fullCutV = [];
		List<(UVTileKey, UVTriangle)> tiledUVs = [];
		Dictionary<(UVTileKey, VertexMixture), Vertex> knownVertexMixtures = [];
		void MapTriangleSubmesh(TriangleSubmesh sourceTriangles) {
			int subMeshIndex = sourceTriangles.Index;
			int triangleCount = sourceTriangles.Count;
			for (int i = 0; i < triangleCount; i++) {
				Triangle sourceTriangle = sourceTriangles.GetTriangle(i);
				var v0 = sourceTriangle.Vertex0;
				var v1 = sourceTriangle.Vertex1;
				var v2 = sourceTriangle.Vertex2;
				var uv0 = v0.GetUV(uvChannel);
				var uv1 = v1.GetUV(uvChannel);
				var uv2 = v2.GetUV(uvChannel);
				UVTriangle fullUVTriangle = new(uv0, uv1, uv2);
				if (DebugHelpers.PrintTrace) {
					DebugHelpers.Trace($"Triangle {i} ({v0.Index} {v1.Index} {v2.Index})");
				}

				tiledUVs.Clear();
				Algorithm.SplitIntoTiles(fullUVTriangle, tiledUVs, halfCutU, fullCutU, halfCutV, fullCutV);

				foreach ((UVTileKey key, UVTriangle tileTriangle) in tiledUVs) {
					MeshX targetMesh = GetMeshForKey(key);
					TriangleSubmesh targetTriangles = targetMesh.TryGetSubmesh<TriangleSubmesh>(subMeshIndex);

					Vertex GetMappedVertex(TriangleUV uv) {
						if (DebugHelpers.PrintTrace) {
							DebugHelpers.Trace($"GetMappedVertex {uv}");
						}
						if (uv.Mixture == new float3(1, 0, 0)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v0 {v0.Position} {v0.UV0 - key.UV}");
							}
							return v0;
						} else if (uv.Mixture == new float3(0, 1, 0)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v1 {v1.Position} {v1.UV0 - key.UV}");
							}
							return v1;
						} else if (uv.Mixture == new float3(0, 0, 1)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v2 {v2.Position} {v2.UV0 - key.UV}");
							}
							return v2;
						}
						//(UVTileKey, VertexMixture) mixtureKey = (key, new VertexMixture(new int3(v0.Index, v1.Index, v2.Index), uv.Mixture).Canonalized());
						//if (knownVertexMixtures.TryGetValue(mixtureKey, out var mixedVertex)) {
						//	//avoided creating a duplicated vertex
						//	return mixedVertex;
						//}

						var vertex = targetMesh.AddVertex();
						Mixer.MixVertices(uv.Mixture, v0, v1, v2, uvChannel, key, vertex);
						//knownVertexMixtures.Add(mixtureKey, vertex);
						if (DebugHelpers.PrintTrace) {
							DebugHelpers.Trace($"GetMappedVertex mixture {vertex.Position} {vertex.UV0}");
						}
						return vertex;
					}
					var outputVertex0 = GetMappedVertex(tileTriangle.UV0);
					var outputVertex1 = GetMappedVertex(tileTriangle.UV1);
					var outputVertex2 = GetMappedVertex(tileTriangle.UV2);
					targetTriangles.AddTriangle(outputVertex0, outputVertex1, outputVertex2);
					if (DebugHelpers.PrintTrace) {
						DebugHelpers.Trace($"Added Triangle ({outputVertex0.Position} {outputVertex0.UV0})({outputVertex1.Position} {outputVertex1.UV0})({outputVertex2.Position} {outputVertex2.UV0})");
					}
				}
			}
		}


		List<MappedTriangle> mappedTriangles = [];
		foreach (Submesh subMesh in original.Submeshes) {
			switch (subMesh) {
				case null:
					continue;
				case PointSubmesh sourcePoints:
					MapPointSubmesh(sourcePoints);
					break;
				case TriangleSubmesh sourceTriangles:
					MapTriangleSubmesh(sourceTriangles);
					break;
				default:
					throw new NotSupportedException($"Submeshes of type '{subMesh.GetType()}' are not supported, yet!");
			}
		}
		return mappedMeshes;
	}
}
