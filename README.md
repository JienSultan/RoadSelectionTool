# RoadSelectionTool

The RoadTool hasn't been updated to the new DevkitSelection System, this Module changes that.

## Overview

**RoadSelectionTool** hooks into Unturned's editor environment and initializes a custom tool when the editor loads a level. It uses Harmony for patching.

## Features

- Selection box for road nodes
- Delete every selected node
- Move every selected node with the primary node as the pivot
- Integrates well with EditorHelper and Breakdown.

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
