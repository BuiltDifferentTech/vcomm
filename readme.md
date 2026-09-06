![alt text](https://vcomm.publiczeus.com/media/logo.png)
# VCOMM

VCOMM is a voice recognition software used to assist in commanding units/AI in video games.

VCOMM allows you to set macros to voice commands that could give you an extra edge when playing your favorite games. VComm is an external application rather than a mod and so can be used on any program/app or game!

## Installation
Download VCOMM directly from the site [HERE](https://vcomm.publiczeus.com).
Once downloaded you can run the VComm.exe and boom, gaming is much cooler!

## Usage
VCOMM has a user-friendly UI that makes configuration and usage easy.

Open **Settings → New Pack** to build a VPack visually. Trigger phrases can be typed or captured from the microphone. Add actions individually or record a keyboard sequence; modifier keys recorded as held remain down through every following step and release safely when the macro ends. Existing packs can be opened with **Edit Active Pack**. The builder validates each command before saving it to the `VPacks` folder.

The ideal setup is a quiet place with a good quality* microphone without using the Push-To-Talk feature. 
### Objects
VComm makes use of several objects/classes to configure usage.
#### VPack
A VPack is a package of VRequests, this is used to save your configurations. (used primarily to save games)
```javascript
{
    "name": "Ready Or Not",
    "author": "Devlin",
    "vRequests": [{
            "phrases": [
                "red",
                "red team"
            ],
            "macro": {
                "msToWait": 0,
                "keycodes": [
                    "{F7}"
                ]
            }
        },
        {
            "phrases": [
                "blue",
                "blue team"
            ],
            "macro": {
                "msToWait": 0,
                "keycodes": [
                    "{F6}"
                ]
            }
        }
    ]
}
```
#### VRequests
A VRequest contains the Phrases that the VComm Voice Engine will recognize and the Macro that should be executed once any of the Phrases are recognized.

```javascript
{
    "vRequests": [{
            "phrases": [
                "red",
                "red team"
            ],
            "macro": {
                "msToWait": 0,
                "keycodes": [
                    "{F7}"
                ]
            }
        },
        {
            "phrases": [
                "blue",
                "blue team"
            ],
            "macro": {
                "msToWait": 0,
                "keycodes": [
                    "{F6}"
                ]
            }
        }
    ]
}
```
#### Macro
A Macro contains the Key Codes that are executed when the attached Phrase is recognized. It also allows for a ms delay to be added. (delay is awaited after each keycode has been sent.)

```javascript
{
    "macro": {
        "msToWait": 0,
        "keycodes": [
            "{F6}"
        ]
    }
}
```

Macro Key Codes can be any key contained within curly brackets (for example, "{F}", "{Q}" or "{/}") or plain names (for example, "{CAPSLOCK}", "{DEL}" or "{DELETE}").
[Learn more about Key Codes](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys)

Prefix an input with `(HOLD)` to keep it down until the rest of that macro has run. Held inputs are released safely in reverse order. For example, the following holds the middle mouse button, presses X, then releases the middle mouse button:

```javascript
"keycodes": [
    "(HOLD){MMB}",
    "{X}"
]
```

Mouse inputs include `{LMB}`, `{RMB}`, `{MMB}`, `{MOUSE4}`, `{MOUSE5}`, `{WHEELUP}`, `{WHEELDOWN}`, `{WHEELLEFT}` and `{WHEELRIGHT}`. Keyboard inputs may also be held, so combinations such as `(HOLD){CTRL}`, `(HOLD){SHIFT}`, `{S}` work as expected. Braces are optional and input names are case-insensitive.

The ms delay is particularly useful in scenarios where the program loads, as each key press is consecutive (and non-concurrent/awaited) if we require a pause before executing the next key press we can apply it here. 

## Contributing

Pull requests are welcome. For major changes, please open an issue first
to discuss what you would like to change.

## License

[MIT](https://choosealicense.com/licenses/mit/)
