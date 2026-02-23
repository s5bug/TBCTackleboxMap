using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using Eflatun.SceneReference;
using ImGuiNET;
using UnityEngine;

namespace TBCTackleboxMap;

public sealed class Map : ManagedBehaviour
{
    public UImGui.UImGui _imgui { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<AreaDefinition, AreaMapData> MapCache { get; } = new();

    public override void ManagedOnEnable()
    {
        this._imgui.Layout += Layout;
    }

    public override void ManagedOnDisable()
    {
        this._imgui.Layout -= Layout;
    }

    private static readonly uint[] Colors =
    [
        ImGui.GetColorU32(new Vector4(191.0f / 255.0f, 97.0f / 255.0f, 106.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(208.0f / 255.0f, 135.0f / 255.0f, 112.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(235.0f / 255.0f, 203.0f / 255.0f, 139.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(163.0f / 255.0f, 190.0f / 255.0f, 140.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(143.0f / 255.0f, 188.0f / 255.0f, 187.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(129.0f / 255.0f, 161.0f / 255.0f, 193.0f / 255.0f, 1.0f)),
        ImGui.GetColorU32(new Vector4(180.0f / 255.0f, 142.0f / 255.0f, 173.0f / 255.0f, 1.0f))
    ];
    
    // Arrow points where 1 in the -Y direction is forward
    private static readonly Vector2[] Arrow =
    [
        new Vector2(0.0f, -8.0f),
        new Vector2(-4.0f, 8.0f),
        new Vector2(0.0f, 2.0f),
        new Vector2(4.0f, 8.0f)
    ];

    public AreaMapData CreateAreaMapData(AreaDefinition area)
    {
        Dictionary<SceneData, SceneParent> sceneParents = SceneParent._sceneParents;
        // The "root scene" of an area is the SceneParent with no _parentData
        SceneData rootScene = null;
        List<SceneData> childScenes = new List<SceneData>();

        // TODO cache all this data per-AreaDefinition
        float minX = Single.PositiveInfinity;
        float maxX = Single.NegativeInfinity;
        float minZ = Single.PositiveInfinity;
        float maxZ = Single.NegativeInfinity;
        Dictionary<SceneData, PathsD> worldBounds = new();
        Dictionary<SceneData, Collectible[]> collectibles = new();
        Dictionary<SceneData, Capturable[]> capturables = new();

        foreach (var (sceneData, sceneParent) in sceneParents)
        {
            if (sceneData._area == area)
            {
                if (sceneData._parentData == null)
                {
                    rootScene = sceneData;
                }
                else
                {
                    childScenes.Add(sceneData);

                    List<Collider> colliders = sceneParent._entranceVolumes.SelectMany(v => v._colliders).ToList();

                    PathsD rawBounds = new PathsD();
                    foreach (Collider collider in colliders)
                    {

                        float thisMinX = collider.bounds.min.x;
                        float thisMinZ = collider.bounds.min.z;
                        float thisMaxX = collider.bounds.max.x;
                        float thisMaxZ = collider.bounds.max.z;
                        
                        rawBounds.Add(new PathD([
                            new PointD(thisMinX, thisMinZ),
                            new PointD(thisMaxX, thisMinZ),
                            new PointD(thisMaxX, thisMaxZ),
                            new PointD(thisMinX, thisMaxZ)
                        ]));

                        minX = Math.Min(minX, thisMinX);
                        maxX = Math.Max(maxX, thisMaxX);
                        minZ = Math.Min(minZ, thisMinZ);
                        maxZ = Math.Max(maxZ, thisMaxZ);
                    }

                    PathsD unionedBounds = Clipper.Union(rawBounds, FillRule.NonZero);

                    worldBounds[sceneData] = unionedBounds;
                    collectibles[sceneData] = sceneParent._activeParent.GetComponentsInChildren<Collectible>(true);
                    capturables[sceneData] = sceneParent._activeParent.GetComponentsInChildren<Capturable>(true);
                }
            }
        }

        return new AreaMapData(minX, maxX, minZ, maxZ, rootScene, childScenes, worldBounds, collectibles, capturables);
    }
    
    public AreaMapData GetOrCreateAreaMapData(AreaDefinition area)
    {
        if (MapCache.TryGetValue(area, out var areaMapData)) return areaMapData;

        AreaMapData next = CreateAreaMapData(area);
        MapCache.Add(area, next);
        return next;
    }
    
    public bool CheckForEnable()
    {
        MainMenu mainMenu = Manager._instance._mainMenu;
        
        // Only show during gameplay
        if (mainMenu._currentState != mainMenu._gameplayState && mainMenu._currentState != mainMenu._inGameState)
            return false;

        PlayerInput playerInput = Manager.GetGameInput().PrimaryPlayerInput;

        if (Manager._instance._timeManagement._gamePaused)
        {
            // If we're paused
            // Check for LB press
            if (playerInput._cameraInputs._lockOn._wasPressed)
            {
                IsEnabled = !IsEnabled;
            }

            return IsEnabled;
        }
        else
        {
            // If we're not paused
            // Check for Tether and Interact held
            return playerInput._movementInputs._tether._currentlyHeld && playerInput._movementInputs._interact._currentlyHeld;
        }
    }

    public void Layout(UImGui.UImGui ui)
    {
        if (!CheckForEnable()) return;

        SaveData currentSaveData = Manager._instance._saveManager._currentSaveData;
        if (!currentSaveData.TryGetLastVisitedArea(out AreaDefinition here)) return;
        
        if (ImGui.Begin("Map"))
        {
            AreaMapData amd = GetOrCreateAreaMapData(here);

            string rootSceneName = amd.rootScene.name;
            
            float xWidth = amd.maxX - amd.minX;
            float zHeight = amd.maxZ - amd.minZ;

            Vector2 imguiCursorScreenPos = ImGui.GetCursorScreenPos();
            Vector2 imguiRemainingSpace = ImGui.GetContentRegionAvail();
            Vector2 imguiBottomRight = imguiCursorScreenPos + imguiRemainingSpace;

            const float mapPadding = 4.0f;
            Vector2 mapTopLeft = imguiCursorScreenPos + new Vector2(mapPadding, mapPadding);
            Vector2 mapBottomRight = imguiBottomRight - new Vector2(mapPadding, mapPadding);
            Vector2 mapSpace = mapBottomRight - mapTopLeft;

            float xToFit = mapSpace.x / xWidth;
            float zToFit = mapSpace.y / zHeight;
            float trueScale = Math.Min(xToFit, zToFit);
            
            // to map a world space (x, z) to a screen space (x, y)
            // 1. make all coordinates relative to minX, minZ, i.e. subtract minX from X and minZ from Z
            // 2. scale by trueScale
            // 3. add imguiCursorScreenPos
            
            ImDrawListPtr imDrawList = ImGui.GetWindowDrawList();
            imDrawList.PushClipRect(imguiCursorScreenPos, imguiBottomRight);
            for (var i = 0; i < amd.childScenes.Count; i++)
            {
                SceneData childScene = amd.childScenes[i];

                PathsD screenPaths = new PathsD();
                foreach (PathD path in amd.worldBounds[childScene])
                {
                    PathD screenPath = new PathD();
                    foreach (PointD point in path)
                    {
                        float pRelativeWorldX = (float)point.x - amd.minX;
                        float pRelativeWorldZ = (float)point.y - amd.minZ;

                        float pRelativeScreenX = trueScale * pRelativeWorldX;
                        float pRelativeScreenY = trueScale * pRelativeWorldZ;

                        float pScreenX = mapTopLeft.x + pRelativeScreenX;
                        float pScreenY = mapBottomRight.y - pRelativeScreenY;
                        
                        screenPath.Add(new PointD(pScreenX, pScreenY));
                        imDrawList.PathLineTo(new Vector2((float)pScreenX, (float)pScreenY));
                    }
                    
                    screenPaths.Add(screenPath);
                    imDrawList.PathStroke(Colors[i % Colors.Length], ImDrawFlags.Closed);
                }

                string mapName = childScene.name;
                if (mapName.StartsWith(rootSceneName + "_"))
                {
                    mapName = mapName[(rootSceneName.Length + 1)..];
                }

                int numCoinsCollected = amd.collectibles[childScene].Count(collectible => collectible._collected);
                int numCoinsTotal = amd.collectibles[childScene].Length;
                float numCoinsFraction = (float)numCoinsCollected / (float)numCoinsTotal;
                int numCoinsPercent = (int)(numCoinsFraction * 100);
                int numCapturablesCollected = amd.capturables[childScene].Count(capturable => capturable._state == Capturable.State.Captured);
                int numCapturablesTotal = amd.capturables[childScene].Length;
                float numCapturablesFraction = (float)numCapturablesCollected / (float)numCapturablesTotal;
                int numCapturablesPercent = (int)(numCapturablesFraction * 100);

                string areaText = $"""
                                   {mapName}
                                   Coins: {numCoinsCollected,3}/{numCoinsTotal,3} ({numCoinsPercent,3}%)
                                   Fish:  {numCapturablesCollected,3}/{numCapturablesTotal,3} ({numCapturablesPercent,3}%)
                                   """;
                Vector2 areaTextSize = ImGui.CalcTextSize(areaText);
                
                RectD screenBounds = Clipper.GetBounds(screenPaths);
                Vector2 centerPoint = new Vector2(
                    (float)(screenBounds.left + screenBounds.right) / 2.0f,
                    (float)(screenBounds.top + screenBounds.bottom) / 2.0f
                );

                Vector2 textPos = centerPoint - (areaTextSize * 0.5f);
                imDrawList.AddText(textPos, Colors[i % Colors.Length], areaText);
            }

            float playerRelativeWorldX = Manager._instance._primaryPlayerMachine._position.x - amd.minX;
            float playerRelativeWorldZ = Manager._instance._primaryPlayerMachine._position.z - amd.minZ;
            float playerRelativeScreenX = trueScale * playerRelativeWorldX;
            float playerRelativeScreenY = trueScale * playerRelativeWorldZ;
            float playerScreenX = mapTopLeft.x + playerRelativeScreenX;
            float playerScreenY = mapBottomRight.y - playerRelativeScreenY;

            float playerAngle = Manager._instance._primaryPlayerMachine._artRotator.rotation.eulerAngles.y;
            float playerSin = Mathf.Sin(playerAngle * Mathf.Deg2Rad);
            float playerCos = Mathf.Cos(playerAngle * Mathf.Deg2Rad);
            foreach (Vector2 arrowPoint in Arrow)
            {
                float rx = (arrowPoint.x * playerCos) - (arrowPoint.y * playerSin);
                float ry = (arrowPoint.x * playerSin) + (arrowPoint.y * playerCos);
                
                Vector2 actualPoint = new Vector2(playerScreenX + rx, playerScreenY + ry);
                imDrawList.PathLineTo(actualPoint);
            }
            imDrawList.PathStroke(ImGui.GetColorU32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f)), ImDrawFlags.Closed, 2.0f);
            
            imDrawList.PopClipRect();
        }
        ImGui.End();
    }
}
