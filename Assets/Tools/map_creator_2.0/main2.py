import sys
import json
import numpy as np
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QPushButton, 
                             QVBoxLayout, QHBoxLayout, QFileDialog, QLabel, 
                             QInputDialog, QComboBox, QSpinBox, QMessageBox)
from PyQt5.QtGui import QPainter, QColor, QPen, QBrush, QPixmap, QImage, QPolygon
from PyQt5.QtCore import Qt, QPoint, QRectF


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
        
        # Controls
        controls_layout = QVBoxLayout()
        
        # Load image button
        load_btn = QPushButton("Load Image")
        load_btn.clicked.connect(self.load_image)
        controls_layout.addWidget(load_btn)
        
        # Generate polygons button
        gen_poly_btn = QPushButton("Generate Polygons")
        gen_poly_btn.clicked.connect(self.generate_polygons)
        controls_layout.addWidget(gen_poly_btn)
        
        # Delete point button
        delete_btn = QPushButton("Delete Selected Point")
        delete_btn.clicked.connect(self.delete_selected_point)
        controls_layout.addWidget(delete_btn)
        
        # Reset zoom button
        reset_zoom_btn = QPushButton("Reset Zoom")
        reset_zoom_btn.clicked.connect(self.reset_zoom)
        controls_layout.addWidget(reset_zoom_btn)
        
        # Clear all button
        clear_btn = QPushButton("Clear All")
        clear_btn.clicked.connect(self.clear_all)
        controls_layout.addWidget(clear_btn)
        
        # Export button
        export_btn = QPushButton("Export JSON")
        export_btn.clicked.connect(self.export_json)
        controls_layout.addWidget(export_btn)
        
        # Info label
        self.info_label = QLabel("Load an image to start")
        controls_layout.addWidget(self.info_label)
        
        # Status display
        self.status_label = QLabel("Points: 0, Edges: 0, Tiles: 0")
        controls_layout.addWidget(self.status_label)
        
        # Zoom info
        self.zoom_label = QLabel("Zoom: 100%")
        controls_layout.addWidget(self.zoom_label)
        
        # Add the controls to the main layout
        control_widget = QWidget()
        control_widget.setLayout(controls_layout)
        main_layout.addWidget(control_widget, 1)  # 4:1 ratio
        
        # Set the main layout
        central_widget = QWidget()
        central_widget.setLayout(main_layout)
        self.setCentralWidget(central_widget)
        
        self.setWindowTitle("Map Editor")
        self.setGeometry(100, 100, 1000, 600)
        self.show()
        
        # Instructions
        self.show_instructions()
    
    def show_instructions(self):
        msg = QMessageBox()
        msg.setWindowTitle("Instructions")
        msg.setText("Map Editor Instructions:\n\n"
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
                    "- Click on a tile to set its properties\n"
                    "- Export to JSON when finished")
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
            self.edges.append(edge)
            # Make the destination point the new selected point
            self.selected_point = idx2
            self.shift_selected_point = None
            self.update_status()
            self.canvas.update()
    
    def update_status(self):
        self.status_label.setText(f"Points: {len(self.points)}, Edges: {len(self.edges)}, Tiles: {len(self.tiles)}")
    
    def get_edge_index(self, p1, p2):
        for i, edge in enumerate(self.edges):
            if edge == (p1, p2):
                return i
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
        if len(cycle) <= 4:
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
    
    def generate_polygons(self):
        """Generate tiles with a more comprehensive approach to finding all valid polygons"""
        if len(self.points) < 3 or len(self.edges) < 3:
            QMessageBox.warning(self, "Warning", "Need at least 3 points and 3 edges to form polygons")
            return
        
        print(f"Generating polygons from {len(self.points)} points and {len(self.edges)} edges")
        
        # Step 1: Prepare graph representation
        adj_list = [[] for _ in range(len(self.points))]
        for e in self.edges:
            adj_list[e[0]].append(e[1])
            adj_list[e[1]].append(e[0])
        
        # Create edge set for validation
        edge_set = set((min(e[0], e[1]), max(e[0], e[1])) for e in self.edges)
        
        # Step 2: Find all possible cycles - including triangles, quads, and larger polygons
        cycles = []
        
        # Find triangles
        triangles = self.find_triangles(adj_list, edge_set)
        if triangles:
            cycles.extend(triangles)
            print(f"Found {len(triangles)} triangles")
            
        # Also look for quads and larger cycles
        quads = self.find_quads(adj_list, edge_set)
        if quads:
            cycles.extend(quads)
            print(f"Found {len(quads)} quads")
            
        # Try to find larger cycles using BFS
        larger_cycles = self.find_all_cycles(adj_list, edge_set)
        if larger_cycles:
            for cycle in larger_cycles:
                # Only add cycles we haven't already found
                key = frozenset(cycle)
                if all(frozenset(existing) != key for existing in cycles):
                    cycles.append(cycle)
            print(f"Found {len(larger_cycles)} additional cycles")
        
        # Final check for minimal cycles
        if not cycles:
            # One last attempt with the fallback method
            cycles = self.find_fallback_cycles(adj_list)
            
        if not cycles:
            QMessageBox.warning(self, "Warning", "No valid polygons could be found")
            return
        
        print(f"Found {len(cycles)} total valid cycles")
        
        # Debug: print details of each cycle
        for i, cycle in enumerate(cycles):
            print(f"Cycle {i} contains points: {cycle}")
        
        # Step 3: Create tiles from cycles
        self.tiles = []
        self.next_tile_id = 0
        new_tiles = []
        processed_cycles = set()
        
        # Process each cycle
        for cycle in cycles:
            # Create a canonical representation for deduplication
            key = frozenset(cycle)
            
            # Skip if we've already processed an identical cycle
            if key in processed_cycles:
                continue
                
            processed_cycles.add(key)
            
            # Create the edges array
            edges_indices = []
            valid = True
            for i in range(len(cycle)):
                p1 = cycle[i]
                p2 = cycle[(i + 1) % len(cycle)]
                edge_index = self.get_edge_index(min(p1, p2), max(p1, p2))
                if edge_index != -1:
                    edges_indices.append(edge_index)
                else:
                    valid = False
                    break
            
            # Only create the tile if we found all edges
            if valid and len(edges_indices) == len(cycle):
                new_tile = {
                    "id": self.next_tile_id,
                    "name": f"Tile {self.next_tile_id}",
                    "type": "plains",
                    "population": 0,
                    "edges": edges_indices,
                    "points": cycle,
                    "neighbors": []
                }
                new_tiles.append(new_tile)
                self.next_tile_id += 1
                print(f"Created new tile {new_tile['id']} with points: {cycle}")
        
        # Update the tile collection and compute neighbors
        self.tiles = new_tiles
        self.compute_neighbors()
        
        # Report results
        print(f"Created {len(new_tiles)} new tiles")
        print(f"Total tiles: {len(self.tiles)}")
        
        self.update_status()
        self.canvas.update()
        
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
        """Find all cycles in the graph up to max_size"""
        all_cycles = []
        visited_cycles = set()
        
        print("Looking for all valid cycles...")
        
        # Try to find cycles of each size
        for size in range(3, max_size + 1):
            for start in range(len(self.points)):
                # Use BFS to find cycles of exactly this length
                queue = [(start, [start])]
                
                while queue:
                    node, path = queue.pop(0)
                    
                    # If path is already at size-1, only look for connections back to start
                    if len(path) == size - 1:
                        if start in adj_list[node]:  # Can complete the cycle
                            cycle = path.copy()
                            
                            # Check if all required edges exist
                            valid = True
                            for i in range(len(cycle)):
                                edge = (min(cycle[i], cycle[(i+1) % len(cycle)]), 
                                       max(cycle[i], cycle[(i+1) % len(cycle)]))
                                if i == len(cycle) - 1:
                                    edge = (min(cycle[i], start), max(cycle[i], start))
                                if edge not in edge_set:
                                    valid = False
                                    break
                            
                            # Check for internal edges (edges that shouldn't be there)
                            if valid and size > 3:  # Triangles can't have internal edges
                                test_cycle = cycle + [start]  # Complete cycle for testing
                                for i in range(len(test_cycle)):
                                    for j in range(i + 2, len(test_cycle)):
                                        # Skip adjacent nodes and wrap-around
                                        if (j == i + 1) or (i == 0 and j == len(test_cycle) - 1):
                                            continue
                                            
                                        # Check if non-adjacent nodes have an edge between them
                                        if (min(test_cycle[i], test_cycle[j]), max(test_cycle[i], test_cycle[j])) in edge_set:
                                            valid = False
                                            print(f"Rejecting cycle due to internal edge: {test_cycle[i]}-{test_cycle[j]}")
                                            break
                                    if not valid:
                                        break
                            
                            if valid:
                                # Create canonical representation for deduplication
                                complete_cycle = cycle + [start]
                                min_idx = complete_cycle.index(min(complete_cycle))
                                canonical = tuple(complete_cycle[min_idx:] + complete_cycle[:min_idx])
                                
                                if canonical not in visited_cycles:
                                    visited_cycles.add(canonical)
                                    all_cycles.append(cycle + [start])
                                    print(f"Found cycle of size {size}: {cycle + [start]}")
                    
                    # Otherwise, extend path
                    elif len(path) < size - 1:
                        for neighbor in adj_list[node]:
                            if neighbor != start and neighbor not in path:  # Avoid cycles too early
                                queue.append((neighbor, path + [neighbor]))
        
        return all_cycles
        
    def find_quads(self, adj_list, edge_set):
        """Find all valid quadrilaterals in the graph"""
        quads = []
        visited = set()
        
        for i in range(len(self.points)):
            for j in adj_list[i]:
                if j > i:  # Avoid duplicates
                    for k in adj_list[j]:
                        if k != i:
                            for l in adj_list[k]:
                                if l != j and l != i and l in adj_list[i]:
                                    # Found a quad
                                    cycle = [i, j, k, l]
                                    
                                    # Check for internal edges
                                    has_internal_edge = False
                                    if (min(i, k), max(i, k)) in edge_set or (min(j, l), max(j, l)) in edge_set:
                                        has_internal_edge = True
                                        print(f"Rejecting quad {cycle} because it has internal edges")
                                    
                                    if not has_internal_edge:
                                        # Normalize to canonical form
                                        min_idx = cycle.index(min(cycle))
                                        cycle = cycle[min_idx:] + cycle[:min_idx]
                                        key = tuple(cycle)
                                        
                                        if key not in visited:
                                            # Make sure all edges exist
                                            valid = True
                                            for idx in range(4):
                                                edge = (min(cycle[idx], cycle[(idx+1) % 4]), 
                                                       max(cycle[idx], cycle[(idx+1) % 4]))
                                                if edge not in edge_set:
                                                    valid = False
                                                    break
                                            
                                            if valid:
                                                visited.add(key)
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
        
        # Get the index of the point to delete
        idx = self.selected_point
        
        # Remove the point
        self.points.pop(idx)
        
        # Update all edges that reference this point or points after it
        updated_edges = []
        for edge in self.edges:
            p1, p2 = edge
            # Skip edges that include the deleted point
            if p1 == idx or p2 == idx:
                continue
                
            # Adjust indices for points that come after the deleted point
            new_edge = (
                p1 if p1 < idx else p1 - 1,
                p2 if p2 < idx else p2 - 1
            )
            updated_edges.append(new_edge)
        
        self.edges = updated_edges
        
        # Clear any tiles as they're now invalid
        self.tiles = []
        
        # Reset selection
        self.selected_point = None
        self.shift_selected_point = None
        
        # Update UI
        self.update_status()
        self.canvas.update()
        print(f"Deleted point {idx}, {len(self.points)} points remain")
    
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
        painter.drawImage(img_x, img_y, scaled_image)
        
        # Store image display dimensions for coordinate conversion
        self.img_x = img_x
        self.img_y = img_y
        self.img_width = scaled_width
        self.img_height = scaled_height
        
        # Draw a border around the image
        painter.setPen(QPen(Qt.red, 2))
        painter.drawRect(img_x, img_y, scaled_width, scaled_height)
        
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
                    x - self.parent.SELECTED_POINT_RADIUS,
                    y - self.parent.SELECTED_POINT_RADIUS,
                    self.parent.SELECTED_POINT_RADIUS * 2,
                    self.parent.SELECTED_POINT_RADIUS * 2
                )
            elif i == self.parent.shift_selected_point:
                # Shift-selected point
                painter.setBrush(QBrush(Qt.yellow))
                painter.setPen(QPen(Qt.black, 1))
                painter.drawEllipse(
                    x - self.parent.SELECTED_POINT_RADIUS,
                    y - self.parent.SELECTED_POINT_RADIUS,
                    self.parent.SELECTED_POINT_RADIUS * 2,
                    self.parent.SELECTED_POINT_RADIUS * 2
                )
            else:
                # Normal point
                painter.setBrush(QBrush(Qt.green))
                painter.setPen(QPen(Qt.black, 1))
                painter.drawEllipse(
                    x - self.parent.POINT_RADIUS,
                    y - self.parent.POINT_RADIUS,
                    self.parent.POINT_RADIUS * 2,
                    self.parent.POINT_RADIUS * 2
                )
                
        # Draw point IDs for clarity (if zoomed in enough)
        if self.parent.zoom_factor > 1.5:
            painter.setPen(QPen(Qt.black, 1))
            for i, point in enumerate(self.parent.points):
                x = self.img_x + point[0] * self.img_width
                y = self.img_y + point[1] * self.img_height
                painter.drawText(QPoint(x + 10, y), str(i))
    
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
        
        return QPoint(scene_x, scene_y)


if __name__ == "__main__":
    app = QApplication(sys.argv)
    editor = MapEditor()
    sys.exit(app.exec_())