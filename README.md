# YunYunLoader
A custom song loader for YunYun Syndrome!? Rhythm Psychosis. Allows you to load custom maps and music into the game through simple configuration files.

## Installation
Requires the latest stable release of [BepInEx 5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.4). After downloading, simply extract the contents of the archive into the game's root folder (where the main game executable is). If your system asks to overwrite any files, click "Yes".

After downloading and installing BepInEx, it is recommended to run the game at least once to generate all necessary files. Afterward, you can download the latest release of the mod in the **Releases** section, and place the `.dll` file in the `BepInEx/plugins` folder, located in the same game root folder.

After running the game again, the mod should load. If you would like a console alongside the game to view mod and Unity log output to confirm that the mod is loaded, head to `BepInEx/config/BepInEx.cfg` and set `Enabled = true` under `[Logging.Console]`.

## Using Custom Songs
After launching the game with the mod once, the mod will generate a `Songs` folder in the game's root folder. You should place all of your custom songs in this folder, with each song and its relevant files in its own sub-folder. If you add or remove songs from this folder while the game is running, you will need to relaunch the game in order for the changes to take effect.

When the mod loads custom songs, they should appear in the game's song list like any other vanilla song. Simply click on the custom song you want to play, choose a difficulty, and confirm your selection to begin playing!

A valid `Songs` directory may look like the following:
```
Songs /
| ExampleSong /
  | music.ogg
  | level1.json
  | song.json
| AnotherSong /
  | more_music.ogg
  | level1.json
  | level3.json
  | song.json
// and so on...
```

## Creating Custom Songs
As of writing, creating custom songs for the mod is somewhat of a tedious process, as a visual editor currently does not exist *(but will hopefully exist soon!)*. You can read more about creating custom songs in the [Wiki](https://github.com/EBro912/YunYunLoader/wiki) tab of this repository.

## Issues
If you find an issue with the mod, feel free to leave a bug report in the **Issues** tab of the repository. Make sure to be as detailed as possible, and leave console output of any errors or warnings you encounter. If you do not have the console enabled when the error occurs, you can view the most recent log output in `BepInEx/LogOutput.log` in any text editor.

## Contributing
If you would like to contribute to the mod, simply clone the repository, make your changes, and open a **Pull Request** explaining your changes and why the change is necessary. When developing, it is highly recommend to modify the `<LibRoot>` property in the project's `.csproj` file with the path to your game's installation. This will automatically import all of the DLLs that the mod uses.
