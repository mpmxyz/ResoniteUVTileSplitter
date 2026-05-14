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
	/// <returns>A mesh that does not contain unused vertices (currently the same object, may be a new object in the future)</returns>
	internal static MeshX StripUnusedVertices(MeshX mesh) {
		GeometryMask mask = new(mesh);
		for (int i = 0; i < mesh.VertexCount; i++) {
			mask.ClearVertex(i);
		}
		foreach (Submesh submesh in mesh.Submeshes) {
			int indexCount = submesh.IndicieCount;
			for (int i = 0; i < indexCount; i++) {
				int vertexIndex = submesh.RawIndicies[i];
				if (vertexIndex >= 0 && vertexIndex < mesh.VertexCount) {
					mask.MaskVertex(vertexIndex);
				}
			}
		}
		int nRemoved = 0;
		for (int i = mesh.VertexCount - 1; i >= 0; i--) {
			if (!mask.IsVertexMasked(i)) {
				int removeTo = i;
				while(i > 0 && !mask.IsVertexMasked(i - 1)) {
					i--;
				}
				int count = removeTo - i + 1;
				nRemoved += count;
				mesh.RemoveVertices(i, count);
			}
		}

		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"Removed {nRemoved} unused vertices.");
		}

		return mesh;


		//mask.MaskPrimitivesFromVertices();
		//var clone = new MeshX(mesh);
		//clone.Copy(mesh, null, mask);
		//return clone;
	}
}
