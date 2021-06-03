using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;
using UnityEngine;

// Possible TODOS:

// maybe? don't open the map when in build mode.

// don't move camera when build menu is open and mouse is moving around.

// disallow and deactivate when swimming

// deactivate when taking damage

namespace Valheim_Build_Camera
{
	[BepInPlugin(MID, PluginName, VERSION)]
	[BepInProcess("valheim.exe")]
	[HarmonyPatch] // The empty annotation marks the class as a patch class.
								 // Harmony will consider the class and its methods.
	public class Valheim_Build_Camera : BaseUnityPlugin
	{
		private const string MID = "org.gittywithexcitement.plugins.valheim.buildCamera";
		private const string VERSION = "1.5.1";
		private const string PluginName = "Build Camera";

		private static ConfigFile configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "Build Camera.cfg"), true);

		private static ConfigEntry<float> distanceCanBuildFromAvatar
			= configFile.Bind("General", "Distance_Can_Build_From_Avatar", 100f,
				"Distance from your avatar that you can build or repair. (Valheim default is 8)");
		private static ConfigEntry<float> distanceCanBuildFromWorkbench
					= configFile.Bind("General", "Distance_Can_Build_From_Workbench", 100f,
						"Distance from nearest workbench/stonecutter/etc. that you can build or repair. (Valheim default is 20)");

		private static ConfigEntry<float> cameraRangeMultiplier
			= configFile.Bind("General", "Camera_Range_Multiplier", 1f,
				"Changes maximum range camera can move away from the build station. 1 means the build station's" +
				" range, 2 means twice the build station range, etc.");
		private static ConfigEntry<float> cameraMoveSpeedMultiplier
			= configFile.Bind("General", "Camera_Move_Speed_Multiplier", 3f,
				"Multiplies the speed at which the build camera pans (i.e. moves around).");
		private static ConfigEntry<bool> moveWithRespectToWorld
			= configFile.Bind("General", "Move_With_Respect_To_World", false,
				"When true, camera panning input (e.g. pressing WASD) moves the camera with respect to the " +
				"world coordinates. This means that turning the camera has no effect on the direction of " +
				"movement. For example, pressing W will always move the camera toward the world's 'North', " +
				"as opposed to the direction the camera is currently facing.");
		private static ConfigEntry<KeyboardShortcut> toggleBuildMode =
			configFile.Bind("Hotkeys", "Toggle_build_mode", new KeyboardShortcut(UnityEngine.KeyCode.B),
				"See https://docs.unity3d.com/ScriptReference/KeyCode.html for the names of all key codes. To " +
				"add one or more modifier keys, separate them with +, like so: Toggle_build_mode = B + LeftControl");
		private static ConfigEntry<bool> verboseLogging
			= configFile.Bind("General", "Verbose_Logging", false,
				"When true, increases verbosity of logging. Enable this if you're wondering why you're unable " +
				"to enable the Build Camera.");

		// This is how we "add" member variables to a class of the game.
		private static Dictionary<Player, bool> inBuildMode = new Dictionary<Player, bool>();

		private static BepInEx.Logging.ManualLogSource log;

		struct BuildCameraView
		{
			/// <summary>
			/// Turns the view left/right.
			/// </summary>
			public float yaw;

			/// <summary>
			/// Turns the view up/down.
			/// </summary>
			public float pitch;
		}

		/// <summary>
		/// The current pitch and yaw of the build camera.
		/// </summary>
		private static BuildCameraView buildCameraViewDirection =
			new BuildCameraView { pitch = 0, yaw = 0 };

		void Awake()
		{
			var harmony = new Harmony(MID);
			harmony.PatchAll();

			log = BepInEx.Logging.Logger.CreateLogSource(PluginName);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Player), "Awake")]
		public static void Player_Awake(ref float ___m_maxPlaceDistance)
		{
			if (___m_maxPlaceDistance < distanceCanBuildFromAvatar.Value)
			{
				log.LogDebug($"in Player_Awake, changing maxPlaceDistance from {___m_maxPlaceDistance} to {distanceCanBuildFromAvatar.Value}");
				___m_maxPlaceDistance = distanceCanBuildFromAvatar.Value;
			}
			else
			{
				log.LogDebug($"Not changing distanceCanBuildFromAvatar (AKA maxPlaceDistance) as it seems another mod has already changed it.");
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CraftingStation), "Start")]
		public static void CraftingStation_Start(ref CraftingStation __instance, ref float ___m_rangeBuild)
		{
			if (___m_rangeBuild < distanceCanBuildFromWorkbench.Value)
			{
				log.LogDebug($"in CraftingStation_Start, changing rangeBuild from {___m_rangeBuild} to {distanceCanBuildFromWorkbench.Value}");
				___m_rangeBuild = distanceCanBuildFromWorkbench.Value;
			}
			else
			{
				log.LogDebug($"Not changing distanceCanBuildFromWorkbench (AKA rangeBuild) as it seems another mod has already changed it.");
			}
		}

		static void LogWhenVerbose(string s)
		{
			if (verboseLogging.Value)
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, s);
				log.LogInfo(s);
			}
		}

		// Returns true when the player has Build Mode activated.
		static bool InBuildMode()
		{
			return (bool)Player.m_localPlayer && inBuildMode[Player.m_localPlayer];
		}

		static void DisableBuildMode()
		{
			inBuildMode[Player.m_localPlayer] = false;
		}

		static void EnableBuildMode()
		{
			inBuildMode[Player.m_localPlayer] = true;

			// When entering build mode, we reset the view direction of the build
			// camera, so that it matches the player's current direction. Thus, when
			// entering build mode, there is no (abrupt) change to the camera.
			var r = Player.m_localPlayer.m_eye.transform.rotation;
			buildCameraViewDirection.pitch = r.eulerAngles.x;
			buildCameraViewDirection.yaw = r.eulerAngles.y;

			Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Entering Build Mode.");
		}

		/// <summary>
		/// Returns true when player is the local player.
		/// </summary>
		/// <param name="player"></param>
		/// <returns></returns>
		static bool IsLocalPlayer(in Player player)
		{
			return (bool)Player.m_localPlayer && player == Player.m_localPlayer;
		}

		static readonly String[] toolNames
			= new String[] { "$item_hammer", "$item_cultivator", "$item_hoe" };

		/// <summary>
		/// Returns true when the item is a Build Camera-compatible tool such as hammer.
		/// </summary>
		/// <param name="itemData"></param>
		/// <returns></returns>
		static bool IsTool(in ItemDrop.ItemData itemData)
		{
			return toolNames.Contains(itemData?.m_shared.m_name)
				&& itemData?.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool;
		}

		/// <summary>
		/// Returns true when this player has a Build Camera-compatible tool such as
		/// hammer equipped.
		/// </summary>
		/// <param name="player"></param>
		/// <returns></returns>
		static bool ToolIsEquipped(in Player player)
		{
			// Tools are always equipped in the right hand.
			return IsTool(player.m_rightItem);
		}

		/// <summary>
		/// Returns true when build mode should be deactivated: the hammer is
    /// unequipped.
		/// </summary>
		/// <param name="player"></param>
		/// <returns></returns>
		static bool ShouldDeactivateBuildMode(in Player player)
		{
			return !ToolIsEquipped(player);
		}


		/// <summary>
		/// When game calls Player.SetLocalPlayer, DisableBuildMode
		/// </summary>
		[HarmonyPatch(typeof(Player), "SetLocalPlayer")]
		[HarmonyPostfix]
		static void InitializeNotInBuildMode()
		{
			DisableBuildMode();
		}

		/// <summary>
		/// Skip the game's Update when in build mode, to disallow actions like
		/// Interact(). Only allow UpdatePlacement.
		/// </summary>
		/// <param name="__instance"></param>
		/// <param name="__runOriginal"></param>
		[HarmonyPatch(typeof(Player), "Update")]
		[HarmonyPrefix]
		static void Update_Prefix_Player(ref Player __instance, ref bool __runOriginal)
		{
			if (IsLocalPlayer(__instance) && InBuildMode())
			{
				if (ShouldDeactivateBuildMode(__instance))
				{
					// The user might have unequipped the hammer (e.g. by using hotbar
					// items or unequipping via the inventory), so deactivate build mode.
					DisableBuildMode();

					__runOriginal = true;
				}
				else
				{
					__runOriginal = false;

					// Allow hotkeys so that hammer can be unequipped, which exits build mode
					// game source: Player.Update
					if (__instance.TakeInput())
					{
						if (Input.GetKeyDown(KeyCode.Alpha1))
						{
							__instance.UseHotbarItem(1);
						}
						if (Input.GetKeyDown(KeyCode.Alpha2))
						{
							__instance.UseHotbarItem(2);
						}
						if (Input.GetKeyDown(KeyCode.Alpha3))
						{
							__instance.UseHotbarItem(3);
						}
						if (Input.GetKeyDown(KeyCode.Alpha4))
						{
							__instance.UseHotbarItem(4);
						}
						if (Input.GetKeyDown(KeyCode.Alpha5))
						{
							__instance.UseHotbarItem(5);
						}
						if (Input.GetKeyDown(KeyCode.Alpha6))
						{
							__instance.UseHotbarItem(6);
						}
						if (Input.GetKeyDown(KeyCode.Alpha7))
						{
							__instance.UseHotbarItem(7);
						}
						if (Input.GetKeyDown(KeyCode.Alpha8))
						{
							__instance.UseHotbarItem(8);
						}

						if (ZInput.GetButtonDown("Hide") || ZInput.GetButtonDown("JoyHide"))
						{
							if ((__instance.GetRightItem() != null || __instance.GetLeftItem() != null)
								&& !__instance.InAttack())
							{
								__instance.HideHandItems();
							}
						}

						__instance.UpdatePlacement(true, Time.deltaTime);
					}
				}
			}
			else
			{
				__runOriginal = true;
			}
		}

		/// <summary>
		/// Enters Build Mode when the bound key is pressed and various conditions
		/// hold (e.g. hammer is equipped, player is close to a crafting station).
		///
		/// Exits Build Mode when the bound key is pressed.
		/// </summary>
		/// <param name="__instance"></param>
		[HarmonyPatch(typeof(Player), "Update")]
		[HarmonyPostfix]
		static void Update_Postfix_Player(ref Player __instance)
		{
			if (IsLocalPlayer(__instance) && toggleBuildMode.Value.IsDown() && __instance.TakeInput())
			{
				if (!InBuildMode() && ToolIsEquipped(__instance) && BuildStationInRange(__instance))
				{
					EnableBuildMode();
					return;
				}
				else if (InBuildMode())
				{
					DisableBuildMode();
					return;
				}
			}

			if (IsLocalPlayer(__instance) && toggleBuildMode.Value.IsDown())
			{
				if (!__instance.TakeInput())
				{
					LogWhenVerbose("Build Mode not enabled because chat, console, menu, inventory, map, or similar is open.");
				}
				else if (!ToolIsEquipped(__instance))
				{
					LogWhenVerbose("Build Mode not enabled because hammer is not equipped.");
				}
				else if (!BuildStationInRange(__instance))
				{
					LogWhenVerbose("Build Mode not enabled because no build station (e.g. workbench) is in range.");
				}
			}
		}


		/// <summary>
		/// Stops the player's avatar from moving when in build mode.
		/// </summary>
		/// <param name="__result"></param>
		/// <param name="__runOriginal"></param>
		[HarmonyPatch(typeof(PlayerController), "TakeInput")]
		[HarmonyPrefix]
		static void TakeInput_PlayerController(ref bool __result, ref bool __runOriginal)
		{
			if (InBuildMode())
			{
				__result = false;
				__runOriginal = false;
			}
			else
			{
				__runOriginal = true;
			}
		}

		// *** Camera ***

		/// <summary>
		/// A crafting station (e.g. workbench) near the player.
		/// </summary>
		struct NearbyCraftingStation
		{
			public Vector3 position;

			// Distance from the player
			public float distance;

			// The valid build range for this specific crafting station. Apparently
      // each station may have a different build range.
			public float rangeBuild;
		}

		/// <summary>
		/// Returns the NearestBuildStation. It can be a workbench or a stone cutting bench.
		/// </summary>
		/// <param name="playerOrCamera"></param>
		/// <returns></returns>
		static NearbyCraftingStation? GetNearestBuildStation(in Vector3 playerOrCamera)
		{
			if (CraftingStation.m_allStations.Count == 0)
			{
				return null;
			}
			else
			{
				List<NearbyCraftingStation> nearbyCraftingStations = new List<NearbyCraftingStation>();
				foreach (CraftingStation station in CraftingStation.m_allStations)
				{
					nearbyCraftingStations.Add(new NearbyCraftingStation
					{
						position = station.transform.position,
						distance = Vector3.Distance(station.transform.position, playerOrCamera),
						rangeBuild = station.m_rangeBuild
					});
				}

				return nearbyCraftingStations.OrderBy(x => x.distance).First();
			}
		}

		/// <summary>
		/// Returns true when a build/craft station is within range.
		///
		/// Note that range is determined by the specific build/craft station. The
		/// range is *not* multiplied by cameraRangeMultiplier. That is, we expect
		/// the player to enter build mode while within the build range of a
		/// crafting station. The camera may stray outside of the building range,
		/// but all pieces will (presumably) be placed within build range.
		/// </summary>
		/// <param name="player"></param>
		/// <returns></returns>
		static bool BuildStationInRange(in Player player)
		{
			var maybeStation = GetNearestBuildStation(player.transform.position);
			if (maybeStation is NearbyCraftingStation nearbyCraftingStation)
			{
				return nearbyCraftingStation.distance <= nearbyCraftingStation.rangeBuild;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Prevents the game camera from going out of range of the nearest build
		/// station (multiplied by the cameraRangeMultiplier).
		/// </summary>
		/// <param name="__instance"></param>
		static void StayNearWorkbench(ref GameCamera __instance)
		{
			var maybeStation = GetNearestBuildStation(__instance.transform.position);
			if (maybeStation is NearbyCraftingStation nearbyCraftingStation)
			{
				if (nearbyCraftingStation.distance
					> nearbyCraftingStation.rangeBuild * cameraRangeMultiplier.Value)
				{
					float error = nearbyCraftingStation.distance
						- nearbyCraftingStation.rangeBuild * cameraRangeMultiplier.Value;
					Vector3 towardStation = nearbyCraftingStation.position - __instance.transform.position;
					Vector3 correction = error * towardStation.normalized;
					__instance.transform.position = __instance.transform.position + correction;
				}
			}
			else
			{
				DisableBuildMode();
			}
		}

		/// <summary>
		/// Prevents the game camera from going below ground.
		/// </summary>
		/// <param name="__instance"></param>
		static void StayAboveGround(ref GameCamera __instance)
		{
			if (ZoneSystem.instance.GetGroundHeight(__instance.transform.position, out float height))
			{
				if (__instance.transform.position.y < height)
				{
					Vector3 p = __instance.transform.position;
					p.y = height;
					__instance.transform.position = p;
				}
			}
		}

		/// <summary>
		/// Updates buildCameraViewDirection (based on mouse and controller
		/// movement) and returns the pitch and yaw as a quanternion.
		/// </summary>
		/// <param name="dt"></param>
		/// <returns></returns>
		static Quaternion UpdateBuildCameraViewDirection(float dt)
		{
			// Game source: GameCamera.UpdateFreeFly(float dt)
			buildCameraViewDirection.yaw +=
				(PlayerController.m_mouseSens * Input.GetAxis("Mouse X"))
				+ (ZInput.GetJoyRightStickX() * 110f * dt);

			float polarity = PlayerController.m_invertMouse ? -1 : 1;
			float pitchUnchecked =
				buildCameraViewDirection.pitch -
				polarity
				* ((PlayerController.m_mouseSens * Input.GetAxis("Mouse Y"))
				- (ZInput.GetJoyRightStickY() * 110f * dt));
			buildCameraViewDirection.pitch = Mathf.Clamp(pitchUnchecked, -89f, 89f);

			return
				Quaternion.Euler(0f, buildCameraViewDirection.yaw, 0f) * Quaternion.Euler(buildCameraViewDirection.pitch, 0f, 0f);
		}

		/// <summary>
		/// Returns the untransformed (i.e. unaffected by current camera view
		/// direction) vector by which the GameCamera should move (i.e. pan).
		///
		/// Movement is based on keyboard (and controller) input.
		/// </summary>
		/// <returns></returns>
		static Vector3 UntransformedMovementVector(float dt)
		{
			// Game source: GameCamera.UpdateFreeFly(float dt)
			Vector3 vector = Vector3.zero;

			if (ZInput.GetButton("Left"))
			{
				vector -= Vector3.right;
			}
			if (ZInput.GetButton("Right"))
			{
				vector += Vector3.right;
			}
			if (ZInput.GetButton("Forward"))
			{
				vector += Vector3.forward;
			}
			if (ZInput.GetButton("Backward"))
			{
				vector -= Vector3.forward;
			}
			if (ZInput.GetButton("Jump"))
			{
				vector += Vector3.up;
			}
			if (ZInput.GetButton("Crouch"))
			{
				vector -= Vector3.up;
			}

			// I'm not sure if this is correct, but I'm going to normalize before
			// accounting for analog (joystick) movements. I would *not* want to
			// normalize after accounting for analog movement, because that would ruin
			// the whole point of having an analog input.
			vector.Normalize();

			vector += Vector3.up * ZInput.GetJoyRTrigger();
			vector -= Vector3.up * ZInput.GetJoyLTrigger();
			vector += Vector3.right * ZInput.GetJoyLeftStickX();
			vector += -Vector3.forward * ZInput.GetJoyLeftStickY();

			float baseSpeed =
				ZInput.GetButton("Run") ? Player.m_localPlayer.m_runSpeed : Player.m_localPlayer.m_walkSpeed;

			// When I use m_walkSpeed to move the build camera, it moves very slow,
			// much slower than the avatar's walking speed. m_walkSpeed is used in
			// Character.UpdateWalking, but that function is so dense, I don't
			// understand why the avatar walks faster. So we speed up the build
			// camera's movement by cameraMoveSpeedMultiplier.
			return vector * (dt * baseSpeed * cameraMoveSpeedMultiplier.Value);
		}

		/// <summary>
		/// Pans and rotates the camera based on user input (e.g. mouse movement and WASD).
		///
		/// Assumes that Build Mode is activated.
		/// </summary>
		/// <param name="dt"></param>
		/// <param name="__instance"></param>
		static void UpdateBuildCamera(float dt, ref GameCamera __instance)
		{
			// Game source: GameCamera.UpdateFreeFly(float dt)
			if (!Console.IsVisible() && Player.m_localPlayer.TakeInput() && !Hud.IsPieceSelectionVisible())
			{
				var untransformed = UntransformedMovementVector(dt);
				Vector3 moveBy = moveWithRespectToWorld.Value
					? untransformed : __instance.transform.TransformVector(untransformed);

				__instance.transform.position += moveBy;
				StayNearWorkbench(ref __instance);
				StayAboveGround(ref __instance);

				__instance.transform.rotation = UpdateBuildCameraViewDirection(dt);
			}
		}

		/// <summary>
		/// Decides if build camera is enabled and the game camera should free fly, or not.
		/// </summary>
		/// <param name="dt"></param>
		/// <param name="__instance"></param>
		/// <param name="__runOriginal"></param>
		[HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
		[HarmonyPrefix]
		static void UpdateCamera(float dt, ref GameCamera __instance, ref bool __runOriginal)
		{
			if (InBuildMode())
			{
				UpdateBuildCamera(dt, ref __instance);

				__runOriginal = false;
			}
			else
			{
				__runOriginal = true;
			}
		}
	}
}
