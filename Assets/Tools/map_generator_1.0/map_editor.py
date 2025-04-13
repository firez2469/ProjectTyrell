import sys
import json
import numpy as np
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                            QHBoxLayout, QGridLayout, QLabel, QLineEdit, 
                            QComboBox, QPushButton, QListWidget, QColorDialog,
                            QMessageBox, QScrollArea, QSplitter, QFrame)
from PyQt5.QtGui import QPainter, QColor, QBrush, QPen, QPolygonF, QFont
from PyQt5.QtCore import Qt, QPointF, pyqtSignal
from scipy.spatial import Voronoi, voronoi_plot_2d
import matplotlib.pyplot as plt

class MapCanvas(QWidget):
    """Widget for displaying the Voronoi map"""
    tileClicked = pyqtSignal(str)  # Signal to emit when a tile is clicked
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setMinimumSize(800, 600)
        
        # Map data
        self.voronoi_data = None
        self.map_data = None
        self.selected_tile = None
        
        # Display settings
        self.scale_factor = 1.0
        self.offset_x = 0
        self.offset_y = 0
        self.zoom_level = 1.0
        self.pan_x = 0
        self.pan_y = 0
        self.dragging = False
        self.last_mouse_pos = None
        
        # Color mappings for different tile types
        self.type_colors = {
            "land": QColor(100, 200, 100),
            "sea": QColor(100, 100, 220),
            "mountain": QColor(150, 150, 150),
            "desert": QColor(240, 230, 140),
            "forest": QColor(34, 139, 34),
            "plains": QColor(154, 205, 50),
            "hills": QColor(160, 180, 120)
        }
        
        # Biome color mappings
        self.biome_colors = {
            "Polar Ice Caps": QColor(220, 240, 255),
            "Tundra": QColor(200, 220, 220),
            "Taiga": QColor(100, 160, 100),
            "Temperate Forest": QColor(80, 140, 80),
            "Tropical Forest": QColor(40, 120, 40),
            "Grassland": QColor(180, 220, 100),
            "Savanna": QColor(210, 200, 120),
            "Desert": QColor(240, 220, 160),
            "Ocean": QColor(70, 130, 200),
            "Sea": QColor(100, 150, 220),
            "Lake": QColor(130, 180, 240)
        }
        
        # Owner colors
        self.owner_colors = [
            QColor(200, 50, 50),    # Red
            QColor(50, 200, 50),    # Green
            QColor(50, 50, 200),    # Blue
            QColor(200, 200, 50),   # Yellow
            QColor(200, 50, 200),   # Magenta
            QColor(50, 200, 200),   # Cyan
            QColor(150, 100, 50),   # Brown
            QColor(100, 50, 150),   # Purple
            QColor(250, 150, 50),   # Orange
            QColor(150, 250, 50),   # Lime
        ]
        
        # Set up the widget
        self.setMouseTracking(True)
        self.setFocusPolicy(Qt.StrongFocus)
    
    def load_data(self, voronoi_file, map_file=None):
        """Load the Voronoi data and optional map data"""
        try:
            with open(voronoi_file, 'r') as f:
                self.voronoi_data = json.load(f)
            
            # If map data is provided, load it
            if map_file:
                try:
                    with open(map_file, 'r') as f:
                        self.map_data = json.load(f)
                        
                    # Check for missing owner and population fields
                    for tile in self.map_data:
                        if "owner" not in tile:
                            tile["owner"] = -1
                        if "population" not in tile:
                            tile["population"] = 0
                except Exception as e:
                    print(f"Error loading map data: {e}")
                    self.map_data = []
            else:
                # Initialize empty map data
                self.map_data = []
            
            # Make sure we have map data for all tiles
            self.ensure_map_data()
            
            # Update the display
            self.update()
            return True
        except Exception as e:
            print(f"Error loading data: {e}")
            return False
    
    def ensure_map_data(self):
        """Make sure we have map data for all tiles in the Voronoi diagram"""
        # Create a lookup dictionary for existing tiles
        existing_tiles = {tile["name"]: tile for tile in self.map_data}
        
        # Generate proper Voronoi regions
        regions = self.generate_proper_regions()
        
        # Update map data with any missing tiles
        for i, region in enumerate(regions):
            tile_name = f"tile{i}"
            
            if tile_name not in existing_tiles:
                # Create a new tile entry
                new_tile = {
                    "name": tile_name,
                    "edges": region,
                    "type": "land",
                    "biome": "Temperate Forest",
                    "neighbors": [],
                    "owner": -1,
                    "population": 0
                }
                self.map_data.append(new_tile)
            else:
                # Make sure the edges are correctly set
                existing_tiles[tile_name]["edges"] = region
                
                # Ensure owner and population fields exist
                if "owner" not in existing_tiles[tile_name]:
                    existing_tiles[tile_name]["owner"] = -1
                if "population" not in existing_tiles[tile_name]:
                    existing_tiles[tile_name]["population"] = 0
        
        # Update neighbor information
        self.update_neighbors()
    
    def generate_proper_regions(self):
        """Generate proper Voronoi regions using scipy.spatial.Voronoi"""
        if not self.voronoi_data or "points" not in self.voronoi_data:
            print("No valid point data found")
            return self.generate_fallback_grid()
        
        points = np.array(self.voronoi_data["points"])
        
        if len(points) < 4:  # Need at least 4 points for a proper Voronoi diagram
            return self.generate_fallback_grid()
        
        try:
            # Generate Voronoi diagram using scipy
            vor = Voronoi(points)
            
            # Update vertices in our data
            self.voronoi_data["vertices"] = vor.vertices.tolist()
            
            # Create regions as lists of vertex indices
            regions = []
            for i, region in enumerate(vor.regions):
                if not -1 in region and len(region) > 0:
                    regions.append(region)
            
            # Make sure regions are ordered correctly for drawing
            for i, region in enumerate(regions):
                regions[i] = self.order_vertices_into_polygon(region, self.voronoi_data["vertices"])
            
            return regions
        except Exception as e:
            print(f"Error generating Voronoi regions: {e}")
            return self.generate_fallback_grid()
    
    def generate_fallback_grid(self):
        """Generate a fallback grid if Voronoi generation fails"""
        print("Falling back to grid generation")
        regions = []
        vertices = []
        
        grid_size = 10
        for i in range(grid_size):
            for j in range(grid_size):
                # Create a square region
                x, y = i/grid_size, j/grid_size
                size = 1/grid_size
                v1 = len(vertices)
                vertices.append([x, y])
                v2 = len(vertices)
                vertices.append([x+size, y])
                v3 = len(vertices)
                vertices.append([x+size, y+size])
                v4 = len(vertices)
                vertices.append([x, y+size])
                regions.append([v1, v2, v3, v4])
        
        # Update the vertices in voronoi_data
        self.voronoi_data["vertices"] = vertices
        
        return regions
    
    def order_vertices_into_polygon(self, vertex_indices, vertices):
        """Order vertices to form a coherent polygon"""
        if not vertex_indices or len(vertex_indices) < 3:
            return vertex_indices
        
        # Convert to numpy arrays for easier calculations
        verts = np.array([vertices[i] for i in vertex_indices])
        
        # Find center point
        center = verts.mean(axis=0)
        
        # Calculate angles from center
        angles = np.arctan2(verts[:, 1] - center[1], verts[:, 0] - center[0])
        
        # Sort vertex indices by angle
        sorted_indices = [vertex_indices[i] for i in np.argsort(angles)]
        
        return sorted_indices
    
    def update_neighbors(self):
        """Update the neighbor information for all tiles"""
        # Create a mapping of vertex indices to tiles
        vertex_to_tiles = {}
        for tile in self.map_data:
            if "edges" in tile and tile["edges"]:
                for edge_idx in tile["edges"]:
                    if edge_idx not in vertex_to_tiles:
                        vertex_to_tiles[edge_idx] = []
                    vertex_to_tiles[edge_idx].append(tile["name"])
        
        # Find neighbors by looking for shared vertices
        for tile in self.map_data:
            neighbors = set()
            if "edges" in tile and tile["edges"]:
                for edge_idx in tile["edges"]:
                    for neighbor in vertex_to_tiles.get(edge_idx, []):
                        if neighbor != tile["name"]:
                            neighbors.add(neighbor)
            
            tile["neighbors"] = list(neighbors)
    
    def paintEvent(self, event):
        """Draw the map"""
        if not self.voronoi_data:
            return
        
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        
        # Draw background
        painter.fillRect(self.rect(), QColor(240, 240, 240))
        
        # Calculate canvas dimensions
        width, height = self.width(), self.height()
        
        # Find the bounds of the vertices to properly scale
        if self.voronoi_data["vertices"]:
            min_x = min(v[0] for v in self.voronoi_data["vertices"])
            max_x = max(v[0] for v in self.voronoi_data["vertices"])
            min_y = min(v[1] for v in self.voronoi_data["vertices"])
            max_y = max(v[1] for v in self.voronoi_data["vertices"])
            
            # Add padding
            padding = 0.05
            x_range = max(0.01, max_x - min_x)  # Avoid division by zero
            y_range = max(0.01, max_y - min_y)
            min_x -= x_range * padding
            max_x += x_range * padding
            min_y -= y_range * padding
            max_y += y_range * padding
            
            # Calculate scaling to fit the canvas while maintaining aspect ratio
            x_scale = width / x_range
            y_scale = height / y_range
            base_scale = min(x_scale, y_scale) * 0.9
            
            # Apply zoom level
            scale = base_scale * self.zoom_level
            
            # Calculate offset to center the map - apply pan offsets
            offset_x = (width - scale * x_range) / 2 - min_x * scale + self.pan_x
            offset_y = (height - scale * y_range) / 2 - min_y * scale + self.pan_y
        else:
            # Default scaling if no vertices
            base_scale = min(width, height) * 0.8
            scale = base_scale * self.zoom_level
            offset_x = width * 0.1 + self.pan_x
            offset_y = height * 0.1 + self.pan_y
        
        # Draw each tile
        for tile in self.map_data:
            # Skip tiles with invalid or missing edges
            if "edges" not in tile or not tile["edges"]:
                continue
            
            # Get the vertices for this tile
            try:
                vertices = [self.voronoi_data["vertices"][i] for i in tile["edges"] if i < len(self.voronoi_data["vertices"])]
            except (IndexError, TypeError):
                # Skip tiles with invalid vertex indices
                continue
            
            # Skip tiles with too few vertices
            if len(vertices) < 3:
                continue
            
            # Create a polygon
            polygon = QPolygonF()
            for v in vertices:
                x = v[0] * scale + offset_x
                y = v[1] * scale + offset_y
                polygon.append(QPointF(x, y))
            
            # Determine the fill color based on tile attributes
            if "owner" in tile and tile["owner"] != -1:
                # Use owner color
                owner_index = tile["owner"] % len(self.owner_colors)
                fill_color = self.owner_colors[owner_index]
            elif "type" in tile and tile["type"] in self.type_colors:
                # Use type color
                fill_color = self.type_colors[tile["type"]]
                
                # Adjust based on biome if available
                if "biome" in tile and tile["biome"] in self.biome_colors:
                    biome_color = self.biome_colors[tile["biome"]]
                    # Blend the colors (70% type, 30% biome)
                    fill_color = QColor(
                        int(fill_color.red() * 0.7 + biome_color.red() * 0.3),
                        int(fill_color.green() * 0.7 + biome_color.green() * 0.3),
                        int(fill_color.blue() * 0.7 + biome_color.blue() * 0.3)
                    )
            else:
                # Default color
                fill_color = QColor(200, 200, 200)
            
            # Highlight selected tile
            if self.selected_tile == tile["name"]:
                pen = QPen(Qt.red, 3)
                painter.setPen(pen)
            else:
                pen = QPen(Qt.black, 1)
                painter.setPen(pen)
            
            # Draw the tile
            painter.setBrush(QBrush(fill_color))
            painter.drawPolygon(polygon)
            
            # Draw tile name if it's selected or we're zoomed in enough
            if self.selected_tile == tile["name"] or self.scale_factor > 0.8:
                # Calculate center of tile
                center_x = sum(v[0] for v in vertices) / len(vertices) * scale + offset_x
                center_y = sum(v[1] for v in vertices) / len(vertices) * scale + offset_y
                
                # Draw tile name
                painter.setPen(QPen(Qt.black, 1))
                painter.setFont(QFont("Arial", 8))
                painter.drawText(int(center_x), int(center_y), tile["name"])
                
                # If population is significant, draw it
                if "population" in tile and tile["population"] > 0:
                    pop_text = f"Pop: {tile['population']}"
                    painter.drawText(int(center_x), int(center_y) + 15, pop_text)
    
    def mousePressEvent(self, event):
        """Handle mouse clicks to select tiles or start dragging"""
        if not self.voronoi_data or not self.map_data:
            return
        
        # Store the current mouse position for panning
        self.last_mouse_pos = event.pos()
        
        # Handle different mouse buttons
        if event.button() == Qt.LeftButton:
            # Find the bounds of the vertices to properly scale
            if self.voronoi_data["vertices"]:
                min_x = min(v[0] for v in self.voronoi_data["vertices"])
                max_x = max(v[0] for v in self.voronoi_data["vertices"])
                min_y = min(v[1] for v in self.voronoi_data["vertices"])
                max_y = max(v[1] for v in self.voronoi_data["vertices"])
                
                # Add padding
                padding = 0.05
                x_range = max(0.01, max_x - min_x)  # Avoid division by zero
                y_range = max(0.01, max_y - min_y)
                min_x -= x_range * padding
                max_x += x_range * padding
                min_y -= y_range * padding
                max_y += y_range * padding
                
                # Calculate scaling to fit the canvas while maintaining aspect ratio
                width, height = self.width(), self.height()
                x_scale = width / x_range
                y_scale = height / y_range
                base_scale = min(x_scale, y_scale) * 0.9
                scale = base_scale * self.zoom_level
                
                # Calculate offset to center the map
                offset_x = (width - scale * x_range) / 2 - min_x * scale + self.pan_x
                offset_y = (height - scale * y_range) / 2 - min_y * scale + self.pan_y
            else:
                # Default scaling if no vertices
                width, height = self.width(), self.height()
                base_scale = min(width, height) * 0.8
                scale = base_scale * self.zoom_level
                offset_x = width * 0.1 + self.pan_x
                offset_y = height * 0.1 + self.pan_y
            
            # Get click position
            click_x = event.x()
            click_y = event.y()
            
            # Check which tile was clicked
            for tile in self.map_data:
                # Skip tiles with invalid or missing edges
                if "edges" not in tile or not tile["edges"]:
                    continue
                
                try:
                    # Get the vertices for this tile
                    vertices = [self.voronoi_data["vertices"][i] for i in tile["edges"] if i < len(self.voronoi_data["vertices"])]
                except (IndexError, TypeError):
                    # Skip tiles with invalid vertex indices
                    continue
                
                # Skip tiles with too few vertices
                if len(vertices) < 3:
                    continue
                
                # Create a polygon
                polygon = QPolygonF()
                for v in vertices:
                    x = v[0] * scale + offset_x
                    y = v[1] * scale + offset_y
                    polygon.append(QPointF(x, y))
                
                # Check if the click is inside the polygon
                point = QPointF(click_x, click_y)
                if polygon.containsPoint(point, Qt.OddEvenFill):
                    # Emit signal with the selected tile name
                    self.selected_tile = tile["name"]
                    self.tileClicked.emit(tile["name"])
                    self.update()
                    break
        
        elif event.button() == Qt.RightButton:
            # Start dragging with right mouse button
            self.dragging = True
    
    def mouseReleaseEvent(self, event):
        """Handle mouse button release"""
        if event.button() == Qt.RightButton:
            self.dragging = False
    
    def mouseMoveEvent(self, event):
        """Handle mouse movement for panning"""
        if self.dragging and self.last_mouse_pos:
            # Calculate the distance moved
            delta = event.pos() - self.last_mouse_pos
            self.pan_x += delta.x()
            self.pan_y += delta.y()
            self.last_mouse_pos = event.pos()
            self.update()
        
        self.last_mouse_pos = event.pos()
    
    def wheelEvent(self, event):
        """Handle mouse wheel for zooming"""
        zoom_factor = 1.1
        
        # Determine zoom in or out
        if event.angleDelta().y() > 0:
            # Zoom in
            self.zoom_level *= zoom_factor
        else:
            # Zoom out
            self.zoom_level /= zoom_factor
        
        # Limit zoom level to reasonable values
        self.zoom_level = max(0.1, min(10.0, self.zoom_level))
        
        self.update()

