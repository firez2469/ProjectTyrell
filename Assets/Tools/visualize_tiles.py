import json
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon
from matplotlib.collections import PatchCollection
import random

def visualize_polygons_from_json(json_file):
    """
    Load polygons from JSON file and visualize them with random colors.
    
    Args:
        json_file (str): Path to the JSON file containing polygon data
    """
    # Load polygon data from JSON
    with open(json_file, 'r') as f:
        polygon_data = json.load(f)
    
    # Extract data
    vertices = np.array(polygon_data["vertices"])
    edges = polygon_data["edges"]
    polygons = polygon_data["polygons"]
    
    # Create figure and axis
    fig, ax = plt.subplots(figsize=(12, 10))
    
    # Set random seed for reproducible colors
    random.seed(42)
    
    # Create patches for each polygon
    patches = []
    polygon_colors = []
    
    for polygon_edges in polygons:
        # Get vertices for this polygon
        polygon_vertices = []
        seen_vertices = set()
        
        # Extract vertices for each edge in the polygon
        for edge_idx in polygon_edges:
            v1_idx, v2_idx = edges[edge_idx]
            
            if v1_idx not in seen_vertices:
                seen_vertices.add(v1_idx)
                polygon_vertices.append(vertices[v1_idx])
            
            if v2_idx not in seen_vertices:
                seen_vertices.add(v2_idx)
                polygon_vertices.append(vertices[v2_idx])
        
        # Convert to numpy array
        polygon_vertices = np.array(polygon_vertices)
        
        # Sort vertices to form a proper polygon (clockwise/counterclockwise order)
        # Calculate centroid
        centroid = polygon_vertices.mean(axis=0)
        
        # Calculate angles of each vertex relative to centroid
        angles = np.arctan2(polygon_vertices[:, 1] - centroid[1],
                           polygon_vertices[:, 0] - centroid[0])
        
        # Sort vertices by angle
        sorted_indices = np.argsort(angles)
        sorted_vertices = polygon_vertices[sorted_indices]
        
        # Create a polygon patch
        patch = Polygon(sorted_vertices, True)
        patches.append(patch)
        
        # Generate a random color for this polygon
        polygon_colors.append(np.array([random.random(), random.random(), random.random(), 0.7]))
    
    # Create a collection of patches
    collection = PatchCollection(patches, cmap=plt.cm.hsv, alpha=0.7, edgecolor='black', linewidth=1)
    
    # Set face colors to our random colors
    collection.set_facecolor(polygon_colors)
    
    # Add the collection to the axis
    ax.add_collection(collection)
    
    # Plot vertices
    ax.scatter(vertices[:, 0], vertices[:, 1], c='blue', s=10, alpha=0.5, label='Vertices')
    
    # Plot edges
    for edge in edges:
        v1, v2 = edge
        ax.plot([vertices[v1][0], vertices[v2][0]], 
                [vertices[v1][1], vertices[v2][1]], 
                'k-', linewidth=0.5, alpha=0.3)
    
    # Set axis limits
    ax.set_xlim([-0.1, 1.1])
    ax.set_ylim([-0.1, 1.1])
    
    # Add title and legend
    ax.set_title(f'Voronoi Diagram with {len(polygons)} Randomly Colored Polygons')
    ax.legend(loc='upper right')
    
    # Display plot in equal aspect ratio
    ax.set_aspect('equal')
    
    # Adjust layout
    plt.tight_layout()
    
    # Save the visualization
    plt.savefig('colored_polygons.png', dpi=300)
    
    # Show the plot
    plt.show()
    
    print(f"Visualization created with {len(polygons)} polygons")
    print("Saved as 'colored_polygons.png'")


visualize_polygons_from_json('polygons.json')