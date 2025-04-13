import json
import numpy as np
import matplotlib.pyplot as plt

# Load the Voronoi data from JSON
with open('voronoi_data.json', 'r') as f:
    voronoi_data = json.load(f)

vertices = np.array(voronoi_data["vertices"])
edges = voronoi_data["edges"]

# Create a figure and axis
fig, ax = plt.subplots(figsize=(10, 10))

# Plot the vertices
ax.plot(vertices[:, 0], vertices[:, 1], 'ko', markersize=3)

# Plot the edges
for edge in edges:
    # Get the coordinates of the vertices that make up this edge
    v1 = vertices[edge[0]]
    v2 = vertices[edge[1]]
    
    # Plot the edge as a line
    ax.plot([v1[0], v2[0]], [v1[1], v2[1]], 'b-', linewidth=1)

# Set axis limits
ax.set_xlim([-0.1, 1.1])
ax.set_ylim([-0.1, 1.1])

# Add a title
ax.set_title('Voronoi Diagram Reconstructed from JSON')

# Show the plot
plt.tight_layout()
plt.show()