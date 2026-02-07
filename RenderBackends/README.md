# RenderBackends
This folder will host concrete rendering backend implementations.
Each backend can target a different runtime or hardware path.
Backends consume RendererCore data but do not depend on Godot.
Keep backend-specific shaders, pipelines, and resource code here.
The intent is to allow multiple interchangeable renderers.
