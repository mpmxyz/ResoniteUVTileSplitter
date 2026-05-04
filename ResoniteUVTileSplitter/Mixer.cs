using Elements.Assets;
using Elements.Core;

namespace ResoniteUVTileSplitter;
internal class Mixer {
	internal static T Mix<T>(float3 coefficients, T a, T b, T c) {
		T value = Coder<T>.Identity;
		float sum = 0;

		void Accumulate(float coefficient, T target) {
			if (coefficient != 0) {
				sum += coefficient;
				value = Coder<T>.Lerp(value, target, coefficient / sum);
			}
		}

		Accumulate(coefficients.x, a);
		Accumulate(coefficients.y, b);
		Accumulate(coefficients.z, c);
		
		return value;
	}
	internal static void MixVertices(float3 coefficients, Vertex a, Vertex b, Vertex c, int splitUVChannel, UVTileKey splitKey, Vertex result) {
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"MixVertices {coefficients}, {a.Position}/{b.Position}/{c.Position}, {a.UV0}/{b.UV0}/{c.UV0}");
		}
		var sourceMesh = a.Mesh;
		if (sourceMesh.HasColors) {
			result.Color = Mix(coefficients, a.Color, b.Color, c.Color);
		}
		if (sourceMesh.HasNormals) {
			result.Normal = Mix(coefficients, a.Normal, b.Normal, c.Normal);
		}
		result.Position = Mix(coefficients, a.Position, b.Position, c.Position);
		if (sourceMesh.HasTangents) {
			result.Tangent4 = Mix(coefficients, a.Tangent4, b.Tangent4, c.Tangent4);
		}
		for (int uvChannel = 0; uvChannel < 4; uvChannel++) {
			if (sourceMesh.HasUV_2D(uvChannel)) {
				float2 offset;
				if (uvChannel == splitUVChannel) {
					//make sure to move into the 0-1 UV range, 
					offset = splitKey.UV;
				} else {
					offset = new(0,0);
				}
				result.SetUV(uvChannel, Mix(coefficients, a.GetUV(uvChannel), b.GetUV(uvChannel), c.GetUV(uvChannel)) - offset);
			} else if (sourceMesh.HasUV_3D(uvChannel)) {
				result.SetUV_3D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)).xyz);
			} else if (sourceMesh.HasUV_4D(uvChannel)) {
				result.SetUV_4D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)));
			}
		}

		if (result.Mesh.HasBoneBindings) {
			int mixedBoneCount = MixBoneBindings(coefficients, a.BoneBinding, b.BoneBinding, c.BoneBinding, out var mixedBoneBinding);
			if (mixedBoneCount > 4) {
				//TODO: accumulate warnings into user information
				UniLog.Warning($"Mixing vertices {a.Index}, {b.Index} and {c.Index} resulted in {mixedBoneCount} bones which is more than 4.");
			}
			result.BoneBinding = mixedBoneBinding;
		}

		MixBlendshapes(coefficients, a, b, c, result);
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"MixVertices result {result.Position}, {result.UV0}");
		}
	}

	internal static int MixBoneBindings(float3 coefficients, BoneBinding a, BoneBinding b, BoneBinding c, out BoneBinding result) {
		Dictionary<int, float> boneWeights = [];
		for (int i = 0; i < 3; i++) {
			BoneBinding source = i switch {
				0 => a,
				1 => b,
				2 => c,
				_ => throw new NotImplementedException(),
			};
			float coefficient = i switch {
				0 => coefficients.x,
				1 => coefficients.y,
				2 => coefficients.z,
				_ => throw new NotImplementedException(),
			};
			for (int j = 0; j < 4; j++) {
				source.GetBinding(j, out var boneIndex, out var boneWeight);
				if (boneWeight > 0) {
					boneWeights[boneIndex] = boneWeights.GetValueOrDefault(boneIndex, 0) + boneWeight * coefficient;
				}
			}
		}
		result = new();
		foreach ((int bone, float weight) in boneWeights) {
			if (weight > 0) {
				result.AddBone(bone, weight);
			}
		}
		return boneWeights.Count;
	}

	internal static int MixBlendshapes(float3 coefficients, Vertex a, Vertex b, Vertex c, Vertex result) {
		//assumes a.Mesh == b.Mesh == c.Mesh
		MeshX sourceMesh = a.Mesh;
		MeshX targetMesh = result.Mesh;
		foreach (BlendShape sourceBlendshape in sourceMesh.BlendShapes) {
			string name = sourceBlendshape.Name;
			BlendShape targetBlendshape = targetMesh.GetBlendShape(name);
			for (int frameIndex = 0; frameIndex < sourceBlendshape.FrameCount; frameIndex++) {
				BlendShapeFrame sourceFrame = sourceBlendshape[frameIndex];
				BlendShapeFrame targetFrame = targetBlendshape[frameIndex];
				float3 positionDeltaA = sourceFrame.GetPositionDelta(a.Index);
				float3 positionDeltaB = sourceFrame.GetPositionDelta(b.Index);
				float3 positionDeltaC = sourceFrame.GetPositionDelta(c.Index);
				targetFrame.SetPositionDelta(result.Index, Mix(coefficients, positionDeltaA, positionDeltaB, positionDeltaC));
				if (sourceBlendshape.HasNormals) {
					float3 normalDeltaA = sourceFrame.GetNormalDelta(a.Index);
					float3 normalDeltaB = sourceFrame.GetNormalDelta(b.Index);
					float3 normalDeltaC = sourceFrame.GetNormalDelta(c.Index);
					targetFrame.SetNormalDelta(result.Index, Mix(coefficients, normalDeltaA, normalDeltaB, normalDeltaC));
				}
				if (sourceBlendshape.HasTangents) {
					float3 tangentDeltaA = sourceFrame.GetTangentDelta(a.Index);
					float3 tangentDeltaB = sourceFrame.GetTangentDelta(b.Index);
					float3 tangentDeltaC = sourceFrame.GetTangentDelta(c.Index);
					targetFrame.SetTangentDelta(result.Index, Mix(coefficients, tangentDeltaA, tangentDeltaB, tangentDeltaC));
				}
			}
		}

		return result.Mesh.BlendShapeCount;
	}
}
