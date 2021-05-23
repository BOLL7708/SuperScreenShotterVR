# SuperScreenShotterVR
Extends SteamVR screenshot functionality, download the latest release [here](https://github.com/BOLL7708/SuperScreenShotterVR/releases).

## Description
This application can do any or all of the following: use custom output folder, save uncompressed .PNG files, use the same screenshot chord as SteamVR, display the result in a notification, display a helpful viewfinder. Things to note are listed below.

![Application Window](https://i.imgur.com/hSPl9JK.png)

## Output folder
If not specifically set it will default to the application folder it is running in. Screenshots are automatically put into subfolders based on the current app ID from SteamVR.

## Submit to Steam
This will submit the screenshot to Steam so it gets included in the screenshot library for the running title in the desktop client. One caveat right now is that this will cause the default screenshot notification to be shown regardless of settings in this application.

## Capture on timer
Enable this to automatically take screenshots at an interval, these will end up in the folder for the title in a subfolder for the current date. These screenshots will not trigger notifications or audio.

## Delayed capture
This option will delay the screenshot with a set amount of seconds, and if the viewfinder is enabled it will show that during the delay. This is mostly meant to be used with controllers that have no good input to map for the viewfinder. There are separate actions to use in bindings for this if you want to be able to trigger it separately with certain devices.

## Notifications and audio
On capture you can choose to get a notification, if it should include a thumbnail of the result, and to play back an audio file.

![Notification with thumbnail](https://i.imgur.com/bzhFmbJ.png)

## Viewfinder
This is an overlay that is shown when a specific input is triggered, the app only comes with Index bindings as that is what it was developed on, but you can bind the action to any controller using the SteamVR input binding interface. The default binding for Index is: system button touch + trigger push.

![Viewfinder](https://i.imgur.com/yLfqear.jpg)

The outer frame shows the approximate crop of the screenshot. The roll indicator helps you  level with the horizon, and the pitch indicator helps you get vertical lines in architecture.

## Remote Server
When enabling the server it is possible to send and receive requests to and from the application. Namely, you can trigger screenshot taking and get the results back, including tagging the stored files. Below are the payload specifications, both requests and response are JSON encoded.

Send this to the server on the port defined, default address is `ws://localhost:8807`, which you can try from [here](https://www.websocket.org/echo.html). The delay is in whole seconds, and if enabled, the viewfinder will be shown during the delay just as with the manual delayed shots.
```js
{
  "nonce": "your-identifier",
  "tag": "tag-the-files",
  "delay": 2
}
```
On the same connection you will get these responses containing the result, the image is a Base64 encoded .PNG image.
```js
{
  "nonce": "your-identifier",
  "image": "base64-encoded-png-image"
}
```

## Customization
The overlay graphics can be customized by overwriting the images in the resources folder. Right now screenshot output is assumed to be square, so the viewfinder border is square as well. With the way overlays work we set the size of the overlay to be the width of the screenshot field of view. Images are limited to a maximum of 1920x1080 in resolution by SteamVR.

The capture audio can be customized by replacing the waveform audio file in the resources directory.

For these changes to take effect, you will have to restart SteamVR and/or this application, to flush caches and reload files.
