import numpy as np
import json
from scipy.spatial import Voronoi
import matplotlib.pyplot as plt
from matplotlib.image import imread
from collections import defaultdict
import os
from matplotlib.colors import rgb_to_hsv, hsv_to_rgb
import colorsys

# Texture file for land/sea determination
TEXTURE_FILE = 'maps/TyrellLandMap.png'  # Replace with your texture file path
ENV_TEXTURE_FILE = 'maps/TyrellBiomeMap.png'
LAND_SEA_THRESHOLD = 0.1  # Threshold value: above = land, below = sea

# Define biome colors based on the biome map legend (RGB values)
# The colors may vary slightly from the image, so we'll use closest match
BIOME_COLORS = {
    "Polar Ice Caps": [192, 192, 192],  # Light gray
    "Polar Tundra": [64, 64, 64],       # Dark gray
    "Subarctic Continental": [32, 96, 32],  # Dark green
    "Humid Continental": [128, 192, 32],   # Lime green
    "Oceanic Climate": [64, 192, 64],    # Medium green
    "Cold Rainforests": [0, 128, 64],     # Forest green
    "Mediterranean": [32, 128, 64],      # Medium dark green
    "Humid Subtropical": [96, 192, 32],   # Bright green
    "Cold Steppe": [255, 192, 64],       # Light orange
    "Cold Desert": [224, 160, 128],      # Pink/salmon
    "Hot Steppe": [255, 128, 0],         # Orange
    "Hot Desert": [255, 0, 0],           # Red
    "Tropical Savanna": [255, 128, 64],  # Orange-red
    "Monsoon rainforest": [64, 192, 192], # Teal
    "Tropical Rainforest": [64, 64, 255], # Blue
    "High Mountains": [0, 0, 0]          # Black
}

# Function to find the closest biome color to a given RGB value
def find_closest_biome(pixel_rgb):
    """
    Find the closest biome based on color distance
    
    Args:
        pixel_rgb: RGB values of the pixel (0-255 scale)
    
    Returns:
        String with the closest biome name
    """
    min_distance = float('inf')
    closest_biome = "Unknown"
    
    for biome_name, biome_color in BIOME_COLORS.items():
        # Calculate Euclidean distance in RGB space
        distance = np.sqrt(sum((np.array(pixel_rgb) - np.array(biome_color))**2))
        
        if distance < min_distance:
            min_distance = distance
            closest_biome = biome_name
    
    return closest_biome

def extract_voronoi_polygons(voronoi_data):
    """
    Extract polygons (cells) from Voronoi data as arrays of edge indices.
    
    Args:
        voronoi_data: Dictionary containing Voronoi vertices, edges, and points
    
    Returns:
        List of polygons, where each polygon is a list of edge indices
    """
    # Reconstruct Voronoi vertices and point arrays
    vertices = np.array(voronoi_data["vertices"])
    points = np.array(voronoi_data["points"])
    edges = voronoi_data["edges"]
    
    # Create a mapping from vertex pairs to edge indices
    edge_map = {}
    for i, edge in enumerate(edges):
        # Store both directions for easy lookup
        edge_map[(edge[0], edge[1])] = i
        edge_map[(edge[1], edge[0])] = i
    
    # Recreate the Voronoi diagram to access cell information
    vor = Voronoi(points)
    
    # Store polygons as arrays of edge indices
    polygons = []
    
    # Track edges for visualization
    edge_to_cells = defaultdict(list)
    
    # Process each region (cell/polygon)
    for region_idx, region in enumerate(vor.regions):
        # Skip empty regions or regions with -1 (unbounded)
        if len(region) == 0 or -1 in region:
            continue
        
        # Get the point index for this region
        point_idx = None
        for i, region_point in enumerate(vor.point_region):
            if region_point == region_idx:
                point_idx = i
                break
        
        if point_idx is None:
            continue  # Skip if we can't find the corresponding point
        
        # Create a list to store edge indices for this polygon
        polygon_edges = []
        
        # Sort vertices to form a clockwise/counterclockwise sequence
        # This is necessary to correctly identify adjacent vertices
        center = points[point_idx]
        sorted_vertices = sorted(region, key=lambda v: 
            np.arctan2(vertices[v][1] - center[1], 
                      vertices[v][0] - center[0]))
        
        # Create edges from consecutive vertices
        for i in range(len(sorted_vertices)):
            v1 = sorted_vertices[i]
            v2 = sorted_vertices[(i + 1) % len(sorted_vertices)]
            
            # Look up the edge index
            edge_key = (v1, v2)
            rev_edge_key = (v2, v1)
            
            if edge_key in edge_map:
                edge_idx = edge_map[edge_key]
            elif rev_edge_key in edge_map:
                edge_idx = edge_map[rev_edge_key]
            else:
                # If edge not found, create a new one and add to edges
                edge_idx = len(edges)
                edges.append([v1, v2])
                edge_map[edge_key] = edge_idx
                edge_map[rev_edge_key] = edge_idx
            
            polygon_edges.append(edge_idx)
            edge_to_cells[edge_idx].append(len(polygons))
        
        # Add the polygon to our list
        polygons.append(polygon_edges)
    
    # Update the edge list in voronoi_data to include any new edges
    voronoi_data["edges"] = edges
    
    return polygons, edge_to_cells

