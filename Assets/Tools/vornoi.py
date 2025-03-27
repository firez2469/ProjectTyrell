import numpy as np
from numpy.random import random
from scipy.spatial import Voronoi, distance
from scipy import ndimage
import json
import matplotlib.pyplot as plt
from matplotlib.image import imread
import cv2
import os
from tqdm import tqdm  # Optional: for progress bar

# Parameters
POINT_COUNT = 3000
LAND_IMAGE_PATH = 'maps\TyrellLandMap.png'  # Your land/edge image
WEIGHT_IMAGE_PATH = 'maps\TyrrellMapWeightMap.png'  # Your weight map (if available)
MIN_DISTANCE = 0.0005  # Minimum distance between points (normalized units)
MAX_ATTEMPTS = 10  # Maximum attempts to place a point
ADD_CORNERS = True   # Add corner points to ensure full coverage
EDGE_POINTS_LIMIT = 1000  # Maximum number of edge points to use

# Function to detect edges in the land map image
def detect_edges(image_path):
    if not os.path.exists(image_path):
        print(f"Warning: Land image file '{image_path}' not found.")
        return np.array([])
    
    # Read image
    img = cv2.imread(image_path, cv2.IMREAD_GRAYSCALE)
    if img is None:
        print(f"Warning: Could not read image '{image_path}'.")
        return np.array([])
    
    # Threshold the image to create binary mask (assuming black is land, white is water)
    _, binary = cv2.threshold(img, 127, 255, cv2.THRESH_BINARY)
    
    # Find edges using Canny edge detector
    edges = cv2.Canny(binary, 50, 150)
    
    # Get coordinates of edge pixels
    edge_points = np.column_stack(np.where(edges > 0))
    
    # Convert to normalized coordinates
    normalized_points = edge_points.astype(float)
    normalized_points[:, 0] /= img.shape[0]  # Normalize y coordinates
    normalized_points[:, 1] /= img.shape[1]  # Normalize x coordinates
    
    # Swap columns to match (x, y) convention instead of (row, col)
    normalized_points = normalized_points[:, [1, 0]]
    
    # If we have too many edge points, sample a subset
    if len(normalized_points) > EDGE_POINTS_LIMIT:
        idx = np.random.choice(len(normalized_points), EDGE_POINTS_LIMIT, replace=False)
        normalized_points = normalized_points[idx]
    
    print(f"Detected {len(normalized_points)} edge points")
    return normalized_points

# Load the weight image file for weighted sampling
if os.path.exists(WEIGHT_IMAGE_PATH):
    # Read image and convert to grayscale if it's in color
    weight_img = imread(WEIGHT_IMAGE_PATH)
    if len(weight_img.shape) == 3:  # Color image
        # Convert to grayscale using luminance formula
        img_gray = 0.2989 * weight_img[:,:,0] + 0.5870 * weight_img[:,:,1] + 0.1140 * weight_img[:,:,2]
    else:  # Already grayscale
        img_gray = weight_img

    # Normalize image to [0, 1] range if needed
    if img_gray.max() > 1.0:
        img_gray = img_gray / 255.0
else:
    print(f"Warning: Weight image file '{WEIGHT_IMAGE_PATH}' not found. Using uniform distribution.")
    img_gray = None

# Function to sample a weight from the image
def get_weight(point):
    if img_gray is None:
        return 1.0  # Uniform weight if no image
    
    # Map point coordinates to image pixels
    x = int(point[0] * (img_gray.shape[1] - 1))
    y = int(point[1] * (img_gray.shape[0] - 1))
    
    # Make sure coordinates are within bounds
    x = max(0, min(x, img_gray.shape[1] - 1))
    y = max(0, min(y, img_gray.shape[0] - 1))
    
    # Get pixel intensity (white = high weight, black = low weight)
    return img_gray[y, x]

