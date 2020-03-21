# SuperScreenShotterVR
Extends SteamVR screenshot functionality

## Description
This application can do any or all of the following: use custom screenshots output folder, save uncompressed .PNG files, use the same screenshot chord as SteamVR, display the result in a notification, display a helpful viewfinder. Things to note are below.

![Application Window](https://i.imgur.com/t5EobC8.png)

## Output folder
If not chosen, it will be the folder the application is in. Screenshots are automatically put into subfolders based on the app ID.

## Submit to Steam
This will submit the screenshot to Steam so it gets included in the screenshot library for the running title in the desktop client. One caveat right now is that this will cause the default screenshot notification to be shown, regardless of settings in this application.

## Notifications and audio
You can choose to get a notification on capture, for it to contain a thumbnail of the result, and to play back an audio file.

![Notification with thumbnail](https://i.imgur.com/bzhFmbJ.png)

## Viewfinder
This is an overlay that is shown on a specific input, the app only comes with Index bindings as that is what it was developed on, but you can bind the actions to any controller using the SteamVR input binding interface.

![Viewfinder](https://i.imgur.com/yLfqear.jpg)

The outer frame shows the approximate crop of the screenshot. The roll indicator helps you  level with the horizon, and the pitch indicator helps you get vertical lines in architecture.

## Supersampling (experimental)
What this effectively does on screenshot capture is: enabled custom application render scaling in SteamVR, boosts it to 500%, captures the shot, and then reverts. Note that this does not work with all titles, observations:

* Unity appears to often respect live render scale changes
* Unreal appears to ignore it completely
* Source 2 simply runs its own dynamic render scale instead

To let the engine have time to change the render scale and save a screenshot before reverting there is a 100 ms delay before and after capture.

## Customization
The overlay graphics can be customized by overwriting the images in the resources folder. Right now screenshot output is assumed to be square, so the viewfinder border is square as well. With the way overlays work we set the size of the overlay to be the width of the screenshot field of view. Images are limited to a maximum of 1920x1080 in resolution by SteamVR.

The capture audio can be customized by replacing the waveform audio file in the resources directory.

For these changes to take effect, you will have to restart SteamVR and/or this application, to flush caches and reload files.

## Log
This is included mostly in debug or error reporting purposes, right now it will always show a few harmless errors as the way we initiate overlays causes those, it's on the todo list to fix.
