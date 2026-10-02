"""Concept-directed, deterministic polish of the active First Trail scene.

Run with exec(compile(open(path, encoding='utf-8').read(), path, 'exec')).
Does not save, export, rebuild the scene, or change gameplay meshes.
"""
import bpy
import math
import random
from mathutils import Vector


PREFIX = 'Polish_'
SEED = 20261001


def base_name(obj):
    return obj.name.split('.')[0]


def world_bounds(obj):
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return tuple(min(p[i] for p in points) for i in range(3)), tuple(max(p[i] for p in points) for i in range(3))


def snapshot(obj):
    return tuple(value for row in obj.matrix_world for value in row), world_bounds(obj)


def material(name, color):
    # Reuse our own material on reruns, without recoloring pre-existing materials.
    existing = bpy.data.materials.get(name)
    if existing:
        return existing
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = .86
    return mat


def make_mesh(scene, name, vertices, faces, mats, indices=None, smooth=False):
    data = bpy.data.meshes.new(PREFIX + name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(PREFIX + name, data)
    scene.collection.objects.link(obj)
    for mat in mats:
        data.materials.append(mat)
    uv = data.uv_layers.new(name='UVMap')
    for face in data.polygons:
        face.use_smooth = smooth
        if indices:
            face.material_index = indices[face.index]
        for li in face.loop_indices:
            point = data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv = (point.x * .5, point.y * .5)
    return obj


def unlink_from_scene(scene, obj):
    # Objects shared by another open scene survive intact there.
    for collection in list(obj.users_collection):
        if collection == scene.collection or collection in scene.collection.children_recursive:
            collection.objects.unlink(obj)
    if obj.users == 0:
        data = obj.data if obj.type == 'MESH' else None
        bpy.data.objects.remove(obj)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)


def isolate_shared_collections(scene):
    """Keep unlinking from a duplicated scene from affecting other open scenes."""
    other_collections = {collection for other in bpy.data.scenes if other != scene
                         for collection in other.collection.children_recursive}

    def clone_tree(collection):
        copy = bpy.data.collections.new(collection.name + '_IslandPolishLocal')
        for obj in collection.objects:
            copy.objects.link(obj)
        for child in collection.children:
            copy.children.link(clone_tree(child))
        copy.hide_render = collection.hide_render
        copy.hide_viewport = collection.hide_viewport
        return copy

    def visit(parent):
        for child in list(parent.children):
            if child in other_collections:
                copy = clone_tree(child)
                parent.children.unlink(child)
                parent.children.link(copy)
            else:
                visit(child)

    visit(scene.collection)


def add_bevel(obj, width=.035):
    modifier = obj.modifiers.new('Soft limestone arris', 'BEVEL')
    modifier.width = width
    modifier.segments = 2
    modifier.affect = 'EDGES'
    modifier.angle_limit = .30
    modifier.limit_method = 'ANGLE'
    # Bake for reproducible FBX geometry and accurate raycasts.
    with bpy.context.temp_override(object=obj, active_object=obj,
                                   selected_objects=[obj], selected_editable_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=modifier.name)


def local_object(scene, obj):
    if any(other != scene and obj.name in other.objects for other in bpy.data.scenes):
        copy = obj.copy()
        copy.data = obj.data.copy()
        unlink_from_scene(scene, obj)
        scene.collection.objects.link(copy)
        return copy
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    return obj


def triangle_count(objects):
    return sum(sum(max(0, len(face.vertices)-2) for face in obj.data.polygons)
               for obj in objects if obj.type == 'MESH')


