"""Deterministic broadleaf foliage. Run with Blender 5.2 --background --python this_file.

Leaves use an opaque curved silhouette, not alpha cards. Vertex R is the existing
LivingWind bend mask, G is per-leaf pigment variation, B is reserved, A is one.
The two LOD meshes share the authored branch/leaf descriptors and plant origin.
"""
import bpy
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / "Assets/_Project/07_Art/BetaPolish/Foliage"
DOC = Path(__file__).resolve().parent
TEXTURE = ROOT / "Assets/_Project/07_Art/FoliageStudy/LeafAlbedo.png"
OUT.mkdir(parents=True, exist_ok=True)


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def material(name, tint, texture=False):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*tint, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*tint, 1)
    shader.inputs["Roughness"].default_value = .7
    if texture:
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(TEXTURE), check_existing=True)
        mix = mat.node_tree.nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1
        mix.inputs[2].default_value = (*tint, 1)
        mat.node_tree.links.new(tex.outputs["Color"], mix.inputs[1])
        mat.node_tree.links.new(mix.outputs[0], shader.inputs["Base Color"])
    return mat


class Geometry:
    def __init__(self, height):
        self.vertices, self.faces, self.uvs, self.colors, self.slots = [], [], [], [], []
        self.height = height

    def vertex(self, position, uv, variation, flexibility=1):
        index = len(self.vertices)
        self.vertices.append(tuple(position))
        self.uvs.append(uv)
        weight = min(1, max(0, position.z / self.height)) ** 1.6
        self.colors.append((weight * flexibility, variation, .5, 1))
        return index

    def face(self, indices, slot):
        self.faces.append(indices)
        self.slots.append(slot)

    def leaf(self, desc, rows):
        base, axis, length, width, roll, variation = desc
        axis = axis.normalized()
        side = axis.cross(Vector((0, 0, 1)))
        if side.length < .01:
            side = Vector((1, 0, 0))
        side.normalize()
        normal = side.cross(axis).normalized()
        side = side * math.cos(roll) + normal * math.sin(roll)
        normal = side.cross(axis).normalized()
        rings = []
        for row in range(rows):
            # Extra perimeter samples at both rounded ends keep the silhouette soft.
            t = (1 - math.cos(math.pi * row / (rows - 1))) * .5
            radius = width * .5 * math.sin(math.pi * t) ** .52
            across = (0,) if row in (0, rows - 1) else (-1, 0, 1)
            ring = []
            for s in across:
                curve = length * (.14 * math.sin(math.pi * t) - .105 * t * t)
                curve -= s * s * width * .105
                p = base + axis * (length * t) + side * (s * radius) + normal * curve
                ring.append(self.vertex(p, (.5 + s * .47, .025 + t * .95), variation, .8 + .2 * t))
            rings.append(ring)
        a, b = rings[:2]
        self.face((a[0], b[1], b[0]), 0)
        self.face((a[0], b[2], b[1]), 0)
        for a, b in zip(rings[1:-2], rings[2:-1]):
            self.face((a[0], a[1], b[1], b[0]), 0)
            self.face((a[1], a[2], b[2], b[1]), 0)
        a, b = rings[-2:]
        self.face((a[0], a[1], b[0]), 0)
        self.face((a[1], a[2], b[0]), 0)

    def branch(self, points, radius, sides):
        rings = []
        for i, p in enumerate(points):
            t = i / (len(points) - 1)
            tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
            side = tangent.cross(Vector((0, 1, 0))).normalized()
            normal = side.cross(tangent).normalized()
            r = radius * (1 - .78 * t)
            ring = []
            for j in range(sides):
                angle = math.tau * j / sides
                pos = p + (side * math.cos(angle) + normal * math.sin(angle)) * r
                ring.append(self.vertex(pos, (j / sides, t), .4, .76))
            rings.append(ring)
        for a, b in zip(rings, rings[1:]):
            for j in range(sides):
                k = (j + 1) % sides
                self.face((a[j], b[j], b[k], a[k]), 1)
        self.face(tuple(rings[0]), 1)
        self.face(tuple(reversed(rings[-1])), 1)

    def object(self, name, materials, parent):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        color = mesh.color_attributes.new(name="Wind", type="FLOAT_COLOR", domain="CORNER")
        for mat in materials:
            mesh.materials.append(mat)
        for poly, slot in zip(mesh.polygons, self.slots):
            poly.use_smooth = True
            poly.material_index = slot
            for li in poly.loop_indices:
                vi = mesh.loops[li].vertex_index
                uv.data[li].uv = self.uvs[vi]
                color.data[li].color = self.colors[vi]
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = parent
        mesh.calc_loop_triangles()
        return obj, {
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.loop_triangles),
            "leaf_triangles": sum(1 for tri in mesh.loop_triangles if mesh.polygons[tri.polygon_index].material_index == 0),
            "bounds_blender_xyz": [[round(min(v[i] for v in self.vertices), 4) for i in range(3)],
                                   [round(max(v[i] for v in self.vertices), 4) for i in range(3)]],
            "wind_red_range": [round(min(c[0] for c in self.colors), 4), round(max(c[0] for c in self.colors), 4)],
        }