def create_tile_dictionary(voronoi_data, polygons):
    """
    Create tile dictionary structure with land/sea types and neighbors.
    
    Args:
        voronoi_data: Dictionary containing Voronoi data
        polygons: List of polygons as edge indices
    
    Returns:
        List of tile dictionaries
    """
    # Create edge to cell mapping to find neighbors
    edge_to_cells = defaultdict(list)
    for i, polygon in enumerate(polygons):
        for edge_idx in polygon:
            edge_to_cells[edge_idx].append(i)
    
    # Initialize tiles
    tiles = []
    
    # Process each polygon into a tile
    for i, polygon in enumerate(polygons):
        # Initialize neighbor list
        neighbors = []
        
        # Find neighbors through shared edges
        for edge_idx in polygon:
            for cell_idx in edge_to_cells[edge_idx]:
                if cell_idx != i and cell_idx not in neighbors:
                    neighbors.append(cell_idx)
        
        # Generate tile
        tile = {
            "name": f"tile{i}",
            "edges": polygon,
            "type": "land",  # Default type, will be updated later
            "biome": "Unknown", # Default biome, will be updated later
            "neighbors": [f"tile{j}" for j in neighbors]
        }
        
        tiles.append(tile)
    
    return tiles

def get_polygon_centers(voronoi_data, polygons):
    """
    Calculate the center point of each polygon.
    
    Args:
        voronoi_data: Dictionary containing Voronoi data
        polygons: List of polygons as edge indices
    
    Returns:
        List of center coordinates for each polygon
    """
    vertices = np.array(voronoi_data["vertices"])
    edges = voronoi_data["edges"]
    
    centers = []
    
    for polygon in polygons:
        # Get all vertices in this polygon
        vertex_indices = set()
        for edge_idx in polygon:
            edge = edges[edge_idx]
            vertex_indices.add(edge[0])
            vertex_indices.add(edge[1])
        
        # Calculate center as average of vertices
        vertex_coords = [vertices[v] for v in vertex_indices]
        center = np.mean(vertex_coords, axis=0)
        centers.append(center)
    
    return centers

def assign_land_sea_from_texture(tiles, polygon_centers):
    """
    Assign land/sea types to tiles based on a texture file.
    
    Args:
        tiles: List of tile dictionaries
        polygon_centers: List of center coordinates for each polygon
    
    Returns:
        Updated tiles with land/sea types
    """
    # Check if we have existing land-sea data
    if os.path.exists('land_sea_map.json'):
        try:
            with open('land_sea_map.json', 'r') as f:
                existing_map = json.load(f)
                
            # Create mapping from tile name to type
            tile_type_map = {tile["name"]: tile["type"] for tile in existing_map}
            
            # Apply types to new tiles where possible
            for tile in tiles:
                if tile["name"] in tile_type_map:
                    tile["type"] = tile_type_map[tile["name"]]
            
            print(f"Loaded land/sea types from existing map for {len(tile_type_map)} tiles")
            return tiles
        except Exception as e:
            print(f"Error loading existing land-sea map: {e}")
    
    # Check if texture file exists
    if not os.path.exists(TEXTURE_FILE):
        print(f"Warning: Texture file '{TEXTURE_FILE}' not found. Using default land/sea distribution.")
        return tiles
    
    # Load texture file
    try:
        texture = imread(TEXTURE_FILE)
        
        # Convert to grayscale if it's color
        if len(texture.shape) == 3:
            texture_gray = 0.2989 * texture[:,:,0] + 0.5870 * texture[:,:,1] + 0.1140 * texture[:,:,2]
        else:
            texture_gray = texture
        
        # Normalize if needed
        if texture_gray.max() > 1.0:
            texture_gray = texture_gray / 255.0
        
        # Get texture dimensions
        height, width = texture_gray.shape
        
        # Create texture flipped if needed (check if map is flipped)
        texture_flipped = np.flipud(texture_gray)  # Flip vertically
        
        # Ensure all polygon centers are within [0,1] range
        normalized_centers = []
        for center in polygon_centers:
            # Clip coordinates to [0,1] range
            normalized_center = np.clip(center, 0, 1)
            normalized_centers.append(normalized_center)
        
        # Assign land/sea based on texture
        for i, center in enumerate(normalized_centers):
            # Map center coordinates to texture coordinates
            # Ensure proper scaling between [0,1] but flip y-coordinate
            # In image coordinates, (0,0) is top-left, but in our map (0,0) is bottom-left
            x = int(center[0] * (width - 1))
            
            # Try both normal and flipped y-coordinate to handle potential coordinate system mismatch
            y_normal = int(center[1] * (height - 1))
            y_flipped = int((1 - center[1]) * (height - 1))
            
            # Bounds checking to prevent index errors
            x = max(0, min(x, width - 1))
            y_normal = max(0, min(y_normal, height - 1))
            y_flipped = max(0, min(y_flipped, height - 1))
            
            # Use the flipped coordinates, which is likely correct for image-to-world mapping
            pixel_value = texture_flipped[y_flipped, x]
            
            # Assign type based on threshold (white = land, black = sea)
            if pixel_value > LAND_SEA_THRESHOLD:
                tiles[i]["type"] = "land"
            else:
                tiles[i]["type"] = "sea"
        
        print(f"Assigned land/sea types from texture file '{TEXTURE_FILE}' with threshold {LAND_SEA_THRESHOLD}")
        return tiles
    
    except Exception as e:
        print(f"Error loading texture file: {e}")
        import traceback
        traceback.print_exc()
        return tiles

