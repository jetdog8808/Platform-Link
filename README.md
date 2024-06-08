# Platform-Link
 A Udon script for VRChat to link players to moving platforms.
 
Video example: 

https://github.com/jetdog8808/Platform-Link/assets/36026251/1ac9db53-6c73-4a1d-83af-f32d295601a6

## How it works

* Colliders are only considered a platform if they have a Rigidbody.
* Will only link you when you are grounded to the collider, and its layer is selected in the "linkToLayers" variable.
* Collider and rigidbody include/exclude layers for the localplayer layer are accounted for.
* The player will move with the platforms rigidbody positon, rotation, and scale.
* You are able to walk and jump while linked to a platform.
* To unlink from the platform you can either walk off the platform or be above the "unLinkDistance" from the platform.
* There is a option to "inheriteVelocity" when unlinking from the platform, allowing you to keep your velocity from the platform.

## How to setup

* Add the [Platform link prefab](Platform%20Link.prefab) or [PlatformLink script](Scripts/PlatformLink.sc) to your scene.
* Select what layers to link to in the "linkToLayers" layer mask variable.
* Enable or Disable keeping a platforms velocity when unlinked with "inheriteVelocity" bool variable.
* Put how far above the platform you want to be to unlink from it in "unLinkDistance" float variable.

## Example

Made some moving platforms you can test [in this world](https://vrchat.com/home/world/wrld_765d95e9-c9f6-4906-af91-7968aa935ef6)

## Notes

Have noticed some issue when moving at very high speeds, but at that point most people would get motion sick. So is unlikely you will encounter issues unless you are pushing the system.

## Requirements

* [VRChat Sdk - Worlds](https://vrchat.com/home/download) 3.6.1+
* Unity 2022.3.22f1+