class TileEditor(QWidget):
    """Widget for editing tile properties"""
    
    tileUpdated = pyqtSignal()  # Signal to emit when a tile is updated
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.map_data = None
        self.current_tile = None
        
        # Create UI
        self.init_ui()
    
    def init_ui(self):
        """Initialize the UI"""
        layout = QVBoxLayout()
        
        # Title
        title_label = QLabel("Tile Editor")
        title_label.setFont(QFont("Arial", 14, QFont.Bold))
        layout.addWidget(title_label)
        
        # Create a form layout for the fields
        form_layout = QGridLayout()
        
        # Name field
        form_layout.addWidget(QLabel("Name:"), 0, 0)
        self.name_edit = QLineEdit()
        form_layout.addWidget(self.name_edit, 0, 1)
        
        # Type field
        form_layout.addWidget(QLabel("Type:"), 1, 0)
        self.type_combo = QComboBox()
        self.type_combo.addItems(["land", "sea", "mountain", "desert", "forest", "plains", "hills"])
        form_layout.addWidget(self.type_combo, 1, 1)
        
        # Biome field
        form_layout.addWidget(QLabel("Biome:"), 2, 0)
        self.biome_combo = QComboBox()
        self.biome_combo.addItems([
            "Polar Ice Caps", "Tundra", "Taiga", "Temperate Forest", 
            "Tropical Forest", "Grassland", "Savanna", "Desert",
            "Ocean", "Sea", "Lake"
        ])
        form_layout.addWidget(self.biome_combo, 2, 1)
        
        # Owner field
        form_layout.addWidget(QLabel("Owner:"), 3, 0)
        self.owner_edit = QLineEdit()
        self.owner_edit.setPlaceholderText("-1 for no owner")
        form_layout.addWidget(self.owner_edit, 3, 1)
        
        # Population field
        form_layout.addWidget(QLabel("Population:"), 4, 0)
        self.population_edit = QLineEdit()
        form_layout.addWidget(self.population_edit, 4, 1)
        
        # Neighbors field
        form_layout.addWidget(QLabel("Neighbors:"), 5, 0, 1, 2)
        self.neighbors_list = QListWidget()
        self.neighbors_list.setMaximumHeight(100)
        form_layout.addWidget(self.neighbors_list, 6, 0, 1, 2)
        
        # Add the form layout to the main layout
        layout.addLayout(form_layout)
        
        # Add save button
        self.save_button = QPushButton("Apply Changes")
        self.save_button.clicked.connect(self.save_tile)
        layout.addWidget(self.save_button)
        
        # Add stretch to push everything to the top
        layout.addStretch(1)
        
        # Set the layout
        self.setLayout(layout)
        
        # Disable the editor until a tile is selected
        self.set_enabled(False)
    
    def set_map_data(self, map_data):
        """Set the map data reference"""
        self.map_data = map_data
    
    def set_enabled(self, enabled):
        """Enable or disable the editor"""
        self.name_edit.setEnabled(enabled)
        self.type_combo.setEnabled(enabled)
        self.biome_combo.setEnabled(enabled)
        self.owner_edit.setEnabled(enabled)
        self.population_edit.setEnabled(enabled)
        self.neighbors_list.setEnabled(enabled)
        self.save_button.setEnabled(enabled)
    
    def load_tile(self, tile_name):
        """Load a tile into the editor"""
        if not self.map_data:
            return
        
        # Find the tile with the given name
        tile = next((t for t in self.map_data if t["name"] == tile_name), None)
        if not tile:
            self.set_enabled(False)
            self.current_tile = None
            return
        
        # Set current tile
        self.current_tile = tile
        
        # Fill in the fields
        self.name_edit.setText(tile["name"])
        
        # Set the type
        index = self.type_combo.findText(tile["type"])
        if index >= 0:
            self.type_combo.setCurrentIndex(index)
        
        # Set the biome
        index = self.biome_combo.findText(tile["biome"])
        if index >= 0:
            self.biome_combo.setCurrentIndex(index)
        
        # Set the owner (ensure it exists)
        if "owner" not in tile:
            tile["owner"] = -1
        self.owner_edit.setText(str(tile["owner"]))
        
        # Set the population (ensure it exists)
        if "population" not in tile:
            tile["population"] = 0
        self.population_edit.setText(str(tile["population"]))
        
        # Set the neighbors
        self.neighbors_list.clear()
        for neighbor in tile["neighbors"]:
            self.neighbors_list.addItem(neighbor)
        
        # Enable the editor
        self.set_enabled(True)
    
    def save_tile(self):
        """Save the tile changes"""
        if not self.current_tile:
            return
        
        # Update the tile with the form values
        self.current_tile["name"] = self.name_edit.text()
        self.current_tile["type"] = self.type_combo.currentText()
        self.current_tile["biome"] = self.biome_combo.currentText()
        
        # Ensure owner and population fields exist
        if "owner" not in self.current_tile:
            self.current_tile["owner"] = -1
        if "population" not in self.current_tile:
            self.current_tile["population"] = 0
        
        # Handle owner field
        try:
            self.current_tile["owner"] = int(self.owner_edit.text())
        except ValueError:
            self.current_tile["owner"] = -1
        
        # Handle population field
        try:
            self.current_tile["population"] = int(self.population_edit.text())
        except ValueError:
            self.current_tile["population"] = 0
        
        # Get neighbors from list widget
        neighbors = []
        for i in range(self.neighbors_list.count()):
            neighbors.append(self.neighbors_list.item(i).text())
        self.current_tile["neighbors"] = neighbors
        
        # Emit signal that the map should be updated
        self.tileUpdated.emit()

