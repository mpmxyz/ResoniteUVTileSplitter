using Elements.Core;

namespace ResoniteUVTileSplitter.MeshXSplitting;

/// <summary>
/// This object represents a mixture of vertices from a given mesh.
/// It is used as a key to avoid creating duplicate vertices from the same mixture.
/// </summary>
/// <param name="VertexIDs">Vertices involved in the mixture</param>
/// <param name="Coefficients">Mixture coefficients</param>
internal record struct VertexMixture(int3 VertexIDs, float3 Coefficients) {
	/// <summary>
	/// This joins duplicated vertex ids and sorts them so that the mixtures with the same components and rations result in the same mixture value.
	/// </summary>
	/// <returns>The canonical version of this mixture.</returns>
	internal readonly VertexMixture Canonalized() {
		static VertexMixture CreateUnified(int3 ids, float3 coefficients) {
			if (ids.x == ids.y) {
				if (ids.y == ids.z) {
					return new VertexMixture(ids.x__, new float3(coefficients.x + coefficients.y + coefficients.z));
				} else {
					return new VertexMixture(ids.xz_, new float3(coefficients.x + coefficients.y, coefficients.z));
				}
			} else if (ids.y == ids.z) {
				return new VertexMixture(ids.xy_, new float3(coefficients.x, coefficients.y + coefficients.z));
			} else {
				return new VertexMixture(ids, coefficients);
			}
		}

		//sort by Vertex ID & unify equal IDs
		if (VertexIDs.x < VertexIDs.y) {
			//_x_y_
			if (VertexIDs.x < VertexIDs.z) {
				//x_y_
				if (VertexIDs.y < VertexIDs.z) {
					//xyz
					return CreateUnified(VertexIDs.xyz, Coefficients.xyz);
				} else {
					//xzy
					return CreateUnified(VertexIDs.xzy, Coefficients.xzy);
				}
			} else {
				//zxy
				return CreateUnified(VertexIDs.zxy, Coefficients.zxy);
			}
		} else {
			//_y_x_
			if (VertexIDs.x < VertexIDs.z) {
				//yxz
				return CreateUnified(VertexIDs.yxz, Coefficients.yxz);
			} else {
				//_y_x
				if (VertexIDs.y < VertexIDs.z) {
					//yzx
					return CreateUnified(VertexIDs.yzx, Coefficients.yzx);
				} else {
					//zyx
					return CreateUnified(VertexIDs.zyx, Coefficients.zyx);
				}
			}
		}
	}
}
