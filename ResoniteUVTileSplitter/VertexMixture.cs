using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct VertexMixture(int3 VertexIDs, float3 Coefficients) {
	readonly int VertexCount {
		get {
			if (Coefficients.x == 0) {
				return 0;
			} else if (Coefficients.y == 0) {
				return 1;
			} else if (Coefficients.z == 0) {
				return 2;
			} else {
				return 3;
			}
		}
	}

	internal VertexMixture Canonalized() {
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