CONFIGS = [
    dict(name="RoundleafShrub_A", seed=1731, branches=9, height=.88, reach=.53,
         leaf_length=.29, leaf_width=.78, lean=.22, shape="rounded upright crown"),
    dict(name="RoundleafShrub_B", seed=5839, branches=8, height=.61, reach=.62,
         leaf_length=.32, leaf_width=.82, lean=.16, shape="low asymmetric spreading crown"),
    dict(name="ShoreBroadleaf_A", seed=3179, branches=7, height=.52, reach=.43,
         leaf_length=.37, leaf_width=.85, lean=.37, shape="layered broadleaf shore fans"),
]


def descriptors(config):
    rng = random.Random(config["seed"])
    branches, leaves = [], []
    for branch in range(config["branches"]):
        angle = branch * 2.399963 + rng.uniform(-.18, .18)
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        cross = Vector((-math.sin(angle), math.cos(angle), 0))
        inner = branch in (0, 3, 6)
        reach = config["reach"] * rng.uniform(.70, 1.05) * (.72 if inner else 1)
        height = config["height"] * rng.uniform(.74, 1.0) * (1.08 if inner else .85)
        offset = Vector((-.055, .02, 0)) if config["name"].endswith("B") else Vector((0, 0, 0))
        def point(t, offset=offset, radial=radial, reach=reach, height=height):
            return offset + radial * (reach * t ** 1.25) + Vector((0, 0, height * (1 - (1 - t) ** 1.7)))
        branches.append((point, .012 if config["name"].startswith("Round") else .009))
        # Four large paired tiers, followed by a soft terminal leaf.
        for node, t in enumerate((.29, .48, .67, .84)):
            for sign in (-1, 1):
                direction = (radial * rng.uniform(.45, .7) + cross * sign * rng.uniform(.65, .88)
                             + Vector((0, 0, config["lean"] + rng.uniform(-.09, .15)))).normalized()
                length = config["leaf_length"] * rng.uniform(.87, 1.13) * (1.09 - .21 * t)
                base = point(t) + cross * sign * .009
                leaves.append((base, direction, length, length * config["leaf_width"] * rng.uniform(.94, 1.06),
                               rng.uniform(-.27, .27), rng.uniform(.2, .92)))
        direction = (radial * .8 + Vector((0, 0, .25))).normalized()
        length = config["leaf_length"] * rng.uniform(.8, .94)
        leaves.append((point(.96), direction, length, length * config["leaf_width"], rng.uniform(-.12, .12), rng.uniform(.5, .9)))
    return branches, leaves


