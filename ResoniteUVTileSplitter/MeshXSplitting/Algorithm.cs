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
	/// <param name="original">mesh to be split</param>
	/// <param name="uvChannel">UV channel to split (allowed values: 0 to 3 inclusive)</param>
	/// <returns>A mapping from UV tiles to their corresponding meshes</returns>
	/// <exception cref="NotSupportedException">If submeshes other than PointSubmesh or TriangleSubmesh are found.</exception>
	internal static Dictionary<UVTileKey, MeshX> SplitMeshByUVTiles(MeshX original, int uvChannel) {
		DebugHelpers.Debug($"Splitting a Mesh with {original.TotalPointCount} vertices and {original.TotalTriangleCount} triangles...");
		if (uvChannel < 0 || uvChannel > 3) {
			throw new ArgumentOutOfRangeException(nameof(uvChannel), uvChannel, "Only UV-Channels 0 to 3 inclusive are supported.");
		}
		Dictionary<UVTileKey, MeshX> mappedMeshes = [];

		MeshX GetMeshForTile(UVTileKey key) {
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
		Dictionary<(UVTileKey, VertexMixture), Vertex> knownVertexMixtures = [];
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

					Vertex GetMappedVertex(MixedUV uv) {
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
						Mixer.MixVertices(uv.Mixture, v0, v1, v2, uvChannel, key.UV, vertex);
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

		string tileDescription= string.Join("", mappedMeshes.Select((e) => $"\n {e.Key.U},{e.Key.V}: {e.Value.VertexCount} vertices, {e.Value.TotalTriangleCount} triangles")); ;
		DebugHelpers.Debug($"Splitting resulted in {mappedMeshes.Count} meshes. Tiles:{tileDescription}");

		return mappedMeshes;
	}
}