def stone(scene, rng, name, x, y, rx, ry, bottom, top, phase, mats):
    # Long tangent-facing slabs with an outward lean replace upright pillars.
    count = rng.choice([6, 7, 8])
    outline = [rng.uniform(.82, 1.16) for _ in range(count)]
    tilt_x, tilt_y = rng.uniform(-.11, .11), rng.uniform(-.09, .09)
    vertices, faces, indices = [], [], []
    layers = [(1.04, bottom, .10), (1.08, bottom + (top-bottom)*.46, .055), (.77, top, -.055)]
    for scale, z, shear in layers:
        for j in range(count):
            angle = phase + j * math.tau / count
            local_angle = j * math.tau / count
            tangent = math.cos(local_angle) * rx * outline[j] * scale
            radial = math.sin(local_angle) * ry * outline[j] * scale
            dx = -math.sin(phase)*tangent + math.cos(phase)*radial
            dy = math.cos(phase)*tangent + math.sin(phase)*radial
            vertices.append((x + dx + math.cos(phase)*shear,
                             y + dy + math.sin(phase)*shear,
                             z + dx*tilt_x + dy*tilt_y + rng.uniform(-.025, .025)))
    for layer in range(2):
        for j in range(count):
            faces.append((layer*count+j, layer*count+(j+1)%count,
                          (layer+1)*count+(j+1)%count, (layer+1)*count+j))
            indices.append(rng.choices([0, 1, 2], [7, 2, 2])[0])
    faces.extend([tuple(range(count*2, count*3)), tuple(reversed(range(count)))])
    indices.extend([1, 2])
    # Tangent/radial basis reverses ring handedness; keep export normals outward.
    faces = [tuple(reversed(face)) for face in faces]
    # Two small broken limestone flakes sit on the outer face of this slab.
    # They share its mesh and material slots, so object count stays unchanged.
    for chip in range(2):
        t = (-.30 if chip == 0 else .29)*rx
        r = ry*.72
        z = bottom+(top-bottom)*(.52 if chip == 0 else .67)
        cx = x-math.sin(phase)*t+math.cos(phase)*r
        cy = y+math.cos(phase)*t+math.sin(phase)*r
        start = len(vertices)
        for tangent, radial, dz in [(-.11, -.015, -.022), (.12, -.01, -.018),
                                    (.10, .040, .006), (-.075, .055, .009),
                                    (-.05, .020, .036)]:
            vertices.append((cx-math.sin(phase)*tangent+math.cos(phase)*radial,
                             cy+math.cos(phase)*tangent+math.sin(phase)*radial, z+dz))
        faces.extend([(start, start+1, start+2, start+3),
                      (start, start+4, start+1), (start+1, start+4, start+2),
                      (start+2, start+4, start+3), (start+3, start+4, start)])
        indices.extend([0, 1, 1, 0, 2])
    obj = make_mesh(scene, name, vertices, faces, mats, indices)
    add_bevel(obj, rng.uniform(.028, .045))
    return obj