def assign_biomes_from_texture(tiles, polygon_centers):
    """
    Assign biome types to tiles based on a biome texture file.
    
    Args:
        tiles: List of tile dictionaries
        polygon_centers: List of center coordinates for each polygon
    
    Returns:
        Updated tiles with biome types
    """
    # Check if biome texture file exists
    if not os.path.exists(ENV_TEXTURE_FILE):
        print(f"Warning: Biome texture file '{ENV_TEXTURE_FILE}' not found. Using default biome distribution.")
        return tiles
    
    # Load biome texture file
    try:
        biome_texture = imread(ENV_TEXTURE_FILE)
        
        # Make sure it's in RGB format
        if len(biome_texture.shape) != 3 or biome_texture.shape[2] < 3:
            print(f"Warning: Biome texture file does not appear to be RGB. Cannot assign biomes.")
            return tiles
        
        # Normalize if needed (0-255 scale to 0-1)
        if biome_texture.max() <= 1.0:
            biome_texture = biome_texture * 255.0
            
        # Create a debug image to verify colors
        plt.figure(figsize=(12, 12))
        plt.imshow(biome_texture)
        plt.title('Biome Map Texture')
        plt.savefig('biome_texture_debug.png')
        plt.close()
        
        # Get texture dimensions
        height, width = biome_texture.shape[:2]
        
        # Create flipped texture
        biome_texture_flipped = np.flipud(biome_texture)  # Flip vertically
        
        # Ensure all polygon centers are within [0,1] range
        normalized_centers = []
        for center in polygon_centers:
            # Clip coordinates to [0,1] range
            normalized_center = np.clip(center, 0, 1)
            normalized_centers.append(normalized_center)
        
        # Create a dictionary to count biome occurrences
        biome_count = defaultdict(int)
        
        # Assign biome based on texture color
        for i, center in enumerate(normalized_centers):
            # Map center coordinates to texture coordinates
            x = int(center[0] * (width - 1))
            y_flipped = int((1 - center[1]) * (height - 1))  # Using flipped coordinates
            
            # Bounds checking to prevent index errors
            x = max(0, min(x, width - 1))
            y_flipped = max(0, min(y_flipped, height - 1))
            
            # Get RGB values from the texture
            rgb_value = biome_texture_flipped[y_flipped, x, :3]  # Get only RGB channels
            
            # Convert to 0-255 scale if needed
            if rgb_value.max() <= 1.0:
                rgb_value = rgb_value * 255.0
                
            # Find the closest biome
            biome = find_closest_biome(rgb_value)
            
            # Assign biome to tile
            tiles[i]["biome"] = biome
            biome_count[biome] += 1
            
            # Debug - log some samples
            if i < 5 or i % 100 == 0:
                print(f"Tile {i}: color RGB={rgb_value}, assigned biome={biome}")
        
        # Print biome distribution
        print("\nBiome distribution:")
        total_tiles = len(tiles)
        for biome, count in sorted(biome_count.items(), key=lambda x: x[1], reverse=True):
            percentage = (count / total_tiles) * 100
            print(f"{biome}: {count} tiles ({percentage:.1f}%)")
        
        print(f"Assigned biomes from texture file '{ENV_TEXTURE_FILE}'")
        return tiles
    
    except Exception as e:
        print(f"Error assigning biomes: {e}")
        import traceback
        traceback.print_exc()
        return tiles

