import sys
import json
import numpy as np
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QPushButton, 
                             QVBoxLayout, QHBoxLayout, QFileDialog, QLabel, 
                             QInputDialog, QComboBox, QSpinBox, QMessageBox)
from PyQt5.QtGui import QPainter, QColor, QPen, QBrush, QPixmap, QImage, QPolygon
from PyQt5.QtCore import Qt, QPoint, QRectF
from shapely.geometry import Polygon as ShapelyPolygon
from shapely.strtree import STRtree
from PyQt5.QtWidgets import QProgressBar

from PyQt5.QtCore import QObject, QThread, pyqtSignal
from scipy.spatial import Voronoi
import random
from PIL import Image
from PyQt5.QtWidgets import QDialog, QVBoxLayout, QLabel, QPushButton, QDialogButtonBox
from PyQt5.QtWidgets import QDialog, QVBoxLayout, QLabel, QPushButton, QDialogButtonBox, QFrame
from shapely.geometry import Polygon as ShapelyPolygon, box

class PolygonWorker(QObject):
    finished = pyqtSignal()
    progress = pyqtSignal(int)
    set_maximum = pyqtSignal(int)
    
    def __init__(self, editor):
        super().__init__()
        self.editor = editor
    
    def run(self, mode="replace"):
        self.editor._generate_polygons_threadsafe(self.progress, self.set_maximum, mode)
        self.finished.emit()



