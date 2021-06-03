# Usage

1. Equip your hammer,
2. Go within build range of a build station (e.g. a workbench),
3. Press B (default - can be changed in config) to activate the build camera.
4. The camera is now disconnected from your avatar. Look around and move as usual (e.g. using mouse and keyboard); use jump (Space) to go up and stealth (Control) to go down. Hold run (Shift) to move the camera faster. Gamepad users: left trigger and right trigger move the camera up and down. Right joystick turns the camera. Left joystick pans the camera left, right, forward, and backward.
5. Build (left click) and choose items to build (right click) as usual.

## Other details

  * This mod changes how far from a work station you're able to build to several times the game default, see the configuration options Distance_Can_Build_From_Avatar and Distance_Can_Build_From_Workbench. There was much demand for this feature.
  * Deactivate build mode by unequipping the hammer or pressing B or R (the keybind for "hide" weapons).
  * Also works with the hoe and the cultivator.
  * The camera must stay within the build area, although the range is configurable with Camera_Range_Multiplier.

# Installation

1. Install [BepInEx for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
2. Place `Build Camera.dll` in your BepInEx plugins directory, like this: `Steam\steamapps\common\Valheim\BepInEx\plugins\Build Camera.dll`.

# Configuration

Start the game with the plugin installed, then edit the file `\BepInEx\config\Build Camera.cfg`. There are several configurable options:

  * Distance_Can_Build_From_Avatar
  * Distance_Can_Build_From_Workbench
  * Toggle_build_mode hotkey
  * Camera_Move_Speed_Multiplier
  * Camera_Range_Multiplier
  * Move_With_Respect_To_World
  * Verbose_Logging

# Incompatible with

  * Valheim Plus first person mode. Sorry, I'm not sure how to make these compatible, both mods are taking over the camera.
  * Masa's FirstPerson mod, on R2ModManager

I've been told Build Camera is compatible with with kailen37's FirstPerson mod on nexusmods.com.

# Miscellaneous

This mod only needs to be installed on your client, not the server, and other clients do not have to install it (unless they want it too).

# Changelog

 * Version 1.5.1
    * Reduce spam when changing the two build distances (now uses LogDebug).
    * When changing the two build distances, be a little more agressive: change if current value is less than setting.
 * Version 1.5
    * Change build distances: "distance can build from avatar" and "distance can build from workbench"
    * Fix gamepad joystick.
 * Version 1.4
    * Stop ignoring the "Hide equipped tool/weapon" hotkey; allow it to put the tool away (and disable build camera).
 * Version 1.3
    * Build Mode is usable with Hoe and Cultivator.
    * Don't turn build camera when user has piece selection HUD visible.
 * Version 1.2
    * Fix camera panning (i.e. movement) speed: mousewheel does not change panning speed. Panning speed is about the same as walking speed. Hold shift to speed up. Add configuration option to change speed.
    * Don't allow looking so far up or down that camera is now upside down.
    * Camera turn speed respects user's Invert Mouse and Mouse Sensitivity options.
    * When entering build mode, we reset the view direction of the build camera, so that it matches the player's current view direction.
    * Add configurable option Move_With_Respect_To_World: When true, camera panning input (e.g. pressing WASD) moves the camera with respect to the world coordinates, not current camera view direction.
    * Don't move camera when user is in the menu, chat, etc.
    * Change Camera_Range_Multiplier default to 1 to provide an experience as close to vanilla as possible.
    * When the config option Verbose_Logging is true, explain 3 reasons why build mode is not activated.
 * Version 1.1
    * Fix: don't only show the sky.
 * Version 1.0.0.0
    * Initial release.

# Source code

Can be found at https://github.com/gittywithexcitement/ValheimBuildCamera .

Contributions are welcome:

   * bug fixes
   * adding compatibility with another mod
   * For new features, please contact me first, by opening an issue on github and explaining what you intend.

# Acknowledgements

Thanks to the excellent [Build Helper mod](https://www.nexusmods.com/valheim/mods/53) for showing me how to change "distance can build from avatar" and "distance can build from workbench".
