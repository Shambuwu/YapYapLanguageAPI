# YapYapLanguageAPI
[![Ask DeepWiki](https://devin.ai/assets/askdeepwiki.png)](https://deepwiki.com/Shambuwu/YapYapLanguageAPI)

YapYapLanguageAPI is a BepInEx mod for the game YAPYAP that allows players and modders to add custom languages for voice commands. It provides a framework for registering new language packs, including voice recognition models and grammar files, and integrates them into the game's settings menu.

## Features

*   **Extensible Language Support:** Add any language supported by the Vosk Speech Recognition Toolkit.
*   **Simple Configuration:** Define new languages through a straightforward `languages.json` file.
*   **Seamless Integration:** Custom languages appear directly in the in-game voice language settings dropdown.
*   **Flexible File Structure:** Organize your custom language model and localisation files as you see fit.

## Installation

1.  Ensure you have [BepInEx](https://github.com/BepInEx/BepInEx) installed for YAPYAP.
2.  Download the latest release of `YapYapLanguageAPI.dll` from the releases page.
3.  Place the `YapYapLanguageAPI.dll` file inside your `YAPYAP/BepInEx/plugins` folder.
4.  Launch the game once to generate the necessary configuration files and directories (`languages.json`, `Models`, `Localisation`).

## How to Add a New Language

Adding a new language involves providing a Vosk model, a localisation file with command grammar, and a definition in `languages.json`.

### 1. File and Folder Structure

Upon first run, the API will create the following structure inside your `BepInEx/plugins/` directory:

```
BepInEx/plugins/
├── YapYapLanguageAPI.dll
├── languages.json
├── Models/
└── Localisation/
```

-   **`Models/`**: This directory is where you should place your language-specific Vosk model folders.
-   **`Localisation/`**: This directory is for your text files that define the vocabulary for voice commands.
-   **`languages.json`**: This file is used to register your new language with the game.

### 2. Add Language Files

Let's add a Dutch language pack as an example:

1.  **Vosk Model**: Download or create a Vosk model for your desired language. Place it in a new subfolder within the `Models` directory.
    -   Example: `BepInEx/plugins/Models/vosk-model-small-nl-0.22/`

2.  **Localisation File**: Create a `.txt` file in the `Localisation` directory. This file will map game actions to spoken words.
    -   Example: `BepInEx/plugins/Localisation/dutch.txt`

The format for the localisation file is `key :: word1 word2 word3`. Each line defines a command. The key corresponds to an in-game action, and the words to the right are the voice triggers for that action.

Example content for `dutch.txt`:
```txt
SPELL_ARC_ASTRAL_EYES :: ASTRALE OGEN
SPELL_ARC_BLINK :: FLITS
SPELL_ARC_GRAB_ANA :: GRIJP ANA
SPELL_ARC_LUX_ANA :: LICHT ANA
SPELL_ARC_SWAP :: WISSEL
```

### 3. Configure `languages.json`

Open the `languages.json` file located in `BepInEx/plugins/`. If the file is empty or doesn't exist, the API will generate a default example. Add a new entry to the `languages` array for your custom language.

```json
{
  "languages": [
    {
      "id": "dutch",
      "displayName": "Nederlands (Community)",
      "systemLanguage": "Dutch",
      "modelFolder": "Models/vosk-model-small-nl-0.22",
      "localisationFile": "Localisation/dutch.txt",
      "fallback": "english"
    }
  ]
}
```

#### Field Explanations:
-   `"id"`: A unique, lowercase string to identify your language pack.
-   `"displayName"`: The name that will be displayed in the game's settings menu (e.g., "Nederlands (Community)").
-   `"systemLanguage"`: The corresponding `UnityEngine.SystemLanguage` enum name. This is used for internal mapping. If unsure, you can fall back to `"English"`.
-   `"modelFolder"`: The path to your Vosk model folder, relative to the `BepInEx/plugins/` directory.
-   `"localisationFile"`: The path to your grammar/vocabulary `.txt` file, relative to the `BepInEx/plugins/` directory.
-   `"fallback"`: The language `id` to use if your custom language fails to load.

After completing these steps, launch the game. Your new language should appear in the voice language dropdown in the settings menu.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.