class MapEditor(QMainWindow):
    def __init__(self):
        super().__init__()
        
        # Constants
        self.POINT_RADIUS = 6
        self.SELECTED_POINT_RADIUS = 8
        self.TILE_TYPES = ["plains", "sea", "mountain", "city", "jungle", "forest", "hills"]
        
        # State variables
        self.image = None
        self.image_size = (0, 0)
        self.points = []  # List of (x, y) normalized coordinates
        self.edges = []   # List of (point_idx1, point_idx2)
        self.tiles = []   # List of tile objects
        self.selected_point = None
        self.shift_selected_point = None
        self.selected_tile = None
        self.next_tile_id = 0
        self.new_edges = []
        
        # Zoom and pan variables
        self.zoom_factor = 1.0
        self.pan_offset_x = 0
        self.pan_offset_y = 0
        self.panning = False
        self.last_mouse_pos = None
        
        self.init_ui()
    
    
    
    def init_ui(self):
        # Main layout
        main_layout = QHBoxLayout()
        
        # Canvas area
        self.canvas = Canvas(self)
        main_layout.addWidget(self.canvas, 4)  # 4:1 ratio

        # Add vertical line (separator)
        line = QFrame()
        line.setFrameShape(QFrame.VLine)
        line.setFrameShadow(QFrame.Sunken)
        main_layout.addWidget(line)
        
        # Controls
        controls_layout = QVBoxLayout()

        def add_section(label_text, buttons):
            section_label = QLabel(f"<b>{label_text}</b>")
            section_label.setStyleSheet("margin-top: 10px; margin-bottom: 4px;")
            controls_layout.addWidget(section_label)
            for btn in buttons:
                controls_layout.addWidget(btn)
        
        load_btn = QPushButton("Load Image")
        load_btn.clicked.connect(self.load_image)

        load_json_btn = QPushButton("Load JSON")
        load_json_btn.clicked.connect(self.load_json)

        export_btn = QPushButton("Export JSON")
        export_btn.clicked.connect(self.export_json)

        delete_btn = QPushButton("Delete Selected Point")
        delete_btn.clicked.connect(self.delete_selected_point)

        clear_btn = QPushButton("Clear All")
        clear_btn.clicked.connect(self.clear_all)

        reset_zoom_btn = QPushButton("Reset Zoom")
        reset_zoom_btn.clicked.connect(self.reset_zoom)

        gen_poly_btn = QPushButton("Generate Polygons")
        gen_poly_btn.clicked.connect(self.generate_polygons)

        gen_new_btn = QPushButton("Generate New Polygons")
        gen_new_btn.clicked.connect(self.generate_new_polygons)

        voronoi_btn = QPushButton("Generate Voronoi Tiles")
        voronoi_btn.clicked.connect(self.generate_voronoi_tiles)

        rand_voronoi_btn = QPushButton("Generate Random Voronoi Tiles")
        rand_voronoi_btn.clicked.connect(self.request_voronoi_generation)

        detect_biome_btn = QPushButton("Detect Biome")
        detect_biome_btn.clicked.connect(self.detect_biomes)


        # --- Add grouped sections to the layout ---
        add_section("Project", [load_btn, load_json_btn, export_btn])
        add_section("Editing", [delete_btn, clear_btn, reset_zoom_btn])
        add_section("Polygon Generation", [gen_poly_btn, gen_new_btn])
        add_section("Voronoi", [voronoi_btn, rand_voronoi_btn, detect_biome_btn])

        
        # Bottom-right stacked labels container
        status_container = QVBoxLayout()
        status_container.setAlignment(Qt.AlignRight | Qt.AlignBottom)

        self.info_label = QLabel("Load an image to start")
        self.status_label = QLabel("Points: 0, Edges: 0, Tiles: 0")
        self.zoom_label = QLabel("Zoom: 100%")

        # Optional: make font smaller or gray for subtle look
        for lbl in [self.info_label, self.status_label, self.zoom_label]:
            lbl.setStyleSheet("font-size: 10pt; color: gray;")

        status_container.addWidget(self.info_label)
        status_container.addWidget(self.status_label)
        status_container.addWidget(self.zoom_label)

        # Add a stretch above to push it to the bottom
        controls_layout.addStretch(1)
        controls_layout.addLayout(status_container)

        
        # Inside controls_layout, after other controls:
        self.progress_bar = QProgressBar()
        self.progress_bar.setValue(0)
        self.progress_bar.setVisible(False)
        controls_layout.addWidget(self.progress_bar)

        # Add the controls to the main layout
        control_widget = QWidget()
        control_widget.setLayout(controls_layout)
        control_widget.setMaximumWidth(220)
        main_layout.addWidget(control_widget, 1)  # 4:1 ratio
        
        # Set the main layout
        central_widget = QWidget()
        central_widget.setLayout(main_layout)
        self.setCentralWidget(central_widget)

        self.setWindowTitle("Tyrell Game Map Editor")
        self.setGeometry(100, 100, 1000, 600)
        self.show()
        
        # Instructions
        self.show_welcome_popup()
    
    
    def load_project(self):
        self.load_image()
        self.load_json()

    def show_welcome_popup(self):
        dialog = QDialog(self)
        dialog.setWindowTitle("Welcome")
        dialog.setMinimumWidth(400)

        layout = QVBoxLayout()

        # Big title
        title = QLabel("<h1 style='text-align: center;'>Tyrell Game Project Map Editor</h1>")
        title.setTextFormat(Qt.RichText)
        title.setAlignment(Qt.AlignCenter)
        layout.addWidget(title)

        # Credit line
        credit = QLabel("<p style='text-align: center;'>Created by Marco Hampel</p>")
        credit.setTextFormat(Qt.RichText)
        credit.setAlignment(Qt.AlignCenter)
        layout.addWidget(credit)

        # Welcome text
        welcome = QLabel(
            "<p style='text-align: center; padding: 0 10px;'>"
            "Hello! This editor was developed to create maps for strategy games using the JSON format. "
            "The editor is currently being developed for the <b>Tyrell Strategy Game Project</b>. "
            "Hope you enjoy!"
            "</p>"
        )
        # Add version number
        version_label = QLabel("<p style='text-align: center; color: gray;'>Version 0.1</p>")
        version_label.setTextFormat(Qt.RichText)
        version_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(version_label)
        welcome.setTextFormat(Qt.RichText)
        welcome.setWordWrap(True)
        welcome.setAlignment(Qt.AlignCenter)
        layout.addWidget(welcome)

        # Buttons
        button_box = QDialogButtonBox()
        load_btn = QPushButton("Load Project")
        instructions_btn = QPushButton("Instructions")
        button_box.addButton(load_btn, QDialogButtonBox.ActionRole)
        button_box.addButton(instructions_btn, QDialogButtonBox.ActionRole)
        layout.addWidget(button_box)

        # Set layout
        dialog.setLayout(layout)

        # Connect button actions
        load_btn.clicked.connect(lambda: (dialog.accept(), self.load_project()))
        instructions_btn.clicked.connect(lambda: self.show_instructions())

        dialog.exec_()


    def show_instructions(self):
        msg = QMessageBox()
        msg.setWindowTitle("Instructions")
        msg.setText("<b><span style='font-size: 14pt;'>Map Editor Instructions</span></b><br><br>"
                    "- Load an image to start\n"
                    "- Click on the image to create points\n"
                    "- Left-click a point to select it\n"
                    "- Double-click near a point to select it (easier selection)\n"
                    "- Hold SHIFT and left-click another point to create an edge\n"
                    "- Hold SHIFT and double-click another point to create an edge (easier selection)\n"
                    "- Right-click a selected point to delete it\n"
                    "- Drag a selected point to move it\n"
                    "- Mouse wheel to zoom in/out\n"
                    "- Hold middle mouse button to pan\n"
                    "- Click 'Generate Polygons' to create tiles from enclosed areas\n"
                    "- Click 'Generate New Polygons' to add new tiles to existing ones (like 100x more efficient)\n"
                    "- Click on a tile to set its properties\n"
                    "- Export to JSON when finished"
                    "- Click 'Load JSON' to load a previously saved map or to keep working on a project\n"
                    "- Click 'Generate Voronoi Tiles' to create Voronoi tiles from selected points\n"
                    "- Click 'Generate Random Voronoi Tiles' to create random Voronoi tiles from randomly distributed points\n")
        msg.exec_()
    
    def load_image(self):
        file_name, _ = QFileDialog.getOpenFileName(self, "Open Image", "", "Image Files (*.png *.jpg *.bmp)")
        if file_name:
            self.image = QImage(file_name)
            if not self.image.isNull():
                self.image_size = (self.image.width(), self.image.height())
                self.info_label.setText(f"Image loaded: {file_name}")
                self.canvas.update()
                
                # Clear data when loading a new image
                self.clear_all()
    
    def clear_all(self):
        self.points = []
        self.edges = []
        self.tiles = []
        self.selected_point = None
        self.shift_selected_point = None
        self.selected_tile = None
        self.next_tile_id = 0
        self.update_status()
        self.canvas.update()
    
    def add_point(self, norm_x, norm_y):
        # Add a point with normalized coordinates (0-1)
        self.points.append((norm_x, norm_y))
        self.selected_point = len(self.points) - 1
        self.shift_selected_point = None
        print(f"Added point {len(self.points)-1} at ({norm_x:.4f}, {norm_y:.4f})")
        print(f"Total points: {len(self.points)}")
        self.update_status()
        self.canvas.update()
    
    def select_point(self, idx):
        if self.selected_point == idx:
            self.selected_point = None
        else:
            self.selected_point = idx
        self.canvas.update()
    
    def add_edge(self, idx1, idx2):
        # Check if the edge already exists
        edge = (min(idx1, idx2), max(idx1, idx2))
        if edge not in self.edges:
            print("Added edge:",len(self.edges))
            self.edges.append(edge)
            self.new_edges.append(edge)
            # Make the destination point the new selected point
            self.selected_point = idx2
            self.shift_selected_point = None
            self.update_status()
            self.canvas.update()
    
    def update_status(self):
        self.status_label.setText(f"Points: {len(self.points)}, Edges: {len(self.edges)}, Tiles: {len(self.tiles)}")
    
    def get_edge_index(self, p1, p2):
        for i, edge in enumerate(self.edges):
            if edge[0] == p1 and edge[1] == p2:
                return i
            if edge == [p1, p2]:
                return i
              
        print(f"Could not find: {p1}, {p2}")
        return -1
    
    def do_edges_intersect(self, p1, p2, p3, p4):
        """
        Check if the line segment from p1 to p2 intersects with the line segment from p3 to p4.
        Each point is a tuple (x, y).
        """
        def ccw(a, b, c):
            # Determine if three points are listed in counterclockwise order
            return (c[1] - a[1]) * (b[0] - a[0]) > (b[1] - a[1]) * (c[0] - a[0])
        
        # Check if line segments intersect
        return ccw(p1, p3, p4) != ccw(p2, p3, p4) and ccw(p1, p2, p3) != ccw(p1, p2, p4)

    def has_self_intersection(self, cycle):
        """
        Simplified check for polygon self-intersection.
        Only checks for obvious cases of self-intersection without being too restrictive.
        """
        # For small cycles (triangles and quads), assume no self-intersection
        if len(cycle) <= 3:
            return False
            
        points = [self.points[idx] for idx in cycle]
        n = len(points)
        
        # Check non-adjacent edges for intersection, but be more lenient
        # Only check edges that are at least 2 edges apart
        for i in range(n):
            p1 = points[i]
            p2 = points[(i + 1) % n]
            
            for j in range(i + 3, i + n - 1):  # Skip adjacent and once-removed edges
                j_mod = j % n
                if (j_mod == (i - 1) % n) or (j_mod == i) or (j_mod == (i + 1) % n) or (j_mod == (i + 2) % n):
                    continue  # Skip testing with adjacent and once-removed edges
                    
                p3 = points[j_mod]
                p4 = points[(j_mod + 1) % n]
                
                # Check if these edges intersect
                if self.do_edges_intersect(p1, p2, p3, p4):
                    return True
        
        return False
    
    def _on_polygons_generated(self):
        self.progress_bar.setMaximum(1)
        self.progress_bar.setValue(1)
        self.progress_bar.setVisible(False)

        self.compute_neighbors()
        self.update_status()
        self.canvas.update()
        print(f"Created {len(self.tiles)} tiles")


    def _generate_polygons_threadsafe(self, progress_callback, set_max_callback, mode="replace"):
        if len(self.points) < 3 or len(self.edges) < 3:
            QMessageBox.warning(self, "Warning", "Need at least 3 points and 3 edges to form polygons")
            return

        print(f"Generating polygons from {len(self.points)} points and {len(self.edges)} edges")
        
        # TODO: Whenever we create a new point, record that as a "new point" then when checking if a point is included in an edge, we can verify if it is a new point or not.
        # Build the graph
        # Build subgraph (only new parts of the graph if appending)
        if mode == "append":
            # Collect all edges used by existing tiles
            used_edges = set()
            used_point_indices = set()

            for tile in self.tiles:
                pts = tile["points"]
                edge_shared_with_new = False
                for i in range(len(pts)):
                    a = pts[i]
                    b = pts[(i + 1) % len(pts)]
                    # check if either point is included in new_edges  if so do not include them in used_edges.
                    for _e in self.new_edges:
                        if a in _e or b in _e:
                            print("Shared edge detected. Stopping removal...")
                            edge_shared_with_new = True
                            break
                    used_edges.add(tuple(sorted((a, b))))
               
                if not edge_shared_with_new:
                    used_point_indices.update(tile["points"])
            
            new_points = set(range(len(self.points))) - used_point_indices
            filtered_edges = []
            for e in self.edges:
                edge_tuple = tuple(sorted(e))
                if edge_tuple not in used_edges or (e[0] in new_points or e[1] in new_points):
                    filtered_edges.append(e)


        else:
            filtered_edges = self.edges

        adj_list = [[] for _ in range(len(self.points))]
        for e in filtered_edges:
            adj_list[e[0]].append(e[1])
            adj_list[e[1]].append(e[0])

        edge_set = set((min(e[0], e[1]), max(e[0], e[1])) for e in filtered_edges)


        # Step 1: Find all simple polygons (those without internal edges)
        all_polygons = self.find_simple_polygons(adj_list, edge_set)
        print(f"Found {len(all_polygons)} candidate polygons")
        
        # Sort polygons by size (smallest first)
        all_polygons.sort(key=len)
        
        # Filter out polygons that are covered by smaller ones
        filtered_polygons = []
        covered_edges = set()
        
        polygon_entries = []
        polygon_shapes = []
        
        #self.progress_bar.setVisible(True)
        #self.progress_bar.setMaximum(len(polygon_shapes))
        #self.progress_bar.setValue(0)
        set_max_callback.emit(len(all_polygons))

        for poly in all_polygons:
            coords = [self.points[i] for i in poly]
            shape = ShapelyPolygon(coords)
            if shape.is_valid and not shape.is_empty:
                polygon_entries.append({
                    "indices": poly,
                    "polygon": shape
                })
                polygon_shapes.append(shape)

        spatial_index = STRtree(polygon_shapes)

        filtered_polygons = []
        used_indices = set()

        for i, shape in enumerate(polygon_shapes):
            progress_callback.emit(i + 1)

           # QApplication.processEvents()
            if i in used_indices:
                continue
            
            poly = polygon_entries[i]["indices"]

            if self.has_self_intersection(poly):
                print(f"Skipping polygon {poly} due to self-intersection")
                continue

            overlaps = False
            for j in spatial_index.query(shape):
                if j == i:
                    continue
                candidate = polygon_shapes[j]
                inter = shape.intersection(candidate)
                if not inter.equals(shape.boundary) and not inter.equals(candidate.boundary):
                    if inter.area > 1e-6 and j in used_indices:
                        overlaps = True
                        break

            if not overlaps:
                filtered_polygons.append(poly)
                used_indices.add(i)
                print(f"added {i} to : {used_indices}")
            else:
                print("overwritten")

        if mode == "replace":
            self.tiles = []
            self.next_tile_id = 0
        else:
            # "append" mode
            existing_edgesets = {frozenset(tile["points"]) for tile in self.tiles}

        # ✅ Now create tiles from filtered polygons
        for poly in filtered_polygons:
            edges_indices = []
            valid = True
            for i in range(len(poly)):
                p1 = poly[i]
                p2 = poly[(i + 1) % len(poly)]
                print(f"({p1}, {p2})")
                edge_index = self.get_edge_index(min(p1, p2), max(p1, p2))
                if edge_index != -1:
                    edges_indices.append(edge_index)
                else:
                    print("failed to find index:",edge_index)
                    valid = False
                    break

            if mode == "append":
                if frozenset(poly) in existing_edgesets:
                    continue  # Skip if same polygon already exists
                new_tile = {
                    "id": self.next_tile_id,
                    "name": f"Tile {self.next_tile_id}",
                    "type": "plains",
                    "population": 0,
                    "edges": edges_indices,
                    "points": poly,
                    "neighbors": []
                }
                self.tiles.append(new_tile)
                self.next_tile_id += 1
                print(f"Created tile {new_tile['id']} with {len(poly)} points: {poly}")
        print("Computing neighbors...")
        # ✅ Now compute neighbors
        self.compute_neighbors()

        print("Finishing up...")
        #self.progress_bar.setVisible(False)
        # ✅ Final UI updates
        print(f"Created {len(self.tiles)} tiles")
        
        self._on_polygons_generated()
    
    def generate_polygons(self):
        self.progress_bar.setVisible(True)
        self.progress_bar.setValue(0)
        self.progress_bar.setMaximum(0)  # Indeterminate until we know polygon count

        self.thread = QThread()
        self.worker = PolygonWorker(self)
        self.worker.moveToThread(self.thread)

        self.thread.started.connect(self.worker.run)
        self.worker.finished.connect(self.thread.quit)
        self.worker.finished.connect(self.worker.deleteLater)
        self.thread.finished.connect(self.thread.deleteLater)

        self.worker.progress.connect(self.progress_bar.setValue)
        self.worker.set_maximum.connect(self.progress_bar.setMaximum)

        self.thread.start()

    def generate_new_polygons(self):
        self.progress_bar.setVisible(True)
        self.progress_bar.setValue(0)
        self.progress_bar.setMaximum(0)  # Indeterminate until known

        self.thread = QThread()
        self.worker = PolygonWorker(self)
        self.worker.moveToThread(self.thread)

        self.worker.run_mode = "append"  # Custom flag for new behavior
        self.thread.started.connect(lambda: self.worker.run(mode="append"))

        self.worker.finished.connect(self.thread.quit)
        self.worker.finished.connect(self.worker.deleteLater)
        self.thread.finished.connect(self.thread.deleteLater)

        self.worker.progress.connect(self.progress_bar.setValue)
        self.worker.set_maximum.connect(self.progress_bar.setMaximum)

        self.thread.start()

   
    def find_simple_polygons(self, adj_list, edge_set):
        """Find all simple polygons (cycles without internal edges)"""
        polygons = []
        
        # Try direct search for polygons of different sizes
        MAX_POLY_SIZE = 8
        for size in range(3, min(len(self.points) + 1, MAX_POLY_SIZE + 1)):
            cycles = self.find_local_greedy_cycles(adj_list, edge_set, max_size=min(size,8), distance_threshold=0.15)
            #cycles = self._find_cycles_of_size(size, adj_list, edge_set)
            print(f"Checking {len(cycles)} cycles of size {size}")
            for cycle in cycles:
                if len(cycle) >= 3 and not self._has_internal_edges(cycle, edge_set):
                    polygons.append(cycle)
                # REPLACEMENT #2
                #if not self._has_internal_edges(cycle, edge_set):
                    #polygons.append(cycle)
                    print(f"Found simple polygon of size {len(cycle)}: {cycle}")
        
        # If we don't find any, try a more direct approach for a single outer polygon
        if not polygons and len(self.points) >= 3:
            # Check if all points form a single simple polygon without internal edges
            outer_polygon = self._find_outer_polygon(adj_list, edge_set)
            if outer_polygon:
                polygons.append(outer_polygon)
                print(f"Found outer polygon with {len(outer_polygon)} points: {outer_polygon}")
        
        return polygons

    def find_local_greedy_cycles(self, adj_list, edge_set, max_size=8, distance_threshold=0.15):
        """Greedy local polygon discovery: prioritize nearby nodes and limit distance."""
        found_cycles = []
        visited = set()

        def euclidean_dist(p1, p2):
            return ((p1[0] - p2[0]) ** 2 + (p1[1] - p2[1]) ** 2) ** 0.5

        for start in range(len(self.points)):
            queue = [([start], set([start]))]
            start_point = self.points[start]

            while queue:
                path, path_set = queue.pop(0)
                current = path[-1]
                current_point = self.points[current]

                # Try to complete the cycle if long enough
                if len(path) >= 3 and start in adj_list[current]:
                    cycle = path[:]
                    if self._is_valid_cycle(cycle, edge_set):
                        key = tuple(sorted(cycle))
                        if key not in visited:
                            visited.add(key)
                            found_cycles.append(cycle)
                            print(f"Greedy found cycle: {cycle}")
                    continue

                # Greedy neighbor sort by spatial proximity
                neighbors = [
                    n for n in adj_list[current] 
                    if n not in path_set or (n == start and len(path) >= 3)
                ]
                neighbors.sort(key=lambda n: euclidean_dist(current_point, self.points[n]))

                for neighbor in neighbors:
                    if len(path) >= max_size:
                        continue  # Too long

                    # Check spatial threshold
                    if euclidean_dist(start_point, self.points[neighbor]) > distance_threshold:
                        continue

                    # Check edge validity
                    edge = (min(current, neighbor), max(current, neighbor))
                    if edge not in edge_set:
                        continue

                    new_path = path + [neighbor]
                    new_set = path_set.copy()
                    new_set.add(neighbor)
                    queue.append((new_path, new_set))

        return found_cycles

    def _is_valid_cycle(self, cycle, edge_set):
        """Check if a cycle is valid: all boundary edges exist and no internal edges"""
        if len(cycle) < 3:
            return False

        # Check boundary edges
        for i in range(len(cycle)):
            p1 = cycle[i]
            p2 = cycle[(i + 1) % len(cycle)]
            if (min(p1, p2), max(p1, p2)) not in edge_set:
                return False

        # Check for internal edges (non-adjacent)
        if len(cycle) > 3:
            for i in range(len(cycle)):
                for j in range(i + 2, len(cycle)):
                    if j == (i + 1) % len(cycle) or (i == 0 and j == len(cycle) - 1):
                        continue
                    edge = (min(cycle[i], cycle[j]), max(cycle[i], cycle[j]))
                    if edge in edge_set:
                        return False

        return True


    def polygons_overlap_significantly(self, poly_a_idx, poly_b_idx):
        from shapely.geometry import Polygon as ShapelyPolygon
        coords_a = [self.points[i] for i in poly_a_idx]
        coords_b = [self.points[i] for i in poly_b_idx]
        poly_a = ShapelyPolygon(coords_a)
        poly_b = ShapelyPolygon(coords_b)

        intersection = poly_a.intersection(poly_b)
        if not intersection.is_empty and not intersection.equals(poly_a.boundary) and not intersection.equals(poly_b.boundary):
            return intersection.area > 1e-6
        return False

    def _find_cycles_of_size(self, size, adj_list, edge_set):
        """Find all cycles of a specific size"""
        cycles = []
        visited = set()
        
        # Use BFS to find paths of the right length that form cycles
        for start in range(len(self.points)):
            queue = [(start, [start], set([start]))]
            
            while queue:
                node, path, visited_nodes = queue.pop(0)
                
                # If we have a path of the right length, check if it forms a cycle
                if len(path) == size - 1:
                    if start in adj_list[node]:
                        # Makes a cycle
                        cycle = path.copy()
                        
                        # Verify all edges exist
                        valid = True
                        for i in range(len(cycle)):
                            next_idx = (i + 1) % len(cycle)
                            next_node = cycle[next_idx] if next_idx < len(cycle) else start
                            edge = (min(cycle[i], next_node), max(cycle[i], next_node))
                            if edge not in edge_set:
                                valid = False
                                break
                        
                        if valid:
                            # Create a canonical representation for deduplication
                            canonical = tuple(sorted(cycle))
                            rotated_forms = [tuple(cycle[i:] + cycle[:i]) for i in range(len(cycle))]
                            if any(r in visited for r in rotated_forms):
                                continue
                            visited.add(canonical)
                            cycles.append(cycle)
                    continue
                
                # Otherwise extend the path
                if len(path) < size - 1:
                    for neighbor in adj_list[node]:
                        if neighbor != start and neighbor not in visited_nodes:
                            new_path = path + [neighbor]
                            new_visited = visited_nodes.copy()
                            new_visited.add(neighbor)
                            queue.append((neighbor, new_path, new_visited))
        
        return cycles

    def _has_internal_edges(self, cycle, edge_set):
        """Check if a cycle has any internal edges"""
        if len(cycle) <= 3:
            return False  # Triangles can't have internal edges
            
        # Check all pairs of non-adjacent vertices
        for i in range(len(cycle)):
            for j in range(i + 2, len(cycle)):
                # Skip adjacent vertices and wraparound
                if j == (i + 1) % len(cycle) or (i == 0 and j == len(cycle) - 1):
                    continue
                    
                # If there's an edge between non-adjacent vertices, it's an internal edge
                edge = (min(cycle[i], cycle[j]), max(cycle[i], cycle[j]))
                if edge in edge_set:
                    print(f"Cycle {cycle} has internal edge {edge}")
                    return True
                    
        return False

    def _find_outer_polygon(self, adj_list, edge_set):
        """Try to find a single large outer polygon that includes all points"""
        # Start with a simple approach - check if all points form a single cycle
        # without internal edges
        
        # First, verify each point has exactly 2 neighbors (a simple cycle)
        for i in range(len(self.points)):
            # Filter neighbors to only include those with valid edges
            valid_neighbors = [n for n in adj_list[i] if (min(i, n), max(i, n)) in edge_set]
            if len(valid_neighbors) != 2:
                return None  # Not a simple cycle
        
        # Try to construct the cycle
        cycle = []
        visited = set()
        start = 0  # Start with vertex 0
        
        current = start
        cycle.append(current)
        visited.add(current)
        
        # Follow neighbors to try to construct a cycle
        while len(cycle) < len(self.points):
            # Find unvisited neighbors
            neighbors = [n for n in adj_list[current] if n not in visited]
            
            # If no unvisited neighbors but we can return to start, we're done
            if not neighbors:
                if start in adj_list[current] and len(cycle) == len(self.points):
                    # Check if the edge exists
                    if (min(current, start), max(current, start)) in edge_set:
                        return cycle
                return None  # Can't complete the cycle
            
            # Move to the next unvisited neighbor
            current = neighbors[0]
            cycle.append(current)
            visited.add(current)
        
        # Check if the last vertex connects back to the start
        if start in adj_list[current]:
            # Verify the edge exists
            if (min(current, start), max(current, start)) in edge_set:
                return cycle
                
        return None



    def find_minimal_triangles(self, adj_list, edge_set):
        """Find minimal triangles in the graph (triangles that don't contain other vertices)"""
        triangles = []
        visited = set()
        
        # Find all possible triangles
        for i in range(len(self.points)):
            for j in adj_list[i]:
                for k in adj_list[j]:
                    if k in adj_list[i]:  # Completes the triangle
                        # Create canonical representation
                        cycle = sorted([i, j, k])
                        key = tuple(cycle)
                        
                        if key not in visited:
                            visited.add(key)
                            
                            # Verify all edges exist
                            edges = [
                                (min(i, j), max(i, j)),
                                (min(j, k), max(j, k)),
                                (min(k, i), max(k, i))
                            ]
                            
                            if all(edge in edge_set for edge in edges):
                                # Check if this triangle contains any other points
                                # For triangles, we just use the basic algorithm
                                triangles.append([i, j, k])
                                print(f"Found triangle: {[i, j, k]}")
        
        return triangles
    
    def find_minimal_quads(self, adj_list, edge_set, existing_cycles):
        """Find minimal quads in the graph that don't overlap with existing cycles"""
        quads = []
        visited = set()
        existing_points_sets = [frozenset(cycle) for cycle in existing_cycles]
        
        # Find all possible quads
        for i in range(len(self.points)):
            for j in adj_list[i]:
                for k in adj_list[j]:
                    if k != i:  # Don't go back
                        for l in adj_list[k]:
                            if l != j and l in adj_list[i]:  # Completes the quad
                                # Check if this quad has an internal edge (diagonal)
                                if (min(i, k), max(i, k)) in edge_set or (min(j, l), max(j, l)) in edge_set:
                                    continue  # Has a diagonal edge, not a simple face
                                
                                # Create canonical representation
                                cycle = [i, j, k, l]
                                min_idx = cycle.index(min(cycle))
                                canonical = tuple(cycle[min_idx:] + cycle[:min_idx])
                                
                                if canonical not in visited:
                                    visited.add(canonical)
                                    
                                    # Verify all boundary edges exist
                                    edges = [
                                        (min(i, j), max(i, j)),
                                        (min(j, k), max(j, k)),
                                        (min(k, l), max(k, l)),
                                        (min(l, i), max(l, i))
                                    ]
                                    
                                    if all(edge in edge_set for edge in edges):
                                        # Check if this quad significantly overlaps with existing cycles
                                        quad_set = frozenset(cycle)
                                        overlap = False
                                        
                                        for existing_set in existing_points_sets:
                                            # If they share 3 or more points, consider it an overlap
                                            if len(quad_set.intersection(existing_set)) >= 3:
                                                overlap = True
                                                break
                                        
                                        # Also check if this quad is fully covered by 2+ triangles
                                        contained = self._is_covered_by_smaller_cycles(cycle, existing_cycles)
                                        
                                        if not overlap and not contained:
                                            quads.append(cycle)
                                            print(f"Found quad: {cycle}")
        
        return quads
    
    def _is_covered_by_smaller_cycles(self, cycle, smaller_cycles):
        """Check if a cycle is completely covered by a set of smaller cycles"""
        cycle_set = set(cycle)
        
        # Count how many points from each smaller cycle are in this cycle
        covered_points = set()
        
        for smaller in smaller_cycles:
            smaller_set = set(smaller)
            if smaller_set.issubset(cycle_set):
                # If a smaller cycle is completely contained in this one
                covered_points.update(smaller_set)
                
                # If all points are covered, return True
                if covered_points == cycle_set:
                    return True
        
        # Check if all edges of the cycle are covered by edges from smaller cycles
        cycle_edges = set()
        for i in range(len(cycle)):
            p1, p2 = cycle[i], cycle[(i+1) % len(cycle)]
            cycle_edges.add((min(p1, p2), max(p1, p2)))
        
        smaller_edges = set()
        for smaller in smaller_cycles:
            for i in range(len(smaller)):
                p1, p2 = smaller[i], smaller[(i+1) % len(smaller)]
                smaller_edges.add((min(p1, p2), max(p1, p2)))
        
        return cycle_edges.issubset(smaller_edges)
    
    def find_remaining_faces(self, adj_list, edge_set, existing_cycles):
        """Find remaining faces not covered by existing cycles"""
        # Mark all edges that have been used in existing cycles
        used_edges = set()
        for cycle in existing_cycles:
            for i in range(len(cycle)):
                p1, p2 = cycle[i], cycle[(i+1) % len(cycle)]
                used_edges.add((min(p1, p2), max(p1, p2)))
        
        # Find unused edges
        unused_edges = edge_set - used_edges
        if not unused_edges:
            return []  # All edges are already used
            
        print(f"Found {len(unused_edges)} unused edges to explore")
        
        # Try to find faces using the unused edges
        additional_faces = []
        explored_edges = set()
        
        for edge in unused_edges:
            if edge in explored_edges:
                continue
                
            u, v = edge
            face = self._trace_minimal_face(u, v, adj_list, edge_set, explored_edges)
            
            if face and len(face) >= 3:
                # Mark all edges in this face as explored
                for i in range(len(face)):
                    p1, p2 = face[i], face[(i+1) % len(face)]
                    explored_edges.add((min(p1, p2), max(p1, p2)))
                
                # Check if this face is already covered by existing cycles
                face_set = frozenset(face)
                covered = False
                
                for cycle in existing_cycles:
                    if frozenset(cycle) == face_set:
                        covered = True
                        break
                
                # Also check if this face contains internal edges
                has_internal_edge = False
                for i in range(len(face)):
                    for j in range(i+2, len(face)):
                        # Skip adjacent indices and wrap-around case
                        if j == (i+1) % len(face) or (i == 0 and j == len(face)-1):
                            continue
                            
                        if (min(face[i], face[j]), max(face[i], face[j])) in edge_set:
                            has_internal_edge = True
                            break
                    if has_internal_edge:
                        break
                
                if not covered and not has_internal_edge:
                    additional_faces.append(face)
                    print(f"Found additional face: {face}")
        
        return additional_faces
    
    def _trace_minimal_face(self, start_u, start_v, adj_list, edge_set, explored_edges):
        """Trace a minimal face starting with edge (start_u, start_v)"""
        face = [start_u, start_v]
        current = start_v
        prev = start_u
        
        while len(face) < len(self.points):  # Safety limit
            # Find valid neighbors (connected by an edge, not already in the face except start_u)
            valid_neighbors = []
            for neighbor in adj_list[current]:
                edge = (min(current, neighbor), max(current, neighbor))
                
                # Skip if this would create an internal edge back to a non-adjacent vertex
                creates_internal_edge = False
                for i in range(len(face) - 1):  # Skip the last vertex which is current
                    if neighbor == face[i] and i != len(face) - 2:  # Not adjacent
                        creates_internal_edge = True
                        break
                
                if edge in edge_set and not creates_internal_edge:
                    valid_neighbors.append(neighbor)
            
            if not valid_neighbors:
                return None  # Dead end
                
            # If start_u is a valid neighbor and the face has at least 3 vertices, close it
            if start_u in valid_neighbors and len(face) >= 3:
                return face
                
            # Find the "leftmost" turn - in this simplified version, just take any valid neighbor
            next_vertex = None
            for n in valid_neighbors:
                if n != prev:  # Don't go back
                    if n not in face or n == start_u:  # Allow returning to start_u
                        next_vertex = n
                        break
            
            if next_vertex is None:
                return None  # Can't proceed
                
            # Move to next vertex
            face.append(next_vertex)
            prev = current
            current = next_vertex
            
            # If we've closed the loop, return the face
            if current == start_u:
                return face
        
        return None  # Face too large or couldn't close
        
    def find_triangles(self, adj_list, edge_set):
        """Find all valid triangles in the graph"""
        triangles = []
        visited = set()
        
        # First, try to find all triangles
        for i in range(len(self.points)):
            for j in adj_list[i]:
                for k in adj_list[j]:
                    if k in adj_list[i]:  # Forms a triangle
                        # Found a potential triangle
                        cycle = sorted([i, j, k])  # Sort for canonical representation
                        key = tuple(cycle)
                        
                        if key not in visited:
                            # Make sure all edges exist
                            valid = True
                            for idx in range(3):
                                edge = (min(cycle[idx], cycle[(idx+1) % 3]), 
                                       max(cycle[idx], cycle[(idx+1) % 3]))
                                if edge not in edge_set:
                                    valid = False
                                    break
                            
                            if valid:
                                visited.add(key)
                                triangles.append([i, j, k])  # Use original order for better face orientation
                                print(f"Found triangle: {[i, j, k]}")
        
        return triangles
        
    def find_all_cycles(self, adj_list, edge_set, max_size=8):
        """Find all cycles in the graph up to max_size using a more thorough approach"""
        all_cycles = []
        visited_cycles = set()
        
        print("Looking for all valid cycles...")
        
        # First approach: BFS-based cycle finding for each size
        for size in range(3, max_size + 1):
            print(f"Searching for cycles of size {size}...")
            for start in range(len(self.points)):
                paths = self._find_paths(start, size, adj_list, edge_set)
                
                for path in paths:
                    # Complete the cycle
                    cycle = path
                    
                    # Check if it's a valid cycle (all edges exist and no internal edges)
                    valid = True
                    
                    # Check for internal edges in cycles with more than 3 vertices
                    if len(cycle) > 3:
                        for i in range(len(cycle)):
                            for j in range(i + 2, len(cycle)):
                                # Skip cases where j is i+1 or the wrap-around from last to first
                                if j == (i + 1) % len(cycle):
                                    continue
                                if i == 0 and j == len(cycle) - 1:
                                    continue
                                    
                                # Check if this pair forms a disallowed internal edge
                                edge = (min(cycle[i], cycle[j]), max(cycle[i], cycle[j]))
                                if edge in edge_set:
                                    valid = False
                                    print(f"Rejecting cycle {cycle} due to internal edge {cycle[i]}-{cycle[j]}")
                                    break
                            if not valid:
                                break
                    
                    if valid:
                        # Create a canonical representation for deduplication
                        key = frozenset(cycle)
                        
                        if all(frozenset(existing) != key for existing in all_cycles):
                            all_cycles.append(cycle)
                            print(f"Found cycle of size {len(cycle)}: {cycle}")
        
        # Second approach: Face-tracing algorithm for finding faces in planar graph
        faces = self._find_faces_by_edge_traversal(adj_list, edge_set)
        for face in faces:
            # Check if this face is already in our cycles
            key = frozenset(face)
            if all(frozenset(existing) != key for existing in all_cycles):
                all_cycles.append(face)
                print(f"Found additional face through edge traversal: {face}")
        
        return all_cycles
        
    def _find_paths(self, start, length, adj_list, edge_set):
        """Find all valid cycles starting from 'start' with exactly 'length' vertices"""
        valid_paths = []
        
        # Use BFS to build paths of exactly the right length
        queue = [([start], set([start]))]  # (path, visited)
        
        while queue:
            path, visited = queue.pop(0)
            current = path[-1]
            
            # If we're at the path length we want, check if it can be completed to a cycle
            if len(path) == length:
                # Check if the last vertex connects back to start to form a cycle
                if start in adj_list[current]:
                    # Check if all edges exist
                    valid = True
                    for i in range(len(path)):
                        next_idx = (i + 1) % len(path)
                        next_vertex = path[next_idx] if next_idx < len(path) else start
                        edge = (min(path[i], next_vertex), max(path[i], next_vertex))
                        if edge not in edge_set:
                            valid = False
                            break
                    
                    if valid:
                        valid_paths.append(path)
                continue  # No need to extend paths that are already at max length
            
            # Otherwise extend the path with neighbors
            for neighbor in adj_list[current]:
                # For the last vertex, we can revisit start to close the cycle
                if neighbor == start and len(path) == length - 1:
                    new_path = path + [neighbor]
                    valid_paths.append(new_path)
                # Otherwise avoid revisiting vertices
                elif neighbor not in visited:
                    new_path = path + [neighbor]
                    new_visited = visited.copy()
                    new_visited.add(neighbor)
                    queue.append((new_path, new_visited))
        
        return valid_paths
        
    def _find_faces_by_edge_traversal(self, adj_list, edge_set):
        """Find faces in the graph by tracing around edges"""
        # Each edge can be part of at most two faces
        edge_usage = {edge: 0 for edge in edge_set}
        faces = []
        
        # Try to find faces starting from each unused or singly-used edge
        for edge in edge_set:
            if edge_usage[edge] < 2:  # Edge can still be part of another face
                start_u, start_v = edge
                
                # Try to trace a face starting with this edge
                face = self._trace_face(start_u, start_v, adj_list, edge_set, edge_usage)
                
                if face and len(face) >= 3:
                    # Update edge usage for all edges in this face
                    for i in range(len(face)):
                        u = face[i]
                        v = face[(i+1) % len(face)]
                        e = (min(u, v), max(u, v))
                        edge_usage[e] += 1
                    
                    faces.append(face)
        
        return faces
        
    def _trace_face(self, start_u, start_v, adj_list, edge_set, edge_usage):
        """Trace a face in the graph starting with edge (start_u, start_v)"""
        face = [start_u, start_v]
        current = start_v
        prev = start_u
        
        # Follow edges around the face, always taking the leftmost turn
        while True:
            # Find all neighbors of current except prev
            neighbors = [n for n in adj_list[current] if n != prev]
            
            # Filter to only include neighbors that form valid edges
            valid_neighbors = []
            for n in neighbors:
                e = (min(current, n), max(current, n))
                if e in edge_set and edge_usage[e] < 2:
                    valid_neighbors.append(n)
            
            if not valid_neighbors:
                # Can't continue - dead end or all edges used
                return None
                
            # If we can get back to start_u, close the face
            if start_u in valid_neighbors and len(face) >= 3:
                return face
                
            # Order neighbors clockwise from prev (leftmost turn principle)
            # This is a simplified version - in a real implementation, you'd need
            # to use geometric angles based on coordinates
            next_vertex = valid_neighbors[0]  # Just take the first valid neighbor for now
            
            # Move to the next vertex
            face.append(next_vertex)
            prev = current
            current = next_vertex
            
            # If we've returned to start, we've found a face
            if current == start_u:
                return face
                
            # Avoid infinite loops
            if len(face) > len(self.points) + 1:
                return None
        
        return None
        
    def find_quads(self, adj_list, edge_set):
        """Find all valid quadrilaterals in the graph"""
        quads = []
        visited = set()
        
        for i in range(len(self.points)):
            for j in adj_list[i]:
                for k in adj_list[j]:
                    if k != i:  # Don't go back
                        for l in adj_list[k]:
                            if l != j and l in adj_list[i]:  # Complete the quad
                                # Found a potential quad
                                cycle = [i, j, k, l]
                                
                                # Check if the quad is valid (no internal edges)
                                internal_edge1 = (min(i, k), max(i, k))
                                internal_edge2 = (min(j, l), max(j, l))
                                
                                if internal_edge1 in edge_set or internal_edge2 in edge_set:
                                    # This has an internal edge, so it's not a single face
                                    continue
                                
                                # Verify all boundary edges exist
                                edges = [
                                    (min(i, j), max(i, j)),
                                    (min(j, k), max(j, k)),
                                    (min(k, l), max(k, l)),
                                    (min(l, i), max(l, i))
                                ]
                                
                                valid = all(edge in edge_set for edge in edges)
                                
                                if valid:
                                    # Create canonical key for deduplication
                                    min_idx = cycle.index(min(cycle))
                                    canonical = tuple(cycle[min_idx:] + cycle[:min_idx])
                                    
                                    if canonical not in visited:
                                        visited.add(canonical)
                                        quads.append(cycle)
                                        print(f"Found quad: {cycle}")
        
        return quads

    def find_basic_cycles(self, adj_list):
        """Find cycles in the graph when simple methods don't suffice"""
        # Create edge set for quick lookup
        edge_set = set((min(e[0], e[1]), max(e[0], e[1])) for e in self.edges)
        
        # Find all potential cycles using a modified DFS approach
        all_cycles = []
        visited_cycles = set()
        
        print("Attempting to find cycles using breadth-first face detection...")
        
        # Use a breadth-first approach to find cycles (more reliable than DFS for large cycles)
        for start in range(len(self.points)):
            # For each node, try paths of length 3-8
            for length in range(3, 9):  # Try paths that would form cycles of size 3-8
                paths = self._find_paths_bfs(start, length, adj_list)
                
                for path in paths:
                    # Check if path forms a valid cycle
                    if path[0] == path[-1] and len(path) >= 4:  # path includes start node at both ends
                        # Remove duplicate start node from end
                        cycle = path[:-1]
                        
                        # Verify it's a valid cycle (edges exist and no internal edges)
                        if self._is_valid_cycle(cycle, edge_set):
                            # Create canonical form for deduplication
                            min_idx = cycle.index(min(cycle))
                            canonical = tuple(cycle[min_idx:] + cycle[:min_idx])
                            
                            if canonical not in visited_cycles:
                                all_cycles.append(cycle)
                                visited_cycles.add(canonical)
                                print(f"Found cycle with BFS: {cycle}")
            
        # If we found some cycles, select a non-overlapping subset
        if all_cycles:
            print(f"Found {len(all_cycles)} potential cycles with BFS approach")
            return self._select_minimal_cycle_set(all_cycles)
            
        # If no luck with BFS, try a classic DFS approach as fallback
        print("No cycles found with BFS, trying DFS approach...")
        for start in range(len(self.points)):
            stack = [(start, [start], set([start]))]
            max_path_length = 10  # Limit to avoid excessive cycles
            
            while stack:
                node, path, visited = stack.pop()
                
                # Skip if path is too long
                if len(path) > max_path_length:
                    continue
                
                # Try each neighbor
                for neighbor in adj_list[node]:
                    # If we've found a cycle back to start
                    if neighbor == start and len(path) >= 3:
                        cycle = path.copy()
                        
                        # Verify it's a valid cycle (edges exist and no internal edges)
                        if self._is_valid_cycle(cycle, edge_set):
                            # Create canonical form for deduplication
                            cycle_key = tuple(sorted(cycle))
                            
                            if cycle_key not in visited_cycles:
                                all_cycles.append(cycle)
                                visited_cycles.add(cycle_key)
                                print(f"Found cycle with DFS: {cycle}")
                        
                        continue
                    
                    # Skip if already in path (except start)
                    if neighbor != start and neighbor in visited:
                        continue
                    
                    # Continue DFS
                    new_path = path + [neighbor]
                    new_visited = visited.copy()
                    new_visited.add(neighbor)
                    stack.append((neighbor, new_path, new_visited))
        
        print(f"Found {len(all_cycles)} potential cycles with DFS approach")
        
        # If we have a small number, return them directly
        if len(all_cycles) <= 3:
            return all_cycles
            
        # Otherwise, select a minimal set of cycles
        return self._select_minimal_cycle_set(all_cycles)
        
    def _find_paths_bfs(self, start, length, adj_list):
        """Find all paths of exactly 'length' starting from 'start' using BFS"""
        # Start with the single-node path
        paths = [[start]]
        
        # For each step, extend all current paths
        for step in range(1, length):
            new_paths = []
            for path in paths:
                last_node = path[-1]
                
                # Try all neighbors
                for neighbor in adj_list[last_node]:
                    # For the last step, only accept paths back to start
                    if step == length - 1:
                        if neighbor == start:
                            new_paths.append(path + [neighbor])
                    # Otherwise, avoid revisiting nodes (except potentially back to start)
                    elif neighbor not in path or (neighbor == start and step == length - 2):
                        new_paths.append(path + [neighbor])
            
            paths = new_paths
            if not paths:  # If no valid paths left, stop early
                break
                
        return paths
        
    def find_simple_cycles(self, adj_list, edge_set):
        """Find simple cycles of any size by checking each potential face in the graph"""
        simple_cycles = []
        visited = set()
        
        # Try to find cycles of any size up to 8 vertices (reasonable for most maps)
        # Start with the smallest cycles (most reliable) and work up
        for size in range(3, 9):  # Try cycles of size 3 to 8
            # For triangles, use a more direct approach
            if size == 3:
                for i in range(len(self.points)):
                    for j in adj_list[i]:
                        if j > i:  # Avoid duplicates
                            for k in adj_list[j]:
                                if k != i and k in adj_list[i]:
                                    # Found a triangle
                                    cycle = [i, j, k]
                                    key = tuple(sorted(cycle))
                                    
                                    if key not in visited:
                                        # Make sure all edges exist
                                        valid = True
                                        for idx in range(3):
                                            edge = (min(cycle[idx], cycle[(idx+1) % 3]), 
                                                max(cycle[idx], cycle[(idx+1) % 3]))
                                            if edge not in edge_set:
                                                valid = False
                                                break
                                        
                                        if valid:
                                            visited.add(key)
                                            simple_cycles.append(cycle)
                                            print(f"Found triangle: {cycle}")
            else:
                # For larger cycles, use a path-based approach
                for start in range(len(self.points)):
                    # Start a new path from each point
                    paths = [[start]]
                    
                    # Extend paths up to the desired length
                    for _ in range(1, size):
                        new_paths = []
                        for path in paths:
                            last = path[-1]
                            for neighbor in adj_list[last]:
                                # Avoid revisiting vertices except for start
                                if neighbor == start and len(path) == size - 1:
                                    # Found a cycle of the right size
                                    cycle = path.copy()
                                    
                                    # Check if the cycle is valid (all edges exist and no internal edges)
                                    if self._is_valid_cycle(cycle, edge_set):
                                        # Canonicalize the cycle for deduplication
                                        key = tuple(sorted(cycle))
                                        if key not in visited:
                                            visited.add(key)
                                            simple_cycles.append(cycle)
                                            print(f"Found cycle of size {size}: {cycle}")
                                elif neighbor not in path:
                                    new_paths.append(path + [neighbor])
                        
                        paths = new_paths
                        if not paths:  # No more paths to extend
                            break
        
        # Check if we found larger polygons
        larger_polygons = [c for c in simple_cycles if len(c) > 3]
        if larger_polygons:
            print(f"Found {len(larger_polygons)} polygons larger than triangles")
        
        # If we found any cycles, stop here
        if simple_cycles:
            # Select a minimal non-overlapping set if we have too many cycles
            if len(simple_cycles) > 5:  # If we have more than 5 cycles
                return self._select_minimal_cycle_set(simple_cycles)
            return simple_cycles
            
        # If we didn't find any simple cycles, try an alternative approach for quads
        print("No simple cycles found, trying explicit quad detection...")
        for i in range(len(self.points)):
            for j in adj_list[i]:
                if j > i:  # Avoid duplicates
                    for k in adj_list[j]:
                        if k != i:
                            for l in adj_list[k]:
                                if l != j and l != i and l in adj_list[i]:
                                    # Found a quad
                                    cycle = [i, j, k, l]
                                    
                                    # Check if the cycle is valid (all edges exist and no internal edges)
                                    if self._is_valid_cycle(cycle, edge_set):
                                        # Normalize to canonical form
                                        min_idx = cycle.index(min(cycle))
                                        cycle = cycle[min_idx:] + cycle[:min_idx]
                                        key = tuple(cycle)
                                        
                                        if key not in visited:
                                            visited.add(key)
                                            simple_cycles.append(cycle)
                                            print(f"Found quad: {cycle}")
        
        return simple_cycles
        
    def _select_minimal_cycle_set(self, cycles):
        """Select a minimal set of non-overlapping cycles"""
        # Sort by size (smaller first) then by area
        sorted_cycles = sorted(cycles, key=lambda c: (len(c), self._calculate_polygon_area(c)))
        
        # Keep track of selected cycles
        selected = []
        used_points = set()
        
        # Try to cover the graph with non-overlapping cycles
        for cycle in sorted_cycles:
            # If this cycle mostly consists of unused points, select it
            cycle_points = set(cycle)
            overlap = len(cycle_points.intersection(used_points))
            
            # Accept if less than half the points are already used
            if overlap < len(cycle_points) / 2:
                selected.append(cycle)
                used_points.update(cycle_points)
                
        if not selected:
            # If we couldn't find non-overlapping cycles, just take the first few
            selected = sorted_cycles[:min(3, len(sorted_cycles))]
            
        print(f"Selected {len(selected)} cycles from {len(cycles)} total")
        return selected

    def _has_obvious_self_intersection(self, cycle):
        """
        Simple check for obvious self-intersections in a polygon.
        This is a simplified version that reduces false positives.
        """
        # Small cycles (triangles and quads) can't self-intersect with valid edges
        if len(cycle) <= 4:
            return False
        
        # For larger cycles, do a basic check for intersecting non-adjacent edges
        points = [self.points[idx] for idx in cycle]
        n = len(points)
        
        for i in range(n):
            p1 = points[i]
            p2 = points[(i + 1) % n]
            
            for j in range(i + 2, n):
                # Skip if this would be checking adjacent edges
                if (j + 1) % n == i:
                    continue
                    
                p3 = points[j]
                p4 = points[(j + 1) % n]
                
                # Simple intersection check
                if self.do_edges_intersect(p1, p2, p3, p4):
                    return True
        
        return False

    def _cycles_have_significant_overlap(self, cycle1, cycle2):
        """Check if two cycles have significant overlap"""
        set1 = set(cycle1)
        set2 = set(cycle2)
        
        # Calculate shared points
        shared = set1.intersection(set2)
        
        # If they share 3 or more points, consider them as having significant overlap
        return len(shared) >= 3

    def _calculate_polygon_area(self, cycle):
        """Calculate approximate area of a polygon using its vertices"""
        # Use the shoelace formula to calculate area
        points = [self.points[idx] for idx in cycle]
        n = len(points)
        area = 0.0
        
        for i in range(n):
            j = (i + 1) % n
            area += points[i][0] * points[j][1]
            area -= points[j][0] * points[i][1]
        
        area = abs(area) / 2.0
        return area

    def find_fallback_cycles(self, adj_list):
        """Fallback method to find cycles when the primary method fails"""
        # This is a very simple fallback that just looks for small cycles (triangles, quads)
        cycles = []
        visited_cycles = set()
        
        # Edge set for validation
        edge_set = set((min(e[0], e[1]), max(e[0], e[1])) for e in self.edges)
        
        # Try to find triangles (cycles of length 3)
        for i in range(len(self.points)):
            for j in adj_list[i]:
                if j > i:  # Avoid duplicates
                    for k in adj_list[j]:
                        if k != i and k in adj_list[i]:
                            # Found a triangle
                            cycle = [i, j, k]
                            cycle.sort()  # Normalize for deduplication
                            cycle_tuple = tuple(cycle)
                            
                            if cycle_tuple not in visited_cycles:
                                visited_cycles.add(cycle_tuple)
                                cycles.append(cycle)
        
        # Try to find quadrilaterals (cycles of length 4)
        for i in range(len(self.points)):
            for j in adj_list[i]:
                if j > i:  # Avoid duplicates
                    for k in adj_list[j]:
                        if k != i:
                            for l in adj_list[k]:
                                if l != j and l != i and l in adj_list[i]:
                                    # Found a quad
                                    cycle = [i, j, k, l]
                                    
                                    # Normalize
                                    min_idx = cycle.index(min(cycle))
                                    norm_cycle = tuple(cycle[min_idx:] + cycle[:min_idx])
                                    
                                    if norm_cycle not in visited_cycles:
                                        # Verify all edges exist
                                        valid = True
                                        for idx in range(4):
                                            edge = (min(cycle[idx], cycle[(idx+1) % 4]), 
                                                max(cycle[idx], cycle[(idx+1) % 4]))
                                            if edge not in edge_set:
                                                valid = False
                                                break
                                        
                                        if valid:
                                            visited_cycles.add(norm_cycle)
                                            cycles.append(cycle)
        
        print(f"Fallback method found {len(cycles)} cycles")
        return cycles
    
    def compute_neighbors(self):
        # For each tile, compute its neighbors based on shared edges
        for i, tile in enumerate(self.tiles):
            tile["neighbors"] = []
            for j, other_tile in enumerate(self.tiles):
                if i != j:
                    # Check if they share any edge
                    for edge_idx in tile["edges"]:
                        if edge_idx in other_tile["edges"]:
                            tile["neighbors"].append(other_tile["id"])
                            break
    
    def select_tile(self, norm_x, norm_y):
        # Convert to pixel coordinates for calculation
        x = norm_x * self.image_size[0]
        y = norm_y * self.image_size[1]
        
        old_selected = self.selected_tile
        self.selected_tile = None
        
        for i, tile in enumerate(self.tiles):
            # Create a polygon from the tile points
            polygon = []
            for p_idx in tile["points"]:
                px, py = self.points[p_idx]
                polygon.append(QPoint(int(px * self.image_size[0]), int(py * self.image_size[1])))
            
            # Convert list to QPolygon
            q_polygon = QPolygon(polygon)
            
            # Check if the point is inside the polygon
            if q_polygon.containsPoint(QPoint(int(x), int(y)), Qt.OddEvenFill):
                self.selected_tile = i
                print(f"Selected tile {i}: {tile['name']}")
                self.edit_tile_properties(i)
                self.canvas.update()
                return
        
        if old_selected is not None:
            print("Deselected tile")
        self.canvas.update()
    
    def edit_tile_properties(self, tile_idx):
        tile = self.tiles[tile_idx]
        
        # Get name
        name, ok = QInputDialog.getText(
            self, "Tile Properties", "Tile Name:", text=tile["name"]
        )
        if ok and name:
            tile["name"] = name
        
        # Get type
        type_idx = self.TILE_TYPES.index(tile["type"]) if tile["type"] in self.TILE_TYPES else 0
        type_name, ok = QInputDialog.getItem(
            self, "Tile Properties", "Tile Type:", self.TILE_TYPES, current=type_idx, editable=False
        )
        if ok:
            tile["type"] = type_name
        
        # Get population
        population, ok = QInputDialog.getInt(
            self, "Tile Properties", "Population:", value=tile["population"], min=0, max=1000000
        )
        if ok:
            tile["population"] = population
        
        self.canvas.update()

    def delete_selected_point(self):
        """Delete the currently selected point and update edges"""
        if self.selected_point is None:
            return

        idx = self.selected_point

        # Remove the point
        self.points.pop(idx)

        # Update all edges that reference this point or points after it
        updated_edges = []
        for edge in self.edges:
            p1, p2 = edge
            if p1 == idx or p2 == idx:
                continue
            new_edge = (
                p1 if p1 < idx else p1 - 1,
                p2 if p2 < idx else p2 - 1
            )
            updated_edges.append(new_edge)
        self.edges = updated_edges

        # Only remove tiles that referenced the deleted point
        updated_tiles = []
        for tile in self.tiles:
            if idx in tile["points"]:
                continue  # Skip tile containing the deleted point
            # Shift any point indices greater than deleted idx
            new_points = [(p if p < idx else p - 1) for p in tile["points"]]
            new_edges = tile["edges"]  # edges already updated above
            tile["points"] = new_points
            tile["edges"] = new_edges
            updated_tiles.append(tile)
        self.tiles = updated_tiles

        # Reset selection
        self.selected_point = None
        self.shift_selected_point = None

        # Update UI
        self.update_status()
        self.canvas.update()
        print(f"Deleted point {idx}, {len(self.points)} points remain, {len(self.tiles)} tiles remain")

    def reset_zoom(self):
        """Reset zoom and pan to default"""
        self.zoom_factor = 1.0
        self.pan_offset_x = 0
        self.pan_offset_y = 0
        self.zoom_label.setText("Zoom: 100%")
        self.canvas.update()
    
    def export_json(self):
        if not self.tiles:
            QMessageBox.warning(self, "Warning", "No tiles to export. Please generate polygons first.")
            return
        
        file_name, _ = QFileDialog.getSaveFileName(self, "Save JSON", "", "JSON Files (*.json)")
        if not file_name:
            return
        print("exporting", len(self.edges), "edges")
        # Prepare data in the specified format
        export_data = {
            "vertices": self.points,
            "edges": self.edges,
            "tiles": []
        }
        
        for tile in self.tiles:
            export_tile = {
                "name": tile["name"],
                "id": tile["id"],
                "type": tile["type"],
                "population": tile["population"],
                "edges": tile["edges"],
                "neighbors": tile["neighbors"]
            }
            export_data["tiles"].append(export_tile)
        
        # Write to file
        try:
            with open(file_name, 'w') as f:
                json.dump(export_data, f, indent=2)
            QMessageBox.information(self, "Success", f"Data exported to {file_name}")
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to export data: {str(e)}")
    
    def load_json(self):
        file_name, _ = QFileDialog.getOpenFileName(self, "Open JSON", "", "JSON Files (*.json)")
        if not file_name:
            return

        try:
            with open(file_name, 'r') as f:
                data = json.load(f)

            # Validate required fields
            if not all(k in data for k in ("vertices", "edges", "tiles")):
                raise ValueError("Missing keys in JSON file")

            # Load data
            self.points = data["vertices"]
            self.edges = data["edges"]
            raw_tiles = data["tiles"]

            self.tiles = []
            for tile in raw_tiles:
                # Reconstruct point loop from edges
                edge_indices = tile["edges"]
                print(edge_indices)
                tile_edges = [self.edges[i] for i in edge_indices]
                print("loaded:", tile_edges)
                # Attempt to reconstruct point loop from connected edges
                point_chain = self.reconstruct_loop_from_edges(tile_edges)
                print("reconstructed...")
                if point_chain:
                    tile["points"] = point_chain
                else:
                    print(f"Warning: Failed to reconstruct tile {tile['id']} from edges")
                    tile["points"] = []

                self.tiles.append(tile)

            self.selected_point = None
            self.shift_selected_point = None
            self.selected_tile = None
            self.next_tile_id = max((tile["id"] for tile in self.tiles), default=-1) + 1

            self.info_label.setText(f"Loaded map from {file_name}")
            self.update_status()
            self.canvas.update()
            QMessageBox.information(self, "Success", f"Map loaded from {file_name}")

        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to load file: {str(e)}")

    def reconstruct_loop_from_edges(self, edges):
        """Reconstruct a closed point loop from an unordered list of edges"""
        if not edges:
            return []

        # Build adjacency map
        adj = {}
        for a, b in edges:
            adj.setdefault(a, []).append(b)
            adj.setdefault(b, []).append(a)

        # Start from a node with degree 2
        start = None
        for k, v in adj.items():
            if len(v) == 2:
                start = k
                break
        if start is None:
            return []

        # Traverse loop
        path = [start]
        prev = None
        current = start
        while True:
            neighbors = adj[current]
            next_point = neighbors[0] if neighbors[0] != prev else neighbors[1]
            if next_point == start:
                break
            path.append(next_point)
            prev, current = current, next_point
            if len(path) > len(edges):
                return []  # safety: avoid infinite loop

        return path

    def generate_voronoi_tiles(self):
        if not self.points:
            QMessageBox.warning(self, "Warning", "No points to generate Voronoi tiles.")
            return
        
        # Convert normalized points to absolute coordinates
        width, height = self.image_size
        coords = [(x * width, y * height) for (x, y) in self.points]

        try:
            vor = Voronoi(coords)
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to generate Voronoi diagram: {str(e)}")
            return
        # ❌ Clear all previous data: points, edges, tiles
        self.points.clear()
        self.edges.clear()
        self.tiles.clear()
        self.next_tile_id = 0

        for i, region_index in enumerate(vor.point_region):
            region = vor.regions[region_index]
            if not region or -1 in region:  # Skip infinite regions
                continue

            vertices = [vor.vertices[j] for j in region]
            if len(vertices) < 3:
                continue

            # Convert absolute back to normalized
            normalized_vertices = [(x / width, y / height) for x, y in vertices]

            if any(not (0 <= x <= 1 and 0 <= y <= 1) for x, y in normalized_vertices):
                continue


            # Add new points if needed
            point_indices = []
            for vx, vy in normalized_vertices:
                if (vx, vy) not in self.points:
                    self.points.append((vx, vy))
                point_indices.append(self.points.index((vx, vy)))

            # Compute edges between points
            tile_edges = []
            for j in range(len(point_indices)):
                a = point_indices[j]
                b = point_indices[(j + 1) % len(point_indices)]
                edge = (min(a, b), max(a, b))
                if edge not in self.edges:
                    self.edges.append(edge)
                tile_edges.append(self.edges.index(edge))

            self.tiles.append({
                "id": self.next_tile_id,
                "name": f"Tile {self.next_tile_id}",
                "type": "plains",
                "population": 0,
                "edges": tile_edges,
                "points": point_indices,
                "neighbors": []
            })
            self.next_tile_id += 1

        self.compute_neighbors()
        self.update_status()
        self.canvas.update()
        print(f"Generated {len(self.tiles)} Voronoi tiles.")
    
    def sample_weighted_points_from_image(self,weight_image_path, n_samples, image_size):
        """
        Samples normalized (x, y) coordinates from an image based on intensity.
        White areas have higher probability.
        """
        # Load and convert to grayscale
        img = Image.open(weight_image_path).convert("L")
        img = img.resize(image_size, Image.BILINEAR)  # Resize to match current image size
        arr = np.asarray(img, dtype=np.float32)

        # Normalize to probability distribution
        prob = arr / np.sum(arr)
        prob_flat = prob.ravel()

        # Sample pixel indices from weighted distribution
        indices = np.random.choice(prob.size, size=n_samples, replace=False, p=prob_flat)
        ys, xs = np.unravel_index(indices, arr.shape)

        width, height = image_size
        coords = [(x / width, y / height) for x, y in zip(xs, ys)]
        return coords

    def request_voronoi_generation(self):
        # Ask for number of centroids
        num, ok = QInputDialog.getInt(self, "Voronoi Generator", "Number of centroids:", min=3, max=10000, value=1000)
        if not ok:
            return

        # Ask for optional grayscale image
        weight_map_path, _ = QFileDialog.getOpenFileName(self, "Optional Weight Map (White = Dense)", "", "Image Files (*.png *.jpg *.bmp)")
        if weight_map_path:
            try:
                points = self.sample_weighted_points_from_image(weight_map_path, num, self.image_size)
                print(f"Using weighted sampling with image: {weight_map_path}")
            except Exception as e:
                QMessageBox.critical(self, "Error", f"Failed to sample from image: {str(e)}")
                return
        else:
            points = [(random.uniform(0, 1), random.uniform(0, 1)) for _ in range(num)]
            print("Using uniform distribution")

        self.generate_voronoi_from_random_centroids(points)

    
    def generate_voronoi_from_random_centroids(self, n_points):
        if self.image is None:
            QMessageBox.warning(self, "Warning", "No image loaded.")
            return

        width, height = self.image_size
        coords = [(x * width, y * height) for x, y in n_points]

        try:
            vor = Voronoi(coords)
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to compute Voronoi diagram: {str(e)}")
            return

        # Clear previous tiles
        self.tiles.clear()
        self.next_tile_id = 0

        new_points = []
        new_edges = []
        bounding_box = box(0, 0, width, height)  # Clip to image bounds

        for region_index in vor.point_region:
            region = vor.regions[region_index]
            if not region or -1 in region:
                continue

            vertices = [vor.vertices[i] for i in region]
            if len(vertices) < 3:
                continue

            # Convert to Shapely polygon and clip it
            poly = ShapelyPolygon(vertices)
            clipped_poly = poly.intersection(bounding_box)

            if clipped_poly.is_empty or not clipped_poly.is_valid:
                continue

            # If it’s a MultiPolygon (happens rarely), take the largest piece
            if clipped_poly.geom_type == "MultiPolygon":
                clipped_poly = max(clipped_poly.geoms, key=lambda g: g.area)

            coords = list(clipped_poly.exterior.coords)
            if len(coords) < 3:
                continue

            # Normalize and add points
            point_indices = []
            for x, y in coords[:-1]:  # skip the closing duplicate point
                vx, vy = x / width, y / height
                if (vx, vy) not in new_points:
                    new_points.append((vx, vy))
                point_indices.append(new_points.index((vx, vy)) + len(self.points))

            # Generate edges
            tile_edges = []
            for j in range(len(point_indices)):
                a = point_indices[j]
                b = point_indices[(j + 1) % len(point_indices)]
                edge = (min(a, b), max(a, b))
                if edge not in new_edges:
                    new_edges.append(edge)
                tile_edges.append(new_edges.index(edge) + len(self.edges))

            self.tiles.append({
                "id": self.next_tile_id,
                "name": f"Tile {self.next_tile_id}",
                "type": "plains",
                "population": 0,
                "edges": tile_edges,
                "points": point_indices,
                "neighbors": []
            })
            self.next_tile_id += 1

        self.edges.extend(new_edges)
        self.points.extend(new_points)

        self.compute_neighbors()
        self.update_status()
        self.canvas.update()
        print(f"Generated {len(self.tiles)} Voronoi tiles (clipped to image bounds).")

    def detect_biomes(self):
        if not self.tiles or not self.points:
            QMessageBox.warning(self, "Warning", "No tiles available. Please generate tiles first.")
            return

        file_name, _ = QFileDialog.getOpenFileName(self, "Load Biome Mask Image", "", "Image Files (*.png *.jpg *.bmp)")
        if not file_name:
            return

        try:
            img = Image.open(file_name).convert("L")  # Grayscale
            img = img.resize(self.image_size, Image.BILINEAR)
            arr = np.array(img)

            width, height = self.image_size

            for tile in self.tiles:
                # Get center of the tile
                coords = [self.points[i] for i in tile["points"]]
                if not coords:
                    continue
                avg_x = sum(x for x, _ in coords) / len(coords)
                avg_y = sum(y for _, y in coords) / len(coords)

                px = int(avg_x * width)
                py = int(avg_y * height)

                if 0 <= px < width and 0 <= py < height:
                    intensity = arr[py, px]
                    tile["type"] = "sea" if intensity < 128 else "plains"

            QMessageBox.information(self, "Success", "Biome detection completed.")
            self.canvas.update()
            self.update_status()

        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to detect biomes: {str(e)}")