def visualize_biome_map(voronoi_data, tiles, polygon_centers):
    """
    Visualize the biome map with different colors for different biome types.
    """
    vertices = np.array(voronoi_data["vertices"])
    edges = voronoi_data["edges"]
    
    # Create a figure and axis
    fig, ax = plt.subplots(figsize=(16, 12))
    
    # Colors for different biome types
    # Use the same colors as defined in BIOME_COLORS but normalized to 0-1 scale
    biome_plot_colors = {
        biome: [r/255.0, g/255.0, b/255.0] for biome, (r, g, b) in BIOME_COLORS.items()
    }
    
    # Add a default color for unknown biomes
    biome_plot_colors["Unknown"] = [0.5, 0.5, 0.5]  # Gray
    biome_plot_colors["sea"] = [0.6, 0.8, 1.0]      # Light blue for sea
    
    # Load biome texture for background if available
    biome_background = None
    if os.path.exists(ENV_TEXTURE_FILE):
        try:
            biome_texture = imread(ENV_TEXTURE_FILE)
            biome_background = biome_texture
        except Exception as e:
            print(f"Warning: Could not load biome texture for background: {e}")
    
    # Plot the texture in the background if available
    if biome_background is not None:
        ax.imshow(biome_background, extent=[0, 1, 0, 1], alpha=0.3)
    
    # Plot the polygons with biome colors
    biome_count = defaultdict(int)
    
    for i, tile in enumerate(tiles):
        # Get all vertices for this polygon
        vertex_indices = set()
        for edge_idx in tile["edges"]:
            edge = edges[edge_idx]
            vertex_indices.add(edge[0])
            vertex_indices.add(edge[1])
        
        # Get vertex coordinates
        polygon_vertices = [vertices[idx] for idx in vertex_indices]
        
        # Sort vertices around the center
        center = polygon_centers[i]
        sorted_vertices = sorted(polygon_vertices, 
                            key=lambda v: np.arctan2(v[1] - center[1], v[0] - center[0]))
        
        # Determine color based on biome or sea
        if tile["type"] == "sea":
            color = biome_plot_colors["sea"]
            biome_count["sea"] += 1
        else:
            biome = tile["biome"]
            color = biome_plot_colors.get(biome, biome_plot_colors["Unknown"])
            biome_count[biome] += 1
        
        # Create a polygon patch
        polygon_path = plt.Polygon(sorted_vertices, 
                                  fill=True, 
                                  color=color, 
                                  alpha=0.7, 
                                  edgecolor='black', 
                                  linewidth=0.5)
        ax.add_patch(polygon_path)
    
    # Set axis limits
    ax.set_xlim([0, 1])
    ax.set_ylim([0, 1])
    
    # Add title
    ax.set_title('Biome Map')
    
    # Create legend patches
    import matplotlib.patches as mpatches
    
    # Sort biomes by count
    sorted_biomes = sorted(biome_count.items(), key=lambda x: x[1], reverse=True)
    
    # Create legend with top 15 biomes (to avoid overcrowding)
    legend_patches = []
    for biome, count in sorted_biomes[:15]:
        color = biome_plot_colors.get(biome, biome_plot_colors["Unknown"])
        legend_patches.append(mpatches.Patch(color=color, label=f"{biome} ({count})"))
    
    ax.legend(handles=legend_patches, loc='upper right', fontsize='small')
    
    # Show the plot
    plt.tight_layout()
    plt.savefig('biome_map.png', dpi=300)
    print("Biome map visualization saved as 'biome_map.png'")


# Load the Voronoi data from JSON
with open('voronoi_data.json', 'r') as f:
    voronoi_data = json.load(f)

# Extract polygons as edge indices
polygons, edge_to_cells = extract_voronoi_polygons(voronoi_data)

# Create tile dictionaries
tiles = create_tile_dictionary(voronoi_data, polygons)

# Calculate polygon centers
polygon_centers = get_polygon_centers(voronoi_data, polygons)

# Assign land/sea types from texture
tiles = assign_land_sea_from_texture(tiles, polygon_centers)

# Assign biomes from texture
tiles = assign_biomes_from_texture(tiles, polygon_centers)

# Create polygon data
polygon_data = {
    "edges": voronoi_data["edges"],  # Updated edge list
    "vertices": voronoi_data["vertices"],  # Original vertices
    "polygons": polygons  # Array of arrays of edge indices
}

# Save polygon data to JSON
with open('polygons.json', 'w') as f:
    json.dump(polygon_data, f)

# Save tile data to JSON
with open('land_sea_map.json', 'w') as f:
    json.dump(tiles, f)

print(f"Created {len(polygons)} polygons")
print(f"Saved polygon data to 'polygons.json'")
print(f"Saved land-sea map with biome data to 'land_sea_map.json'")

# Generate visualizations
try:
    visualize_biome_map(voronoi_data, tiles, polygon_centers)
except Exception as e:
    print(f"Biome visualization error: {e}")
    import traceback
    traceback.print_exc()