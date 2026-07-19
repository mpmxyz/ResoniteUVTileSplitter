using Elements.Assets;

namespace ResoniteUVTileSplitter.MeshXSplitting;

/// <summary>
/// This class contains post-processing methods that clean up meshes.
/// </summary>
internal static class Cleanup {
	/// <summary>
	/// Removes unused vertices from the mesh.
	/// This step is important to keep the assumptions of Renderite regarding the choice of index buffer format.
	/// </summary>
	/// <param name="mesh">The mesh to be optimized.</param>
	/// <returns>A mesh that does not contain unused vertices</returns>
	internal static MeshX StripUnusedVertices(MeshX mesh) {
		int originalVertexCount = mesh.VertexCount;

		GeometryMask mask = new(mesh);
		mask.InitializePrimitiveMasks();

		foreach (Submesh submesh in mesh.Submeshes) {
			int indexCount = submesh.IndicieCount;
			for (int i = 0; i < indexCount; i++) {
				int vertexIndex = submesh.RawIndicies[i];
				if (vertexIndex >= 0 && vertexIndex < mesh.VertexCount) {
					mask.MaskVertex(vertexIndex);
				}
			}
			for (int i = 0; i < submesh.Count; i++) {
				mask.MaskPrimitive(submesh.Index, i);
			}
		}

		MeshX newMesh = new();
		newMesh.Copy(mesh, null, mask);
		
		int nRemoved = originalVertexCount - newMesh.VertexCount;

		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"Removed {nRemoved} unused vertices.");
		}

		return newMesh;
	}
}
