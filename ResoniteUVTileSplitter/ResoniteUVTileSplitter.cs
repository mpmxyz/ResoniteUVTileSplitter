using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

using HarmonyLib;

using ResoniteModLoader;

namespace ResoniteUVTileSplitter;

public class ResoniteUVTileSplitter : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.1";
	public override string Name => "ResoniteUVTileSplitter";
	public override string Author => "mpmxyz";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/mpmxyz/ResoniteUVTileSplitter/";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> allowSharing = new("allowSharing", "Do you allow other users to use your mod instance?", () => false);

	public override void OnEngineInit() {
		Harmony harmony = new("mpmxyz.ResoniteUVTileSplitter");
		harmony.PatchAll();
	}

	/// <summary>
	/// Attaches buttons to trigger UV tile splitting to inspector panels.
	/// The buttons can be used by all users in a session as long as the creator of the inspector UI is still within the session.
	/// </summary>
	[HarmonyPatch(typeof(MeshRenderer), "BuildInspectorUI")]
	class MeshRenderer_BuildInspectorUI_Patch {
		public static void Postfix(MeshRenderer __instance, UIBuilder ui) {
			AddSplitButton(__instance, ui, 0);
			AddSplitButton(__instance, ui, 1);
			AddSplitButton(__instance, ui, 2);
			AddSplitButton(__instance, ui, 3);
		}

		private static void AddSplitButton(MeshRenderer __instance, UIBuilder uiBuilder, int uvChannel) {
			Button splitMeshButton = uiBuilder.Button($"Split UV Tiles (UV Channel: {uvChannel})");
			splitMeshButton.Slot.DestroyWhenUserLeaves(__instance.LocalUser);

			Slot contextMenuItems = splitMeshButton.Slot;
			ContextMenuSubmenu submenu = splitMeshButton.Slot.AttachComponent<ContextMenuSubmenu>(beforeAttach: (c) => {
				c.ItemsRoot.Target = contextMenuItems;
			});

			void TryRunAction(bool createDrives) {
				if (!splitMeshButton.Enabled) {
					//disabled state of main button acts as a gatekeeper for currently active processing
					return;
				}

				RunProcessingTask(__instance, splitMeshButton, (original, assetTarget, setLabel) => {
					return MeshRendererSplitting.Algorithm.SplitMeshByUVTilesTask(__instance, original, uvChannel, createDrives, assetTarget, setLabel);
				});
			}

			//listening to a field allows other users to execute the action
			ValueField<bool?> actionField = contextMenuItems.AttachComponent<ValueField<bool?>>(beforeAttach: (c) => {
				c.Value.OnValueChange += (field) => {
					bool? nextAction = field.Value;
					if (!nextAction.HasValue) {
						//only non-null values represent an action (null is default state)
						return;
					}
					bool createDrives = nextAction.Value;
					//reset field so it can listen to another action
					field.World.RunSynchronously(() => {
						field.Value = null;
					});

					if (!allowSharing.Value) {
						//prevent other users from running the action
						return;
					}

					TryRunAction(createDrives);
				};
			});
			void CreateSubmenuItem(bool createDrives) {
				Slot itemSlot = contextMenuItems.AddSlot($"Item");
				ContextMenuItemSource itemSource = itemSlot.AttachComponent<ContextMenuItemSource>(beforeAttach: (c) => {
					c.LabelText = createDrives ? "With Drives" : "Without Drives";
					c.Color.Value = createDrives ? colorX.Green : colorX.Red;
					c.CloseMenuOnPress.Value = true;
				});
				ButtonValueSet<bool?> syncedSubmenuAction = itemSlot.AttachComponent<ButtonValueSet<bool?>>(beforeAttach: (c) => {
					c.TargetValue.Target = actionField.Value;
					c.SetValue.Value = createDrives;
				});
				//Other users use the synced action. (may be disabled by mod config)
				syncedSubmenuAction.EnabledField.OverrideForUser(itemSlot.LocalUser, false);
				//Local user triggers action directly.
				itemSource.LocalPressed += (button, eventData) => { TryRunAction(createDrives); };
			}
			CreateSubmenuItem(true);
			CreateSubmenuItem(false);
		}
	}

	/// <summary>
	/// This method emulates MeshRenderer.RunProcessingTask to avoid a dependency on a private FrooxEngine implementation.
	/// It ensures button locking and communicates the status to the user.
	/// Additionally it prepares a slot for assets to be collected in. (avoids jumping to world at the start of the task)
	/// </summary>
	/// <param name="renderer">MeshRenderer the operation is applied on</param>
	/// <param name="button">Button that is updated with the state of processing</param>
	/// <param name="process">Action that is applied on the renderer</param>
	/// <returns>The task running the processing action</returns>
	private static Task RunProcessingTask(MeshRenderer renderer, IButton button, Func<MeshX, Slot, Action<string>, Task> process) {
		void SetLabel(string text) {
			DebugHelpers.Msg(text);
			button.RunSynchronously(() => {
				if (!button.IsRemoved) {
					button.LabelText = text;
				}
			}, true);
		}
		void SetAvailable(bool enabled) {
			button.RunSynchronously(() => {
				if (!button.IsRemoved) {
					button.Enabled = enabled;
				}
			}, true);
		}
		SetAvailable(false);
		Slot assetSlot = renderer.World.AssetsSlot.AddSlot(renderer.Slot.Name + " - UV Tiles");

		return renderer.StartGlobalTask(async () => {
			AssetRef<Mesh> meshRef = renderer.Mesh;
			object? mutex = new();
			try {
				IAssetProvider<Mesh> assetProvider = meshRef.Target;
				if (assetProvider == null) {
					SetLabel("ERROR: MeshRenderer has no mesh!");
					return;
				}

				SetLabel("Waiting for mesh to be loaded...");
				while (!assetProvider.IsAssetAvailable) {
					await default(NextUpdate);
				}

				SetLabel("Starting background task...");
				await default(ToBackground);

				SetLabel("Copying asset...");
				Mesh asset = assetProvider.Asset;
				await asset.RequestReadLock(mutex).ConfigureAwait(false);
				MeshX copy = new(asset.Data);
				asset.ReleaseReadLock(mutex);
				mutex = null;

				SetLabel("Processing...");
				await process(copy, assetSlot, SetLabel);
			} catch (Exception e) {
				SetLabel($"ERROR: {e} - {e.Message}");
				Error($"Unexpected exception during mesh processing:\n{e}");
			} finally {
				SetAvailable(true);
				if (mutex != null) {
					meshRef.Asset.ReleaseReadLock(mutex);
				}
			}
		});
	}
}