# Poisson disk sampling with weights
def poisson_disk_sampling_weighted():
    # Start with edge points from the land map
    edge_points = detect_edges(LAND_IMAGE_PATH)
    points = edge_points.tolist() if len(edge_points) > 0 else []
    
    # Convert to numpy arrays for consistency
    points = [np.array(p) for p in points]
    
    # Add corner points if requested
    if ADD_CORNERS:
        # Add points at corners and possibly along edges
        corners = [
            [0.0, 0.0],  # Bottom-left
            [1.0, 0.0],  # Bottom-right
            [0.0, 1.0],  # Top-left
            [1.0, 1.0]   # Top-right
        ]
        
        # Add midpoints of edges for better coverage
        edges = [
            [0.0, 0.5],  # Left edge
            [1.0, 0.5],  # Right edge
            [0.5, 0.0],  # Bottom edge
            [0.5, 1.0]   # Top edge
        ]
        
        # Add corners and edges to our points
        for corner in corners:
            points.append(np.array(corner))
        
        for edge in edges:
            points.append(np.array(edge))
    
    # Calculate remaining points to generate
    remaining_points = max(0, POINT_COUNT - len(points))
    print(f"Already have {len(points)} points from edges and corners. Generating {remaining_points} more...")
    
    if remaining_points > 0:
        # Generate a large pool of candidate points
        candidates = random((remaining_points * 100, 2))
        
        # Get weights for all candidates
        weights = np.array([get_weight(p) for p in candidates])
        
        # Make sure weights sum to 1
        if weights.sum() > 0:
            weights = weights / weights.sum()
        else:
            weights = np.ones_like(weights) / len(weights)
        
        # To track progress
        pbar = tqdm(total=remaining_points, desc="Generating additional points")
        
        # Try to add points until we have enough or run out of candidates
        while len(points) < (POINT_COUNT + (8 if ADD_CORNERS else 0) + len(edge_points)) and len(candidates) > 0:
            # Select a candidate based on weights
            idx = np.random.choice(len(candidates), p=weights)
            candidate = candidates[idx]
            
            # Check if it's far enough from existing points
            if len(points) == 0 or all(distance.euclidean(candidate, p) >= MIN_DISTANCE for p in points):
                # Add the point
                points.append(candidate)
                pbar.update(1)
            
            # Remove the candidate from the pool
            candidates = np.delete(candidates, idx, axis=0)
            weights = np.delete(weights, idx)
            
            # Renormalize weights
            if len(weights) > 0 and weights.sum() > 0:
                weights = weights / weights.sum()
            
            # If we're running low on candidates, generate more
            if len(candidates) < remaining_points * 10 and len(points) < (POINT_COUNT + len(edge_points)):
                new_candidates = random((remaining_points * 20, 2))
                new_weights = np.array([get_weight(p) for p in new_candidates])
                
                if new_weights.sum() > 0:
                    new_weights = new_weights / new_weights.sum()
                else:
                    new_weights = np.ones_like(new_weights) / len(new_weights)
                    
                candidates = np.vstack([candidates, new_candidates])
                weights = np.concatenate([weights, new_weights])
                
                # Renormalize combined weights
                if weights.sum() > 0:
                    weights = weights / weights.sum()
        
        pbar.close()
    
    # Convert list of points to numpy array
    points = np.array(points)
    
    # If we couldn't find enough points with minimum distance
    target_count = (POINT_COUNT + (8 if ADD_CORNERS else 0) + len(edge_points))
    if len(points) < target_count:
        print(f"Warning: Could only place {len(points)} points with minimum distance {MIN_DISTANCE}")
        
        # Fill remaining points using rejection sampling
        remaining = target_count - len(points)
        print(f"Attempting to place {remaining} more points with relaxed constraints...")
        
        remaining_points = []
        for _ in range(remaining):
            best_point = None
            best_dist = 0
            best_weight = 0
            
            # Try MAX_ATTEMPTS times to find a point
            for _ in range(MAX_ATTEMPTS):
                # Generate a random point
                candidate = random(2)
                weight = get_weight(candidate)
                
                # Skip points with zero weight
                if weight <= 0:
                    continue
                
                # Find minimum distance to existing points
                if len(points) > 0:
                    min_dist = min(distance.euclidean(candidate, p) for p in points)
                    if len(remaining_points) > 0:
                        min_dist = min(min_dist, min(distance.euclidean(candidate, p) for p in remaining_points))
                else:
                    min_dist = float('inf')
                
                # Keep track of the best point found
                if min_dist > best_dist or (min_dist == best_dist and weight > best_weight):
                    best_dist = min_dist
                    best_point = candidate
                    best_weight = weight
            
            # Add the best point found
            if best_point is not None:
                remaining_points.append(best_point)
        
        # Combine all points
        if remaining_points:
            points = np.vstack([points, np.array(remaining_points)])
    
    return points, len(edge_points)

# Generate points with minimum distance
print(f"Generating combined set of edge points and {POINT_COUNT} additional points...")
points, num_edge_points = poisson_disk_sampling_weighted()

# Add additional boundary points to create a bounding box (useful for visualization)
# These are outside the normal [0,1] range to ensure proper cell closure
boundary_points = []
if ADD_CORNERS:
    buffer = 0.5  # Distance outside the normal range
    for x in np.linspace(-buffer, 1+buffer, 6):
        for y in [-buffer, 1+buffer]:
            boundary_points.append([x, y])
    
    for y in np.linspace(-buffer, 1+buffer, 6):
        for x in [-buffer, 1+buffer]:
            if [x, y] not in boundary_points:  # Avoid duplicates at extreme corners
                boundary_points.append([x, y])
    
    # Add these points
    boundary_points = np.array(boundary_points)
    points = np.vstack([points, boundary_points])

# Compute Voronoi diagram
vor = Voronoi(points)

# Create dictionary with vertices and edges
voronoi_data = {
    "vertices": vor.vertices.tolist(),  # Convert numpy array to list for JSON serialization
    "edges": [],
    "points": points.tolist(),  # Also save the original points
    "metadata": {
        "edge_points_count": num_edge_points,
        "random_points_count": len(points) - num_edge_points - len(boundary_points),
        "boundary_points_count": len(boundary_points)
    }
}

