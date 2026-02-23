using System.Collections.Generic;
using Clipper2Lib;

namespace TBCTackleboxMap;

public record AreaMapData(
    float minX,
    float maxX,
    float minZ,
    float maxZ,
    SceneData rootScene,
    List<SceneData> childScenes,
    Dictionary<SceneData, PathsD> worldBounds,
    Dictionary<SceneData, Collectible[]> collectibles,
    Dictionary<SceneData, Capturable[]> capturables
);
