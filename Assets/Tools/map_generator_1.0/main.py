import vornoi
import tiles
import visualize_tiles
import map_editor

map_editor.main()

"""
import json

data = json.load(open('polygons.json','r'))

d = data['vertices']

print(data.keys())

import numpy as np

d = np.array(d)
print(d.shape)

import pandas as pd
df= pd.DataFrame(d)
print(df.head())"""