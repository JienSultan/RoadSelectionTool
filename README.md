# RoadSelectionTool

The RoadTool hasn't been updated to the new DevkitSelection System, this Module changes that.

## Features

- Selection box for road nodes
- Delete every selected node
- Move every selected node with the primary node as the pivot
- Integrates well with EditorHelper and Breakdown.

https://github.com/user-attachments/assets/878d50aa-d1bf-4086-bf1c-8fd86d26ca31

## How It Works

When the module is initialized:
- Subscribes to `Level.onPostLevelLoaded`.
- Once the level is loaded in editor mode, creates a `GameObject` called `RoadSelectionTool` and attaches the  RoadSelectionTool` script to it.

## Installation

- **Download** the latest release from releases. 

## Dependencies

- Harmony
- Breakdown.Tools (For easy Harmony usage)
- ReflectionTools
