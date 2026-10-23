"""Blender FBX roundtrip checks; does not touch Unity scene or material assets."""
import bpy
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DOC = Path(__file__).resolve().parent
OUT = ROOT / "Assets/_Project/07_Art/BetaPolish/Foliage"
manifest = json.loads((OUT / "foliage-manifest.json").read_text(encoding="utf-8"))
reports = []
for asset in manifest["assets"]:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(OUT / (asset["name"] + ".fbx")), colors_type="LINEAR")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    assert len(meshes) == 2, (asset["name"], len(meshes))
    for expected in asset["lods"]:
        name = asset["name"] + "_LOD" + str(expected["lod"])
        obj = next(obj for obj in meshes if obj.name == name)
        mesh = obj.data
        mesh.calc_loop_triangles()
        assert len(mesh.loop_triangles) == expected["triangles"], name
        assert len(mesh.uv_layers) == 1, name
        assert len(mesh.color_attributes) == 1, name
        assert [mat.name.split(".")[0] for mat in mesh.materials] == expected["material_slots"], name
        colors = [value.color for value in mesh.color_attributes[0].data]
        assert all(all(math.isfinite(channel) for channel in color) for color in colors), name
        assert all(abs(color[2] - .5) < .0001 and abs(color[3] - 1) < .0001 for color in colors), name
        red = [min(color[0] for color in colors), max(color[0] for color in colors)]
        assert all(abs(a - b) < .0001 for a, b in zip(red, expected["wind_red_range"])), (name, red)
        degenerate = sum(1 for tri in mesh.loop_triangles if tri.area < 1e-10)
        assert degenerate == 0, (name, degenerate)
        assert all(abs(v.normal.length - 1) < .0001 for v in mesh.vertices), name
        uv = [entry.uv for entry in mesh.uv_layers[0].data]
        assert all(-.0001 <= value.x <= 1.0001 and -.0001 <= value.y <= 1.0001 for value in uv), name
        assert all(abs(value - 1) < .0001 for value in obj.scale), (name, tuple(obj.scale))
        reports.append({"mesh": name, "triangles": len(mesh.loop_triangles), "uv_layers": len(mesh.uv_layers),
                        "wind_red_range": [round(value, 5) for value in red], "degenerate_triangles": degenerate,
                        "material_slots": len(mesh.materials), "unit_scale": list(obj.scale), "passed": True})
result = {"check": "FBX reimport with numeric LINEAR vertex colors", "all_passed": True, "meshes": reports}
(DOC / "foliage-validation.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print("BETA_FOLIAGE_VALIDATION " + json.dumps(result))