class MapEditor(QMainWindow):
    """Main window for the map editor"""
    
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Voronoi Map Editor")
        self.resize(1200, 800)
        
        # Data
        self.voronoi_data = None
        self.map_data = []
        
        # Initialize UI
        self.init_ui()
    
    def init_ui(self):
        """Initialize the UI"""
        # Create central widget
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        # Create main layout
        main_layout = QHBoxLayout()
        
        # Create map canvas
        self.map_canvas = MapCanvas()
        self.map_canvas.tileClicked.connect(self.on_tile_clicked)
        
        # Create tile editor
        self.tile_editor = TileEditor()
        
        # Create a splitter to allow resizing
        splitter = QSplitter(Qt.Horizontal)
        splitter.addWidget(self.map_canvas)
        
        # Put the tile editor in a scroll area
        scroll_area = QScrollArea()
        scroll_area.setWidget(self.tile_editor)
        scroll_area.setWidgetResizable(True)
        scroll_area.setMinimumWidth(300)
        scroll_area.setMaximumWidth(400)
        
        splitter.addWidget(scroll_area)
        
        # Set initial sizes
        splitter.setSizes([800, 400])
        
        # Add the splitter to the main layout
        main_layout.addWidget(splitter)
        
        # Set the layout
        central_widget.setLayout(main_layout)
        
        # Add toolbar with actions
        self.create_toolbar()
        
        # Add status bar info
        self.statusBar().showMessage("Ready. Zoom with mouse wheel, pan with right-click drag, select with left-click")
        
        # Load test data
        self.load_data("voronoi_data.json", "land_sea_map.json")
    
    def create_toolbar(self):
        """Create the toolbar with actions"""
        toolbar = self.addToolBar("Map Tools")
        
        # Load action
        load_action = toolbar.addAction("Load Data")
        load_action.triggered.connect(self.on_load_data)
        
        # Save action
        save_action = toolbar.addAction("Save Map")
        save_action.triggered.connect(self.on_save_map)
        
        # Reset view action
        reset_view_action = toolbar.addAction("Reset View")
        reset_view_action.triggered.connect(self.reset_view)
    
    def reset_view(self):
        """Reset the map view to default zoom and pan"""
        self.map_canvas.zoom_level = 1.0
        self.map_canvas.pan_x = 0
        self.map_canvas.pan_y = 0
        self.map_canvas.update()
        self.statusBar().showMessage("View reset to default")
    
    def load_data(self, voronoi_file, map_file=None):
        """Load the Voronoi and map data"""
        success = self.map_canvas.load_data(voronoi_file, map_file)
        if success:
            self.map_data = self.map_canvas.map_data
            self.tile_editor.set_map_data(self.map_data)
            self.statusBar().showMessage(f"Loaded data from {voronoi_file}")
        else:
            self.statusBar().showMessage("Error loading data")
    
    def on_load_data(self):
        """Handle the load data action"""
        # In a real application, you would use QFileDialog here
        # For this example, we'll just load the hardcoded files
        self.load_data("voronoi_data.json", "land_sea_map.json")
    
    def on_save_map(self):
        """Handle the save map action"""
        try:
            with open("map_data.json", 'w') as f:
                json.dump(self.map_data, f, indent=2)
            self.statusBar().showMessage("Map saved to map_data.json")
        except Exception as e:
            self.statusBar().showMessage(f"Error saving map: {e}")
            QMessageBox.critical(self, "Save Error", f"Could not save map: {e}")
    
    def on_tile_clicked(self, tile_name):
        """Handle a tile being clicked"""
        self.tile_editor.load_tile(tile_name)
        self.statusBar().showMessage(f"Selected {tile_name}")
    
    def update_map(self):
        """Update the map display"""
        self.map_canvas.update()
        self.statusBar().showMessage("Map updated")

def main():
    app = QApplication(sys.argv)
    editor = MapEditor()
    editor.show()
    sys.exit(app.exec_())

if __name__ == "__main__":
    main()