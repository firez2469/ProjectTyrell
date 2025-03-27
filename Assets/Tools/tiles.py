import numpy as np
import json
from scipy.spatial import Voronoi
import matplotlib.pyplot as plt
from matplotlib.image import imread
from collections import defaultdict
import os

# Texture file for land/sea determination
TEXTURE_FILE = 'maps/TyrellLandMap.png'  # Replace with your texture file path
LAND_SEA_THRESHOLD = 0.1  # Threshold value: above = land, below = sea

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
        
        # Debug - save the grayscale texture to verify it
        plt.figure(figsize=(8, 8))
        plt.imshow(texture_gray, cmap='gray')
        plt.title('Grayscale Texture Used for Sampling')
        plt.colorbar(label='Pixel Value')
        plt.savefig('texture_grayscale_debug.png')
        plt.close()
        
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
            
            # Get pixel values for both coordinate systems
            pixel_value_normal = texture_gray[y_normal, x]
            pixel_value_flipped = texture_flipped[y_flipped, x]
            
            # Debug - log a few values to see what's happening
            if i < 10 or i % 100 == 0:
                print(f"Tile {i}: center={center}, texture coords=({x},{y_normal}), flipped=({x},{y_flipped}), " 
                      f"value={pixel_value_normal}, flipped_value={pixel_value_flipped}")
            
            # Use the flipped coordinates, which is likely correct for image-to-world mapping
            pixel_value = pixel_value_flipped
            
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

def visualize_land_sea_map(voronoi_data, tiles, polygon_centers):
    """
    Visualize the land-sea map with different colors for different tile types.
    """
    vertices = np.array(voronoi_data["vertices"])
    edges = voronoi_data["edges"]
    
    # Create a figure and axis
    fig, ax = plt.subplots(figsize=(12, 10))
    
    # Colors for different tile types
    colors = {
        "land": "forestgreen",
        "sea": "steelblue"
    }
    
    # Load texture for background if available
    texture_background = None
    if os.path.exists(TEXTURE_FILE):
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
                
            # Flip the texture vertically to match our coordinate system
            texture_background = np.flipud(texture_gray)
            texture_background = texture_gray
        except Exception as e:
            print(f"Warning: Could not load texture for background: {e}")
    
    # Plot the texture in the background if available
    if texture_background is not None:
        ax.imshow(texture_background, extent=[0, 1, 0, 1], alpha=0.3, cmap='gray')
    
    # Plot the polygons with land/sea colors
    land_count = 0
    sea_count = 0
    
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
        
        # Create a polygon patch
        color = colors.get(tile["type"], "gray")
        polygon_path = plt.Polygon(sorted_vertices, 
                                 fill=True, 
                                 color=color, 
                                 alpha=0.7, 
                                 edgecolor='black', 
                                 linewidth=1)
        ax.add_patch(polygon_path)
        
        # Count land vs sea
        if tile["type"] == "land":
            land_count += 1
        else:
            sea_count += 1
        
        # Add tile name as text (smaller font for readability)
        ax.text(center[0], center[1], tile["name"], 
               ha='center', va='center', fontsize=6,
               color='white' if tile["type"] == "sea" else 'black')
    
    # Set axis limits
    ax.set_xlim([0, 1])
    ax.set_ylim([0, 1])
    
    # Add title and legend
    ax.set_title(f'Land-Sea Map (Threshold: {LAND_SEA_THRESHOLD}, Land: {land_count}, Sea: {sea_count})')
    
    # Create legend patches
    import matplotlib.patches as mpatches
    legend_patches = [mpatches.Patch(color=color, label=f"{tile_type} ({land_count if tile_type=='land' else sea_count})") 
                     for tile_type, color in colors.items()]
    ax.legend(handles=legend_patches, loc='upper right')
    
    # Show the plot
    plt.tight_layout()
    plt.savefig('land_sea_map.png', dpi=300)
    
    # Create a debug visualization showing the original texture and the resulting map
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(16, 8))
    
    # Plot original texture
    if texture_background is not None:
        ax1.imshow(texture_background, cmap='gray')
        ax1.set_title("Original Texture (Flipped)")
        ax1.axis('off')
    
    # Plot tile centers colored by land/sea
    land_centers = [center for center, tile in zip(polygon_centers, tiles) if tile["type"] == "land"]
    sea_centers = [center for center, tile in zip(polygon_centers, tiles) if tile["type"] == "sea"]
    
    if land_centers:
        land_centers = np.array(land_centers)
        ax1.scatter(land_centers[:, 0] * (texture_background.shape[1] - 1), 
                  land_centers[:, 1] * (texture_background.shape[0] - 1), 
                  c='green', alpha=0.5, s=10)
    
    if sea_centers:
        sea_centers = np.array(sea_centers)
        ax1.scatter(sea_centers[:, 0] * (texture_background.shape[1] - 1), 
                  sea_centers[:, 1] * (texture_background.shape[0] - 1), 
                  c='blue', alpha=0.5, s=10)
    
    # Plot resulting map
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
        
        # Create a polygon patch
        color = colors.get(tile["type"], "gray")
        polygon_path = plt.Polygon(sorted_vertices, 
                                 fill=True, 
                                 color=color, 
                                 alpha=0.7, 
                                 edgecolor='black', 
                                 linewidth=0.5)
        ax2.add_patch(polygon_path)
    
    ax2.set_xlim([0, 1])
    ax2.set_ylim([0, 1])
    ax2.set_title("Resulting Land-Sea Map")
    
    plt.tight_layout()
    plt.savefig('texture_vs_map_debug.png', dpi=300)


# Load the Voronoi data from JSON
with open('voronoi_data.json', 'r') as f:
    voronoi_data = json.load(f)

# Get vertices
vertices = np.array(voronoi_data["vertices"])
points = np.array(voronoi_data["points"])

# Recreate Voronoi object
vor = Voronoi(points)

# Extract polygons as edge indices
polygons, edge_to_cells = extract_voronoi_polygons(voronoi_data)

# Create tile dictionaries
tiles = create_tile_dictionary(voronoi_data, polygons)

# Calculate polygon centers
polygon_centers = get_polygon_centers(voronoi_data, polygons)

# Assign land/sea types from texture
tiles = assign_land_sea_from_texture(tiles, polygon_centers)

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
print(f"Saved land-sea map to 'land_sea_map.json'")

# Visualize the land-sea map
try:
    visualize_land_sea_map(voronoi_data, tiles, polygon_centers)
    print("Land-sea map visualization saved as 'land_sea_map.png'")
    print("Debug visualization saved as 'texture_vs_map_debug.png'")
except Exception as e:
    print(f"Visualization error: {e}")
    import traceback
    traceback.print_exc()