def continuous_shore(scene, cx, cy, rx, ry, water_points, mats):
    """Seat the shore against the actual soil boundary, including its outer lip."""
    surfaces = [o for o in scene.objects if o.type == 'MESH' and
                o.name.startswith(('Living forest floor', 'Uneven earthen shoreline lip'))]
    edges = []
    for obj in surfaces:
        uses = {}
        for polygon in obj.data.polygons:
            for i, vertex in enumerate(polygon.vertices):
                edge = tuple(sorted((vertex, polygon.vertices[(i+1) % len(polygon.vertices)])))
                uses[edge] = uses.get(edge, 0)+1
        edges.extend((obj.matrix_world @ obj.data.vertices[a].co,
                      obj.matrix_world @ obj.data.vertices[b].co)
                     for (a, b), uses_count in uses.items() if uses_count == 1)

    def boundary(angle):
        dx, dy = math.cos(angle), math.sin(angle)
        hits = []
        for a, b in edges:
            sx, sy = b.x-a.x, b.y-a.y
            determinant = dx*sy-dy*sx
            if abs(determinant) < 1e-8:
                continue
            ax, ay = a.x-cx, a.y-cy
            distance = (ax*sy-ay*sx)/determinant
            t = (ax*dy-ay*dx)/determinant
            if distance > 0 and -.0001 <= t <= 1.0001:
                hits.append((distance, a.z+(b.z-a.z)*t))
        assert hits, 'Missing soil boundary at angle %s' % angle
        return max(hits, key=lambda hit: hit[0])

    count = 288
    layers = 5
    vertices, faces, indices = [], [], []
    perimeter = []
    for j in range(count):
        angle = j*math.tau/count
        radius, top = boundary(angle)
        dx, dy = math.cos(angle), math.sin(angle)
        x, y = cx+radius*dx, cy+radius*dy
        perimeter.append((x, y, top))
        inner_radius = radius-.075
        inner_top = floor_height(surfaces, cx+inner_radius*dx, cy+inner_radius*dy)
        if inner_top is None:
            inner_top = top
        # A narrow soil overlap seats the stone skirt beneath both soil meshes.
        # Lower relief remains continuous, avoiding holes between rock groups.
        for r, z in [(inner_radius, inner_top+.004), (radius+.008, top+.003),
                     (radius+.012, min(top-.07, -.15)),
                     (radius-.035, -.48+.022*math.sin(angle*7)),
                     (radius-.13, -.91)]:
            vertices.append((cx+r*dx, cy+r*dy, z))
    for j in range(count):
        nxt = (j+1)%count
        x = (perimeter[j][0]+perimeter[nxt][0])*.5
        y = (perimeter[j][1]+perimeter[nxt][1])*.5
        top = (perimeter[j][2]+perimeter[nxt][2])*.5
        # Keep the lowered creek bed and the water outlet unobstructed.
        if top < -.065 and any(math.hypot(x-p.x, y-p.y) < .34 for p in water_points):
            continue
        for ring in range(layers-1):
            faces.append((j*layers+ring, j*layers+ring+1,
                          nxt*layers+ring+1, nxt*layers+ring))
            indices.append(3 if ring == 0 else (0 if ring == 1 else 2))
    ground = surfaces[0].data.materials[0]
    obj = make_mesh(scene, 'ContinuousLimestoneSupport', vertices, faces, mats+[ground], indices)
    uv = obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        if polygon.material_index == 3:
            for li in polygon.loop_indices:
                point = obj.data.vertices[obj.data.loops[li].vertex_index].co
                uv.data[li].uv = (point.x/7.5+.5, point.y/4.5+.5)
    print('Shore seam fitted to %d actual soil boundary edges.' % len(edges))
    return obj


def vary_main_tree_silhouettes(scene, floor, cx, cy, rx, ry):
    """Shrink four main-island crowns around verified trunk centers, once only."""
    trunks = []
    for obj in scene.objects:
        if obj.type != 'MESH' or not obj.name.startswith('Reference pine trunk'):
            continue
        lo, hi = world_bounds(obj)
        x, y = (lo[0]+hi[0])*.5, (lo[1]+hi[1])*.5
        if ((x-cx)/rx)**2+((y-cy)/ry)**2 < 1.15 and floor_height(floor, x, y) is not None:
            trunks.append((x, y, floor_height(floor, x, y)))
    trunks.sort()
    if len(trunks) != 4:
        print('Tree silhouette variation skipped: expected four unambiguous island trunks, got', len(trunks))
        return 0
    grouped = [[] for _ in trunks]
    for obj in scene.objects:
        if obj.type != 'MESH' or not obj.name.startswith('Rounded pine bough'):
            continue
        lo, hi = world_bounds(obj)
        x, y = (lo[0]+hi[0])*.5, (lo[1]+hi[1])*.5
        index = min(range(4), key=lambda i: math.hypot(x-trunks[i][0], y-trunks[i][1]))
        if math.hypot(x-trunks[index][0], y-trunks[index][1]) < .67:
            grouped[index].append(obj)
    if any(len(group) != 33 for group in grouped):
        print('Tree silhouette variation skipped: ambiguous bough groups', [len(g) for g in grouped])
        return 0
    changed = 0
    for index, group in enumerate(grouped):
        x, y, base = trunks[index]
        height = [.94, 1.00, .91, .97][index]
        for obj in group:
            if obj.get('island_polish_silhouette_v3', False):
                continue
            obj = local_object(scene, obj)
            obj.data = obj.data.copy()
            obj['island_polish_silhouette_baseline'] = [value for vertex in obj.data.vertices for value in vertex.co]
            inverse = obj.matrix_world.inverted()
            # All XY changes shrink towards the trunk, avoiding extra route occlusion.
            for vertex in obj.data.vertices:
                point = obj.matrix_world @ vertex.co
                tier = min(2, max(0, int((point.z-base)/.45)))
                width = max(.90, [.94, .98, .92, .95][index] - tier*.012)
                point.x = x+(point.x-x)*width
                point.y = y+(point.y-y)*width
                point.z = base+(point.z-base)*height
                vertex.co = inverse @ point
            obj.data.update()
            obj['island_polish_silhouette_v3'] = True
            changed += 1
    return changed


