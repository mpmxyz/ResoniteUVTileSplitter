using Elements.Assets;
using Elements.Core;

using ResoniteUVTileSplitter.UVSplitting;

namespace ResoniteUVTileSplitter.MeshXSplitting;

/// <summary>
/// This class is dedicated to splitting MeshX objects into tiles.
/// </summary>
internal static class Algorithm {
	/// <summary>
	/// Splits a single MeshX into UV tiles
	/// </summary>
	/// <param name="originalMesh">mesh to be split</param>
	/// <param name="uvChannel">UV channel to split (allowed values: 0 to 3 inclusive)</param>
	/// <returns>A mapping from UV tiles to their corresponding meshes</returns>
	/// <exception cref="NotSupportedException">If submeshes other than PointSubmesh or TriangleSubmesh are found.</exception>
	internal static Dictionary<UVTileKey, MeshX> SplitMeshByUVTiles(MeshX originalMesh, int uvChannel) {
		DebugHelpers.Debug($"Splitting a Mesh with {originalMesh.VertexCount} vertices, {originalMesh.TotalPointCount} points and {originalMesh.TotalTriangleCount} triangles...");
		if (uvChannel < 0 || uvChannel > 3) {
			throw new ArgumentOutOfRangeException(nameof(uvChannel), uvChannel, "Only UV-Channels 0 to 3 inclusive are supported.");
		}
		Dictionary<UVTileKey, MeshX> mappedMeshes = [];

		MeshX GetMeshForTile(UVTileKey key) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($"GetMeshForKey {key}");
			}
			if (mappedMeshes.TryGetValue(key, out MeshX? value)) return value;
			var newMesh = new MeshX(originalMesh);
			foreach (Submesh subMesh in newMesh.Submeshes) {
				subMesh.Clear();
			}
			if (newMesh.BlendShapeCount != originalMesh.BlendShapeCount) {
				throw new Exception($"Assertion failed: {newMesh.BlendShapeCount} == {originalMesh.BlendShapeCount}");
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
			DebugHelpers.Debug($"Splitting PointSubmesh {subMeshIndex} with {pointCount} points...");
			for (int i = 0; i < pointCount; i++) {
				Point point = sourcePoints.GetPoint(i);
				UVTileKey key = UVTileKey.FromPoint(point.Vertex.GetUV(uvChannel));
				MeshX targetMesh = GetMeshForTile(key);
				PointSubmesh targetPoints = targetMesh.TryGetSubmesh<PointSubmesh>(subMeshIndex);
				targetPoints.AddPoint(point.VertexIndex);
			}
		}