def create_asset(config, materials):
    root = bpy.data.objects.new(config["name"], None)
    bpy.context.collection.objects.link(root)
    branches, leaves = descriptors(config)
    report = {"name": config["name"], "seed": config["seed"], "shape": config["shape"], "lods": []}
    objects = []
    for lod in (0, 1):
        geo = Geometry(config["height"] + .16)
        count = 0
        for i, desc in enumerate(leaves):
            # Retain terminal and outer tiers; omit one interior pair on alternating fans.
            if lod == 1 and (i % 9 in (0, 1) or (i // 9 % 3 == 1 and i % 9 == 3)):
                continue
            geo.leaf(desc, 11 if lod == 0 else 5)
            count += 1
        for point, radius in branches:
            segments = 6 if lod == 0 else 3
            geo.branch([point(i / segments) for i in range(segments + 1)], radius, 4 if lod == 0 else 3)
        obj, stats = geo.object(config["name"] + "_LOD" + str(lod), materials, root)
        obj.hide_render = lod > 0
        stats.update(lod=lod, leaves=count, material_slots=["BetaFoliageLeaves", "BetaFoliageBark"])
        report["lods"].append(stats)
        objects.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root] + objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(OUT / (config["name"] + ".fbx")), use_selection=True,
                            object_types={"EMPTY", "MESH"}, axis_forward="-Z", axis_up="Y",
                            add_leaf_bones=False, bake_anim=False, apply_scale_options="FBX_SCALE_ALL",
                            use_custom_props=False, path_mode="STRIP", mesh_smooth_type="FACE", colors_type="LINEAR")
    return root, objects, report


def preview(assets):
    # Render gallery belongs to the source .blend only; exported FBX has no stage objects.
    for column, (root, objects, _) in enumerate(assets):
        root.location = ((column - 1) * 1.85, .3, .018)
        objects[1].hide_render = True
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.005))
    floor = bpy.context.object
    floor.name = "Preview stage (not exported)"
    floor.data.materials.append(material("GalleryWarmGrey", (.18, .21, .18)))
    world = bpy.context.scene.world
    world.use_nodes = True
    world.node_tree.nodes.get("Background").inputs[0].default_value = (.65, .74, .8, 1)
    world.node_tree.nodes.get("Background").inputs[1].default_value = .45
    bpy.ops.object.light_add(type="AREA", location=(-3, -4, 6))
    key = bpy.context.object
    key.data.energy = 1100
    key.data.shape = "DISK"
    key.data.size = 5
    key.rotation_euler = (Vector((0, 0, .4)) - key.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add(location=(3, -7.5, 5))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, .15, .35)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 6.15
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 860
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(DOC / "foliage-gallery-lod0.png")
    bpy.ops.render.render(write_still=True)
    for _, objects, _ in assets:
        objects[0].hide_render = True
        objects[1].hide_render = False
    scene.render.filepath = str(DOC / "foliage-gallery-lod1.png")
    bpy.ops.render.render(write_still=True)
    for root, objects, _ in assets:
        # Keep the saved source organized as a gallery; export happened at origin.
        objects[0].hide_render = False
        objects[1].hide_render = True
        objects[1].hide_set(True)


def main():
    clean_scene()
    materials = [material("BetaFoliageLeaves", (.78, .89, .72), texture=True),
                 material("BetaFoliageBark", (.19, .1, .044))]
    assets = [create_asset(config, materials) for config in CONFIGS]
    manifest = {
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "blender_version": bpy.app.version_string,
        "texture_reused_without_changes": str(TEXTURE.relative_to(ROOT)).replace("\\", "/"),
        "units": "meters; FBX -Z forward / Y up; root at ground contact",
        "wind_colors": {"r": "height^1.6 times leaf flexibility, stationary root", "g": "per-leaf variation .2-.92", "b": .5, "a": 1},
        "vertex_color_export_space": "LINEAR (numeric masks; do not apply sRGB conversion)",
        "runtime_shader": "Diceforge/Diorama/Soft Foliage; _AutoHeight=0; Cull Off; opaque geometry; existing LivingWind vertex contract",
        "recommended_lod_thresholds": [.06, .006],
        "assets": [asset[2] for asset in assets],
    }
    for asset in manifest["assets"]:
        assert 2000 <= asset["lods"][0]["triangles"] <= 4000, asset
        assert asset["lods"][1]["triangles"] < 1000, asset
    payload = json.dumps(manifest, indent=2)
    (DOC / "foliage-manifest.json").write_text(payload + "\n", encoding="utf-8")
    (OUT / "foliage-manifest.json").write_text(payload + "\n", encoding="utf-8")
    preview(assets)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(DOC / "BetaFoliage.blend"))
    print("BETA_FOLIAGE_COMPLETE " + json.dumps([{a[2]["name"]: [lod["triangles"] for lod in a[2]["lods"]]} for a in assets]))


if __name__ == "__main__":
    main()