def floor_height(surfaces, x, y):
    height = None
    for obj in surfaces:
        inverse = obj.matrix_world.inverted()
        direction = (inverse.to_3x3() @ Vector((0, 0, -1))).normalized()
        hit, point, normal, index = obj.ray_cast(inverse @ Vector((x, y, 4)), direction)
        if hit:
            z = (obj.matrix_world @ point).z
            height = z if height is None else max(height, z)
    return height


def outside_route(x, y, bounds, padding):
    return all(not (lo[0]-padding < x < hi[0]+padding and
                    lo[1]-padding < y < hi[1]+padding) for lo, hi in bounds)


def flora_group(scene, rng, index, x, y, z, mats):
    """One mesh per group, with fern leaflets, curved grass and three mushrooms."""
    vertices, faces, indices = [], [], []

    def face(points, material_index):
        start = len(vertices)
        vertices.extend(points)
        faces.append(tuple(range(start, start+len(points))))
        indices.append(material_index)

    for frond in range(7):
        angle = frond * math.tau / 7 + .2 * index
        forward = Vector((math.cos(angle), math.sin(angle), 0))
        sideways = Vector((-math.sin(angle), math.cos(angle), 0))
        length = rng.uniform(.20, .30)
        for j in range(1, 6):
            t = j/6
            center = Vector((x, y, z+.012)) + forward*(length*t)
            center.z += .15 * math.sin(t*math.pi*.85)
            width = .070 * (1-t*.8)
            for side in [-1, 1]:
                tip = center + sideways*(side*width) + forward*.035
                tip.z += .008
                face([center-forward*.025, tip-forward*.012, tip, center+forward*.027], 0)
    for blade in range(16):
        angle = rng.random()*math.tau
        dx, dy = math.cos(angle), math.sin(angle)
        bx, by = x+dx*rng.uniform(.03, .14), y+dy*rng.uniform(.03, .14)
        height = rng.uniform(.10, .21)
        width = .009
        face([(bx-dy*width, by+dx*width, z), (bx+dy*width, by-dx*width, z),
              (bx+dx*.045+dy*width*.4, by+dy*.045-dx*width*.4, z+height*.6),
              (bx+dx*.075, by+dy*.075, z+height),
              (bx+dx*.045-dy*width*.4, by+dy*.045+dx*width*.4, z+height*.6)], 1)
    for mushroom in range(3):
        angle = index*.8 + mushroom*2.0
        cx, cy = x+math.cos(angle)*.19, y+math.sin(angle)*.19
        radius = rng.uniform(.035, .056)
        height = rng.uniform(.055, .085)
        n = 10
        stem_low = [(cx+math.cos(j*math.tau/n)*.012, cy+math.sin(j*math.tau/n)*.012, z) for j in range(n)]
        stem_high = [(px, py, z+height) for px, py, _ in stem_low]
        cap_low = [(cx+math.cos(j*math.tau/n)*radius, cy+math.sin(j*math.tau/n)*radius, z+height) for j in range(n)]
        cap_mid = [(cx+math.cos(j*math.tau/n)*radius*.72, cy+math.sin(j*math.tau/n)*radius*.72, z+height+radius*.46) for j in range(n)]
        tip = (cx, cy, z+height+radius*.70)
        for j in range(n):
            k = (j+1)%n
            face([stem_low[j], stem_low[k], stem_high[k], stem_high[j]], 3)
            face([cap_low[j], cap_low[k], cap_mid[k], cap_mid[j]], 2)
            face([cap_mid[j], cap_mid[k], tip], 2)
        face(list(reversed(cap_low)), 3)
    return make_mesh(scene, 'BotanicalGroup_%02d' % index, vertices, faces, mats, indices)