# Extract edges from Voronoi diagram
for ridge_vertices in vor.ridge_vertices:
    # Skip if any vertex is -1 (means the ridge extends to infinity)
    if -1 not in ridge_vertices:
        # Store edge as tuple of vertex indices
        voronoi_data["edges"].append(ridge_vertices)

# Save to JSON file
with open('voronoi_data.json', 'w') as f:
    json.dump(voronoi_data, f)

# Create visualization to verify our combined points
plt.figure(figsize=(12, 8))

# Plot the land image if available
plt.subplot(2, 2, 1)
if os.path.exists(LAND_IMAGE_PATH):
    land_img = imread(LAND_IMAGE_PATH)
    plt.imshow(land_img)
    plt.title('Land Map Image')
else:
    plt.text(0.5, 0.5, "No land image available", ha='center', va='center')
    plt.title('No Land Image')
plt.axis('off')

# Plot the weight image if available
plt.subplot(2, 2, 2)
if img_gray is not None:
    plt.imshow(img_gray, cmap='gray')
    plt.title('Weight Map')
else:
    plt.text(0.5, 0.5, "No weight image available", ha='center', va='center')
    plt.title('No Weight Map')
plt.axis('off')

# Plot the points on the land image
plt.subplot(2, 2, 3)
if os.path.exists(LAND_IMAGE_PATH):
    land_img = imread(LAND_IMAGE_PATH)
    plt.imshow(land_img, alpha=0.7)

    # Edge points (first in the array)
    if num_edge_points > 0:
        edge_pts = points[:num_edge_points]
        plt.scatter(edge_pts[:, 0] * (land_img.shape[1] - 1), 
                    edge_pts[:, 1] * (land_img.shape[0] - 1),
                    c='red', s=5, alpha=0.7, label='Edge Points')
    
    # Corner and regular points
    if ADD_CORNERS and len(points) > num_edge_points + 8:
        # Corner points
        corner_pts = points[num_edge_points:num_edge_points+4]
        plt.scatter(corner_pts[:, 0] * (land_img.shape[1] - 1),
                    corner_pts[:, 1] * (land_img.shape[0] - 1),
                    c='blue', s=30, alpha=0.9, label='Corner Points')
        
        # Edge midpoints
        edge_mid_pts = points[num_edge_points+4:num_edge_points+8]
        plt.scatter(edge_mid_pts[:, 0] * (land_img.shape[1] - 1),
                    edge_mid_pts[:, 1] * (land_img.shape[0] - 1),
                    c='green', s=20, alpha=0.9, label='Edge Midpoints')
        
        # Regular points
        regular_pts = points[num_edge_points+8:-len(boundary_points)] if len(boundary_points) > 0 else points[num_edge_points+8:]
        plt.scatter(regular_pts[:, 0] * (land_img.shape[1] - 1),
                    regular_pts[:, 1] * (land_img.shape[0] - 1),
                    c='yellow', s=3, alpha=0.5, label='Regular Points')
    
    # Boundary points
    if len(boundary_points) > 0:
        plt.scatter(boundary_points[:, 0] * (land_img.shape[1] - 1),
                    boundary_points[:, 1] * (land_img.shape[0] - 1),
                    c='purple', s=15, alpha=0.5, label='Bounding Points', marker='x')
    
    plt.title('Points on Land Map')
    plt.legend(loc='upper right', fontsize='small')
else:
    # If no land image, just show the points
    plt.scatter(points[:, 0], points[:, 1], c='red', s=5, alpha=0.5)
    plt.title('Generated Points')

plt.axis('off')

# Plot the Voronoi diagram
plt.subplot(2, 2, 4)
if os.path.exists(LAND_IMAGE_PATH):
    land_img = imread(LAND_IMAGE_PATH)
    plt.imshow(land_img, alpha=0.5)

# Plot Voronoi edges
for simplex in vor.ridge_vertices:
    if -1 not in simplex:
        plt.plot(vor.vertices[simplex, 0] * (land_img.shape[1] - 1 if os.path.exists(LAND_IMAGE_PATH) else 1), 
                 vor.vertices[simplex, 1] * (land_img.shape[0] - 1 if os.path.exists(LAND_IMAGE_PATH) else 1), 
                 'k-', linewidth=0.5)

plt.title('Voronoi Diagram')
plt.axis('off')

plt.tight_layout()
plt.savefig('edge_based_voronoi_visualization.png', dpi=300)
plt.close()

print(f"Saved data with {len(voronoi_data['vertices'])} vertices and {len(voronoi_data['edges'])} edges")
print(f"Total points generated: {len(points)}")
print(f"Breakdown: {num_edge_points} edge points, {voronoi_data['metadata']['random_points_count']} random points, {len(boundary_points)} boundary points")
print(f"Visualization saved as 'edge_based_voronoi_visualization.png'")