class Canvas(QWidget):
    def __init__(self, parent):
        super().__init__()
        self.parent = parent
        self.setMouseTracking(True)
        self.dragging_point = False
        self.drag_start_pos = None
        
        # Set attribute to enable mouse double click events
        self.setAttribute(Qt.WA_AcceptTouchEvents, True)
    
    def paintEvent(self, event):
        if not self.parent.image:
            return
        
        painter = QPainter(self)
        
        # Apply pan transformation only (not zoom)
        painter.translate(self.parent.pan_offset_x, self.parent.pan_offset_y)
        
        # Calculate base image size that fits in the widget
        base_width = self.width()
        base_height = self.height()
        
        # Calculate the scaled dimensions based on the original image aspect ratio
        img_aspect = self.parent.image.width() / self.parent.image.height()
        
        # Determine dimensions that maintain aspect ratio within view
        if base_width / base_height > img_aspect:
            # Widget is wider than image aspect ratio
            base_height = min(base_height, base_width / img_aspect)
            base_width = base_height * img_aspect
        else:
            # Widget is taller than image aspect ratio
            base_width = min(base_width, base_height * img_aspect)
            base_height = base_width / img_aspect
        
        # Now apply zoom to these base dimensions
        scaled_width = base_width * self.parent.zoom_factor
        scaled_height = base_height * self.parent.zoom_factor
        
        # Scale the image with the zoom factor applied
        scaled_image = self.parent.image.scaled(
            int(scaled_width), 
            int(scaled_height),
            Qt.KeepAspectRatio
        )
        
        # Calculate the position to center the image in the view
        img_x = (self.width() - scaled_width) / 2
        img_y = (self.height() - scaled_height) / 2
        
        # Draw the image
        painter.drawImage(int(img_x), int(img_y), scaled_image)
        
        # Store image display dimensions for coordinate conversion
        self.img_x = img_x
        self.img_y = img_y
        self.img_width = scaled_width
        self.img_height = scaled_height
        
        # Draw a border around the image
        painter.setPen(QPen(Qt.red, 2))
        painter.drawRect(int(img_x), int(img_y), int(scaled_width), int(scaled_height))
        
        # Draw tiles
        for i, tile in enumerate(self.parent.tiles):
            # Create a polygon from the tile points
            polygon = []
            for p_idx in tile["points"]:
                # Make sure p_idx is in range
                if p_idx < len(self.parent.points):
                    px, py = self.parent.points[p_idx]
                    polygon.append(QPoint(
                        int(self.img_x + px * self.img_width),
                        int(self.img_y + py * self.img_height)
                    ))
            
            # Skip if polygon is empty
            if not polygon:
                continue
            
            # Fill the polygon with a semi-transparent color based on tile type
            color = self.get_tile_color(tile["type"])
            if i == self.parent.selected_tile:
                color.setAlpha(150)  # More opaque for selected tile
            else:
                color.setAlpha(80)   # Semi-transparent for other tiles
            
            painter.setBrush(QBrush(color))
            painter.setPen(QPen(Qt.black, 1))
            
            # Convert list to QPolygon
            q_polygon = QPolygon(polygon)
            
            painter.drawPolygon(q_polygon)
            
            # Draw tile name and info at the center
            if len(polygon) > 0:
                # Calculate the center of the polygon
                center_x = sum(p.x() for p in polygon) / len(polygon)
                center_y = sum(p.y() for p in polygon) / len(polygon)
                
                painter.setPen(QPen(Qt.black, 1))
                # Fix deprecation warning by explicitly converting to int
                painter.drawText(QPoint(int(center_x), int(center_y)), f"{tile['name']}")
                painter.drawText(QPoint(int(center_x), int(center_y) + 15), f"{tile['type']} (Pop: {tile['population']})")
        
        # Draw edges
        painter.setPen(QPen(Qt.blue, 2))
        for edge in self.parent.edges:
            p1 = self.parent.points[edge[0]]
            p2 = self.parent.points[edge[1]]
            painter.drawLine(
                int(self.img_x + p1[0] * self.img_width),
                int(self.img_y + p1[1] * self.img_height),
                int(self.img_x + p2[0] * self.img_width),
                int(self.img_y + p2[1] * self.img_height)
            )
        
        # Draw points
        for i, point in enumerate(self.parent.points):
            x = self.img_x + point[0] * self.img_width
            y = self.img_y + point[1] * self.img_height
            
            if i == self.parent.selected_point:
                # Selected point
                painter.setBrush(QBrush(Qt.red))
                painter.setPen(QPen(Qt.black, 1))
                painter.drawEllipse(
                    int(x - self.parent.SELECTED_POINT_RADIUS),
                    int(y - self.parent.SELECTED_POINT_RADIUS),
                    self.parent.SELECTED_POINT_RADIUS * 2,
                    self.parent.SELECTED_POINT_RADIUS * 2
                )
            elif i == self.parent.shift_selected_point:
                # Shift-selected point
                painter.setBrush(QBrush(Qt.yellow))
                painter.setPen(QPen(Qt.black, 1))
                painter.drawEllipse(
                    int(x - self.parent.SELECTED_POINT_RADIUS),
                    int(y - self.parent.SELECTED_POINT_RADIUS),
                    self.parent.SELECTED_POINT_RADIUS * 2,
                    self.parent.SELECTED_POINT_RADIUS * 2
                )
            else:
                # Normal point
                painter.setBrush(QBrush(Qt.green))
                painter.setPen(QPen(Qt.black, 1))
                painter.drawEllipse(
                    int(x - self.parent.POINT_RADIUS),
                    int(y - self.parent.POINT_RADIUS),
                    self.parent.POINT_RADIUS * 2,
                    self.parent.POINT_RADIUS * 2
                )
                
        # Draw point IDs for clarity (if zoomed in enough)
        if self.parent.zoom_factor > 1.5:
            painter.setPen(QPen(Qt.black, 1))
            for i, point in enumerate(self.parent.points):
                x = self.img_x + point[0] * self.img_width
                y = self.img_y + point[1] * self.img_height
                painter.drawText(QPoint(int(x + 10), int(y)), str(i))
    
    def get_tile_color(self, tile_type):
        # Return a color based on the tile type
        color_map = {
            "plains": QColor(240, 240, 140),  # Pale yellow
            "sea": QColor(100, 170, 255),     # Light blue
            "mountain": QColor(139, 137, 137), # Gray
            "city": QColor(200, 0, 0),        # Red
            "jungle": QColor(0, 100, 0),      # Dark green
            "forest": QColor(34, 139, 34),    # Forest green
            "hills": QColor(160, 126, 84)     # Brown
        }
        return color_map.get(tile_type, QColor(200, 200, 200))  # Default gray
    
    def mousePressEvent(self, event):
        if not self.parent.image:
            return
            
        # Convert mouse position from view coordinates to scene coordinates
        scene_pos = self.map_to_scene_coords(event.x(), event.y())
        
        # Check if the click is within the image bounds
        if (self.img_x <= scene_pos.x() <= self.img_x + self.img_width and
            self.img_y <= scene_pos.y() <= self.img_y + self.img_height):
            
            # Middle button for panning
            if event.button() == Qt.MiddleButton:
                self.parent.panning = True
                self.parent.last_mouse_pos = event.pos()
                self.setCursor(Qt.ClosedHandCursor)
                return
            
            # Convert to normalized coordinates
            norm_x = (scene_pos.x() - self.img_x) / self.img_width
            norm_y = (scene_pos.y() - self.img_y) / self.img_height
            
            # Add debug print
            print(f"Click at ({scene_pos.x()}, {scene_pos.y()}), normalized: ({norm_x:.4f}, {norm_y:.4f})")
            
            # Check if we're selecting or interacting with an existing point
            point_selected = False
            for i, point in enumerate(self.parent.points):
                px = self.img_x + point[0] * self.img_width
                py = self.img_y + point[1] * self.img_height
                
                distance = ((scene_pos.x() - px) ** 2 + (scene_pos.y() - py) ** 2) ** 0.5
                
                if distance <= self.parent.POINT_RADIUS * 1.5:  # A bit larger area for easier selection
                    point_selected = True
                    
                    # Right click to delete point
                    if event.button() == Qt.RightButton:
                        if self.parent.selected_point == i:
                            self.parent.delete_selected_point()
                        else:
                            self.parent.selected_point = i
                            self.parent.delete_selected_point()
                        return
                    
                    # Shift key is pressed for edge creation
                    if event.modifiers() & Qt.ShiftModifier and event.button() == Qt.LeftButton:
                        if self.parent.selected_point is not None and self.parent.selected_point != i:
                            self.parent.add_edge(self.parent.selected_point, i)
                        return
                    
                    # Normal left click on a point - select it
                    if event.button() == Qt.LeftButton:
                        # If point is already selected, prepare for dragging
                        if self.parent.selected_point == i:
                            self.drag_start_pos = event.pos()
                            self.dragging_point = True
                            self.setCursor(Qt.SizeAllCursor)
                        else:
                            # Just select the point
                            self.parent.selected_point = i
                            self.parent.shift_selected_point = None
                        break
            
            if point_selected:
                return
                
            # If we have tiles and we're left-clicking without shift, check if we're selecting a tile
            if self.parent.tiles and event.button() == Qt.LeftButton and not (event.modifiers() & Qt.ShiftModifier):
                self.parent.select_tile(norm_x, norm_y)
                # If we selected a tile, we don't want to create a point
                if self.parent.selected_tile is not None:
                    return
            
            # If we're not selecting anything, add a new point
            if event.button() == Qt.LeftButton:
                self.parent.add_point(norm_x, norm_y)
                
    def mouseReleaseEvent(self, event):
        # Stop dragging
        if event.button() == Qt.LeftButton and self.dragging_point:
            self.dragging_point = False
            self.drag_start_pos = None
            self.setCursor(Qt.ArrowCursor)
            
        # Stop panning
        if event.button() == Qt.MiddleButton:
            self.parent.panning = False
            self.setCursor(Qt.ArrowCursor)
            
    def mouseDoubleClickEvent(self, event):
        """Handle double click events to select points"""
        if not self.parent.image:
            return
            
        # Convert mouse position from view coordinates to scene coordinates
        scene_pos = self.map_to_scene_coords(event.x(), event.y())
        
        # Check if the click is within the image bounds
        if (self.img_x <= scene_pos.x() <= self.img_x + self.img_width and
            self.img_y <= scene_pos.y() <= self.img_y + self.img_height):
            
            # Convert to normalized coordinates for reference
            norm_x = (scene_pos.x() - self.img_x) / self.img_width
            norm_y = (scene_pos.y() - self.img_y) / self.img_height
            
            # Search for a point near the click location within a slightly larger radius for easier selection
            selection_radius = self.parent.POINT_RADIUS * 2.0  # Increased radius for easier selection with double-click
            closest_point = None
            min_distance = float('inf')
            
            for i, point in enumerate(self.parent.points):
                px = self.img_x + point[0] * self.img_width
                py = self.img_y + point[1] * self.img_height
                
                distance = ((scene_pos.x() - px) ** 2 + (scene_pos.y() - py) ** 2) ** 0.5
                
                if distance < min_distance:
                    min_distance = distance
                    closest_point = i
                    
            # If we found a point within the selection radius, select it
            if closest_point is not None and min_distance <= selection_radius:
                self.parent.selected_point = closest_point
                self.parent.shift_selected_point = None
                print(f"Double-click selected point {closest_point} at distance {min_distance:.2f}")
                
                # If shift is being held when double-clicking, and we already have a selected point,
                # create an edge between the previously selected point and this one
                if event.modifiers() & Qt.ShiftModifier and self.parent.selected_point is not None:
                    prev_selected = self.parent.selected_point
                    if prev_selected != closest_point:
                        self.parent.add_edge(prev_selected, closest_point)
            else:
                print(f"No point found within selection radius. Closest point at distance {min_distance:.2f}")
                
            self.update()

    def mouseMoveEvent(self, event):
        if not self.parent.image:
            return
            
        # Handle panning
        if self.parent.panning and self.parent.last_mouse_pos:
            delta = event.pos() - self.parent.last_mouse_pos
            self.parent.pan_offset_x += delta.x()
            self.parent.pan_offset_y += delta.y()
            self.parent.last_mouse_pos = event.pos()
            self.update()
            return
            
        # Handle point dragging
        if self.dragging_point and self.parent.selected_point is not None and self.drag_start_pos is not None:
            # Calculate how far we've moved since starting the drag
            delta = event.pos() - self.drag_start_pos
            if delta.manhattanLength() > 3:  # Small threshold to prevent accidental moves
                # Convert mouse position to scene coordinates
                scene_pos = self.map_to_scene_coords(event.x(), event.y())
                
                # Check if we're within image bounds
                if (self.img_x <= scene_pos.x() <= self.img_x + self.img_width and
                    self.img_y <= scene_pos.y() <= self.img_y + self.img_height):
                    
                    # Convert to normalized coordinates
                    norm_x = (scene_pos.x() - self.img_x) / self.img_width
                    norm_y = (scene_pos.y() - self.img_y) / self.img_height
                    
                    # Update the point position
                    self.parent.points[self.parent.selected_point] = (norm_x, norm_y)
                    self.update()
                    
                # Update drag start position for next move calculation
                self.drag_start_pos = event.pos()
                
    def wheelEvent(self, event):
        if not self.parent.image:
            return
        
        # Get the amount of scroll
        delta = event.angleDelta().y()
        
        # Calculate zoom factor - more gentle zooming
        factor = 1.1 if delta > 0 else 0.9
        
        # Get mouse position
        mouse_x = event.x()
        mouse_y = event.y()
        
        # Calculate relative position of mouse over the image (0-1)
        if self.img_width > 0 and self.img_height > 0:
            # Adjust for pan offset
            view_x = mouse_x - self.parent.pan_offset_x
            view_y = mouse_y - self.parent.pan_offset_y
            
            # Check if mouse is over the image
            if (self.img_x <= view_x <= self.img_x + self.img_width and
                self.img_y <= view_y <= self.img_y + self.img_height):
                
                # Calculate relative position within image (0-1)
                rel_x = (view_x - self.img_x) / self.img_width
                rel_y = (view_y - self.img_y) / self.img_height

                rel_x-=0.5
                rel_y-=0.5
                
                # Calculate old dimensions
                old_width = self.img_width
                old_height = self.img_height
                
                # Apply zoom factor
                old_zoom = self.parent.zoom_factor
                self.parent.zoom_factor *= factor
                
                # Limit zoom range
                self.parent.zoom_factor = max(0.1, min(10.0, self.parent.zoom_factor))
                
                # Calculate new dimensions after zoom
                # These will be calculated in paintEvent, so we approximate
                new_width = old_width * (self.parent.zoom_factor / old_zoom)
                new_height = old_height * (self.parent.zoom_factor / old_zoom)
                
                # Calculate the position change
                dx = (new_width - old_width) * rel_x
                dy = (new_height - old_height) * rel_y
                
                # Adjust pan offset to keep the point under mouse in the same position
                self.parent.pan_offset_x -= dx
                self.parent.pan_offset_y -= dy
                
                # Update zoom label
                self.parent.zoom_label.setText(f"Zoom: {int(self.parent.zoom_factor * 100)}%")
                
                # Update the canvas
                self.update()
        
    def map_to_scene_coords(self, x, y):
        """Convert view coordinates to scene coordinates (accounting for zoom and pan)"""
        # Adjust for pan offset
        view_x = x - self.parent.pan_offset_x
        view_y = y - self.parent.pan_offset_y
        
        # Calculate position relative to the image
        rel_x = view_x - self.img_x
        rel_y = view_y - self.img_y
        
        # Convert to scene coordinates
        scene_x = self.img_x + rel_x
        scene_y = self.img_y + rel_y
        
        return QPoint(int(scene_x), int(scene_y))


if __name__ == "__main__":
    import sys
    app = QApplication(sys.argv)
    app.setApplicationName("Tyrell Game Map Editor")
    editor = MapEditor()
    sys.exit(app.exec_())