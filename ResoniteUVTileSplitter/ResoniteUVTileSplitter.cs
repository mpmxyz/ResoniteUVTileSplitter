using System.Reflection.Metadata.Ecma335;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using ResoniteModLoader;

namespace ResoniteUVTileSplitter;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class ResoniteUVTileSplitter : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "ResoniteUVTileSplitter";
	public override string Author => "mpmxyz";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/resonite-modding-group/ResoniteUVTileSplitter/";

	public override void OnEngineInit() {
		Harmony harmony = new("com.example.ResoniteUVTileSplitter");
		harmony.PatchAll();
	}

	//Example of how a HarmonyPatch can be formatted, Note that the following isn't a real patch and will not compile.
	[HarmonyPatch(typeof(ClassNameHere), "MethodNameHere")]
	class ClassNameHere_MethodNameHere_Patch {
		static void Postfix(ClassNameHere __instance) {
			Msg("Postfix from ResoniteUVTileSplitter");
		}
	}

	static List<MeshX> SplitMeshByUVTiles(MeshX original, int uvChannel) {
		Dictionary<UVTileKey, MeshX> mappedMeshes = [];

		MeshX GetMeshForKey(UVTileKey key) {
			if (mappedMeshes.TryGetValue(key, out MeshX? value)) return value;
			var newMesh = new MeshX(original);
			var submeshCount = newMesh.SubmeshCount;
			foreach (Submesh subMesh in newMesh.Submeshes) {
				subMesh.Clear();
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
		List<(UVTileKey, UVTriangle)> tiledUVs = [];
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

				tiledUVs.Clear();
				Algorithm.SplitIntoTiles(fullUVTriangle, tiledUVs);

				Dictionary<(UVTileKey, EdgeUV), Vertex> knownEdgeIntersections;
				foreach ((UVTileKey key, UVTriangle tileTriangle) in tiledUVs) {
					MeshX targetMesh = GetMeshForKey(key);
					TriangleSubmesh targetTriangles = targetMesh.TryGetSubmesh<TriangleSubmesh>(subMeshIndex);

					Vertex GetMappedVertex(TriangleUV uv) {
						if (uv.Mixture == new float3(1, 0, 0)) {
							return v0;
						} else if (uv.Mixture == new float3(0, 1, 0)) {
							return v1;
						} else if (uv.Mixture == new float3(0, 0, 1)) {
							return v2;
						}
						//TODO: handle edges
						var vertex = targetMesh.AddVertex();
						Mixer.MixVertices(uv.Mixture, v0, v1, v2, vertex);
					}
					targetTriangles.AddTriangle(GetMappedVertex(tileTriangle.UV0), GetMappedVertex(tileTriangle.UV1), GetMappedVertex(tileTriangle.UV2))
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
		return [.. mappedMeshes.Values];
	}
}
