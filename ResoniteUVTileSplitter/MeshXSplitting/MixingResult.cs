namespace ResoniteUVTileSplitter;

/// <summary>
/// Diagnostic output of Mixer.MixVertices to indicate if it could be completed without issues.
/// At the moment there is only one error state. In theory it is possible to add more in the future. In this case the value is to be treated as a mask of error states.
/// </summary>
[Flags]
internal enum MixingResult {
	/// <summary>
	/// Mixing succeeded
	/// </summary>
	Ok = 0,
	/// <summary>
	/// There were too many bone bindings to mix. This likely results in corrupted visuals.
	/// </summary>
	TooManyBones = 1
}