		//reused triangle lists to avoid allocations
		List<MixedUVTriangle> halfCutU = [], fullCutU = [], halfCutV = [], fullCutV = [];
		List<(UVTileKey, MixedUVTriangle)> tiledUVs = [];
		Dictionary<(UVTileKey, VertexMixture), (Vertex vertex, MixingResult result)> knownVertexMixtures = [];
		void MapTriangleSubmesh(TriangleSubmesh sourceTriangles) {
			int subMeshIndex = sourceTriangles.Index;
			int triangleCount = sourceTriangles.Count;
			DebugHelpers.Debug($"Splitting TriangleSubmesh {subMeshIndex} with {triangleCount} triangles...");
			for (int i = 0; i < triangleCount; i++) {
				Triangle sourceTriangle = sourceTriangles.GetTriangle(i);
				var v0 = sourceTriangle.Vertex0;
				var v1 = sourceTriangle.Vertex1;
				var v2 = sourceTriangle.Vertex2;
				var uv0 = v0.GetUV(uvChannel);
				var uv1 = v1.GetUV(uvChannel);
				var uv2 = v2.GetUV(uvChannel);
				MixedUVTriangle fullUVTriangle = new(uv0, uv1, uv2);
				if (DebugHelpers.PrintTrace) {
					DebugHelpers.Trace($"Triangle {i} ({v0.Index} {v1.Index} {v2.Index})");
				}

				tiledUVs.Clear();
				UVSplitting.Algorithm.SplitIntoTiles(fullUVTriangle, tiledUVs, halfCutU, fullCutU, halfCutV, fullCutV);

				foreach ((UVTileKey key, MixedUVTriangle tileTriangle) in tiledUVs) {
					MeshX targetMesh = GetMeshForTile(key);

					TriangleSubmesh targetTriangles = targetMesh.TryGetSubmesh<TriangleSubmesh>(subMeshIndex);
					var vTarget0 = targetMesh.GetVertex(v0.Index);
					var vTarget1 = targetMesh.GetVertex(v1.Index);
					var vTarget2 = targetMesh.GetVertex(v2.Index);

					MixingResult TryGetMappedVertex(MixedUV uv, out Vertex vertex) {
						if (DebugHelpers.PrintTrace) {
							DebugHelpers.Trace($"GetMappedVertex {uv}");
						}
						if (uv.Mixture == new float3(1, 0, 0)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v0 {vTarget0.Position} {vTarget0.UV0}");
							}
							vertex = vTarget0;
							return MixingResult.Ok;
						} else if (uv.Mixture == new float3(0, 1, 0)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v1 {vTarget1.Position} {vTarget1.UV0}");
							}
							vertex = vTarget1;
							return MixingResult.Ok;
						} else if (uv.Mixture == new float3(0, 0, 1)) {
							if (DebugHelpers.PrintTrace) {
								DebugHelpers.Trace($"GetMappedVertex v2 {vTarget2.Position} {vTarget2.UV0}");
							}
							vertex = vTarget2;
							return MixingResult.Ok;
						}
						(UVTileKey, VertexMixture) mixtureKey = (key, new VertexMixture(new int3(v0.Index, v1.Index, v2.Index), uv.Mixture).Canonalized());
						if (knownVertexMixtures.TryGetValue(mixtureKey, out var knowResult)) {
							//avoided creating a duplicated vertex
							vertex = knowResult.vertex;
							DebugHelpers.Trace($"GetMappedVertex known mixture {vertex.Position} {vertex.UV0} {knowResult.result}");
							return knowResult.result;
						}

						vertex = targetMesh.AddVertex();
						var result = Mixer.MixVertices(uv.Mixture, vTarget0, vTarget1, vTarget2, vertex);
						knownVertexMixtures.Add(mixtureKey, (vertex, result));
						if (DebugHelpers.PrintTrace) {
							DebugHelpers.Trace($"GetMappedVertex new mixture {vertex.Position} {vertex.UV0} {result}");
						}
						return result;
					}

					MixingResult triangleResult = MixingResult.Ok;
					triangleResult |= TryGetMappedVertex(tileTriangle.UV0, out var outputVertex0);
					triangleResult |= TryGetMappedVertex(tileTriangle.UV1, out var outputVertex1);
					triangleResult |= TryGetMappedVertex(tileTriangle.UV2, out var outputVertex2);
					targetTriangles.AddTriangle(outputVertex0, outputVertex1, outputVertex2);
					if (DebugHelpers.PrintTrace) {
						DebugHelpers.Trace($"Added Triangle ({outputVertex0.Position} {outputVertex0.UV0})({outputVertex1.Position} {outputVertex1.UV0})({outputVertex2.Position} {outputVertex2.UV0})");
					}
					if (triangleResult != MixingResult.Ok){
						if ((triangleResult & MixingResult.TooManyBones) != 0) {
							UniLog.Warning($"Mixing vertices {sourceTriangle.Vertex0Index}, {sourceTriangle.Vertex1Index} and {sourceTriangle.Vertex2Index} resulted in bone bindings with more than the maximum number of 4 bones. This may cause degraded visuals.");
						}
						//TODO: add to list, then output status to the user
					}
				}
			}
		}

		foreach (Submesh subMesh in originalMesh.Submeshes) {
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

		DebugHelpers.Debug($"Stripping unused vertices...");


		foreach ((UVTileKey key, MeshX mesh) in mappedMeshes.ToList()) {
			mappedMeshes[key] = Cleanup.StripUnusedVertices(mesh);
		}

		string tileDescription= string.Join("", mappedMeshes.Select((e) => $"\n {e.Key.U},{e.Key.V}: {e.Value.VertexCount} vertices, {e.Value.TotalTriangleCount} triangles")); ;
		DebugHelpers.Debug($"Splitting resulted in {mappedMeshes.Count} meshes. Tiles:{tileDescription}");

		return mappedMeshes;
	}
}