def polish_active_scene():
    scene = bpy.context.scene
    rng = random.Random(SEED)
    routes = sorted([o for o in scene.objects if o.type == 'MESH' and
                     base_name(o).startswith('TrailCell_')], key=lambda o: base_name(o))
    expected = ['TrailCell_%02d' % i for i in range(8)]
    assert [base_name(o) for o in routes] == expected, 'Expected exactly eight TrailCell_00..07 meshes.'
    bpy.context.view_layer.update()
    route_before = {o.name: snapshot(o) for o in routes}
    anchor_prefixes = ('Little bridge', 'Bridge ', 'Water creek', 'Creek ', 'Exit ', 'Pennant ',
                       'Small canvas tent', 'Tent ', 'TrailCell_')
    anchors = {o.name: snapshot(o) for o in scene.objects
               if o.name.startswith(anchor_prefixes) or o.type == 'CAMERA'}
    before_count = len(scene.objects)
    triangles_before = triangle_count(scene.objects)
    isolate_shared_collections(scene)

    for obj in list(scene.objects):
        if obj.name.startswith(PREFIX) and not obj.name.startswith(PREFIX+'BotanicalGroup_'):
            unlink_from_scene(scene, obj)

    limestone = [material('IslandLimestone', (.52, .51, .43)),
                 material('IslandLimestoneLight', (.69, .66, .54)),
                 material('IslandLimestoneDark', (.39, .43, .40))]
    botanical = [material('IslandFern', (.22, .34, .095)),
                 material('IslandGrass', (.37, .46, .12)),
                 material('IslandMushroom', (.64, .16, .065)),
                 material('IslandMushroomStem', (.84, .77, .58))]
    needles = [material('TrailNeedles', (.20, .29, .105)),
               material('TrailNeedlesLight', (.25, .34, .14)),
               material('TrailNeedlesDeep', (.12, .21, .11))]
    floor = [o for o in scene.objects if o.type == 'MESH' and o.name.startswith('Living forest floor')]
    assert floor, 'Living forest floor mesh is required to place dressing.'
    lo, hi = world_bounds(floor[0])
    center_x, center_y = (lo[0]+hi[0])*.5, (lo[1]+hi[1])*.5
    radius_x, radius_y = (hi[0]-lo[0])*.5, (hi[1]-lo[1])*.5
    route_bounds = [world_bounds(o) for o in routes]
    # Existing water vertices identify both openings, including a transformed scene.
    water_points = [o.matrix_world @ v.co for o in scene.objects
                    if o.type == 'MESH' and o.name.startswith('Water creek') for v in o.data.vertices]

    removed = 0
    for obj in list(scene.objects):
        if obj.name.startswith(('Rocky edge', 'Carved shoreline stone', 'Low shoreline shelf')):
            unlink_from_scene(scene, obj)
            removed += 1

    continuous_shore(scene, center_x, center_y, radius_x, radius_y, water_points, limestone)
    rocks = 1
    # Six irregular outcrop groups, separated by quieter low dark shoulders.
    formations = [(0.14, .92, .39, -.57, -.04), (.94, .73, .35, -.47, -.09),
                  (2.17, 1.04, .44, -.66, -.025), (3.30, .83, .36, -.51, -.07),
                  (4.12, 1.14, .45, -.71, -.015), (5.68, .77, .34, -.50, -.10)]
    for i, (angle, tangent_width, radial_width, bottom, top) in enumerate(formations):
        x = center_x + radius_x*.982*math.cos(angle)
        y = center_y + radius_y*.982*math.sin(angle)
        # Leave the creek mouths fully open and the route silhouette unobstructed.
        if any(math.hypot(x-p.x, y-p.y) < .56 for p in water_points):
            continue
        # The crown stays below the floor, so shoreline under route endpoints
        # supports the island silhouette without covering the playable surface.
        stone(scene, rng, 'LimestoneStratum_%02d' % i, x, y,
              tangent_width, radial_width, bottom, top,
              angle+rng.uniform(-.19, .19), limestone)
        rocks += 1
        if i in [0, 2, 4, 5]:
            stone(scene, rng, 'LowShelf_%02d' % i,
                  center_x+(x-center_x)*1.025-math.sin(angle)*tangent_width*.32,
                  center_y+(y-center_y)*1.025+math.cos(angle)*tangent_width*.32,
                  tangent_width*rng.uniform(.62, .83), radial_width*.84, -.90,
                  rng.uniform(-.64, -.46), angle-.19, limestone)
            rocks += 1

    boughs = sorted([o for o in scene.objects if o.type == 'MESH' and
                     o.name.startswith('Rounded pine bough')], key=lambda o: o.name)
    for obj in boughs:
        # Never mutate mesh data belonging to another scene/object.
        obj = local_object(scene, obj)
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        mid_z = (min(p.z for p in points)+max(p.z for p in points))*.5
        # Stable per-bough variation; no change to silhouette or occlusion.
        pick = 2 if mid_z < .85 else rng.choices([0, 1, 2], [6, 2, 2])[0]
        if not obj.get('island_polish_decimated', False):
            obj.data.materials.clear()
            obj.data.materials.append(needles[pick])
            for polygon in obj.data.polygons:
                polygon.material_index = 0
        if not obj.get('island_polish_decimated', False):
            # Each original petal is dense; apply once, including after reruns.
            obj.data = obj.data.copy()
            modifier = obj.modifiers.new('Reference foliage budget', 'DECIMATE')
            modifier.ratio = .45
            modifier.use_collapse_triangulate = True
            with bpy.context.temp_override(object=obj, active_object=obj,
                                           selected_objects=[obj], selected_editable_objects=[obj]):
                bpy.ops.object.modifier_apply(modifier=modifier.name)
            obj['island_polish_decimated'] = True

    silhouette_boughs = vary_main_tree_silhouettes(scene, floor, center_x, center_y, radius_x, radius_y)
    bpy.context.view_layer.update()
    groups = len([o for o in scene.objects if o.name.startswith(PREFIX+'BotanicalGroup_')])
    preserve_flora = groups > 0
    # Six groups from eight candidates; fallback locations get every clearance check.
    placed = []
    for index, (nx, ny) in enumerate([(-.43, -.69), (-.08, -.70), (-.22, .37),
                                     (.66, -.53), (.18, .72), (-.57, .61),
                                     (.77, .11), (-.11, .66)]):
        if groups >= 6 or preserve_flora:
            break
        x, y = center_x+nx*radius_x, center_y+ny*radius_y
        alternatives = [(x+dx, y+dy) for dx, dy in [(0, 0), (0, -.23), (-.24, 0), (0, .24), (.24, 0)]]
        location = None
        for px, py in alternatives:
            if not outside_route(px, py, route_bounds, .31):
                continue
            if any(math.hypot(px-p.x, py-p.y) < .45 for p in water_points):
                continue
            if any(math.hypot(px-qx, py-qy) < .61 for qx, qy in placed):
                continue
            heights = [floor_height(floor, px+dx, py+dy)
                       for dx, dy in [(0, 0), (.29, 0), (-.29, 0), (0, .29), (0, -.29)]]
            if any(h is None for h in heights):
                continue
            location = (px, py, heights[0])
            break
        if location is None:
            continue
        x, y, height = location
        flora_group(scene, rng, index, x, y, height+.004, botanical)
        placed.append((x, y))
        groups += 1

    bpy.context.view_layer.update()
    assert {o.name: snapshot(o) for o in routes} == route_before, 'Route changed during polish.'
    for name, original in anchors.items():
        obj = scene.objects.get(name)
        assert obj is not None and snapshot(obj) == original, 'Gameplay anchor changed: ' + name
    new_objects = [o for o in scene.objects if o.name.startswith(PREFIX)]
    triangles_after = triangle_count(scene.objects)
    print('Island polish: scene=%s; removed shore=%d; new rocks=%d; botanical groups=%d; '
          'boughs recolored=%d; meshes=%d; objects %d -> %d; route/anchors unchanged.' %
          (scene.name, removed, rocks, groups, len(boughs), len(new_objects), before_count, len(scene.objects)))
    print('Island polish triangles: %d -> %d.' % (triangles_before, triangles_after))
    print('Main-island silhouette boughs updated:', silhouette_boughs)
    return {'scene': scene.name, 'rocks': rocks, 'botanical_groups': groups,
            'new_objects': len(new_objects), 'route_unchanged': True,
            'triangles_before': triangles_before, 'triangles_after': triangles_after}


if __name__ == '__main__':
    polish_active_scene()
