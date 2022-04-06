# Unity Package Tool

Update package.json files in the Unity project to match assemblies referenced in the assembly definition.

## Disclaimer

This tool is provided freely but is tested only in my configuration to smoothen the workflow of updating my other assets packages. It is not meant to be used by others in other configurations, but could work for you, I don't know.

## Usage

Call **Update dependencies** from the Window/FredericRP menu item to update all of package.json files from the Assembly Definition files.

Steps done by the tool:
1. retrieve package name and version from project Packages
2. list all assembly definition files present in the Assets folder (for now, it only do so by searching assets that have a name starting by "FredericRP." so it should find only mine)
3. list package.json file from the assembly definition file location (using folder structure shown below)
4. update **dependencies** those package.json by finding the corresponding package name and version from the **references** listed in the assembly definition file (the references should not use GUIDs but assembly name only)
