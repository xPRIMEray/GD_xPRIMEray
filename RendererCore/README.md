# RendererCore
This folder holds the renderer-agnostic core for the project.
It will contain shared types and services used by all rendering backends.
Subfolders separate concerns like scene snapshots, fields, and integrators.
Nothing here should depend on Godot-specific APIs.
The goal is to keep this layer portable and testable.
