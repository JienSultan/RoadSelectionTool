using System.Collections.Generic;
using System.Reflection;
using Breakdown.Tools.Patching;
using HarmonyLib;
using SDG.Framework.Rendering;
using SDG.Framework.Utilities;
using UnityEngine;
using SDG.Unturned;

namespace RoadSelectionTool.Module.Editor.Tools
{
    /// <summary>
    /// A custom tool for selecting and manipulating road nodes in the Unturned editor.
    /// </summary>
    public class RoadSelectionTool : MonoBehaviour
    {
        // Reflection fields to access private Unturned fields and methods
        private static readonly FieldInfo? _roads =
            typeof(LevelRoads).GetField("roads", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly FieldInfo? _selection =
            typeof(EditorRoads).GetField("selection", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo? _select =
            typeof(EditorRoads).GetMethod("select", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo? _deselect =
            typeof(EditorRoads).GetMethod("deselect", BindingFlags.NonPublic | BindingFlags.Static);

        // State for drag selection rectangle
        private Vector2 _startScreenPos;
        private bool _isSelecting;
        private bool _isAddingToSelection;
        private Rect _selectionRect;

        // Currently selected joints and their associated paths
        public static List<RoadJoint> SelectedJoints = new();
        public static List<RoadPath> SelectedPaths = new();

        private Camera? _sceneCamera;

        // Information about the primary joint (the pivot for moving multiple joints)
        private static RoadJoint? _primaryJoint;
        private Vector3 _primaryLastPosition;

        // Stores relative offsets for multi-joint movement
        private static readonly Dictionary<RoadJoint, Vector3> _otherOffsets = new();

        // Harmony
        private static bool _patched = false;
        public const string HarmonyKey = "RoadSelectionTool.Module.Editor.Tools.RoadSelectionTool";
        
        private void Start()
        {
            UnturnedLog.info("Initialized the Road Selection Tool.");
            _sceneCamera = Camera.main;
            
            if (!_patched)
            {
                PatchingTool.AddHarmony(HarmonyKey);
                PatchingTool.Patch(HarmonyKey, typeof(Road_AddVertex_Patch));
                _patched = true;
            }
        }

        /// <summary>
        /// Clears the current selection, unhighlighting all selected nodes and resetting state.
        /// </summary>
        private void clear()
        {
            foreach (var road in GetAllRoads())
                foreach (var path in road.paths)
                   path.unhighlightVertex(); 

            SelectedJoints.Clear();
            SelectedPaths.Clear();
            _primaryJoint = null;
            _otherOffsets.Clear();

            _deselect?.Invoke(null, null);
        }

        private void Update()
        {
            // If the editor is not in paving mode, clear selection and do an early return
            if (!EditorRoads.isPaving)
            {
                if (SelectedJoints.Count > 0)
                    clear();
                return;
            }

            // While dragging, update the selection rectangle and highlight nodes under it
            if (_isSelecting)
            {
                _selectionRect = GetScreenRect(_startScreenPos, Input.mousePosition);
                SelectObjectsInRect();
            }

            // Ensure all selected paths are highlighted
            foreach (var path in SelectedPaths)
                path.highlightVertex();

            // Start a new selection rectangle with middle mouse button
            if (Input.GetMouseButtonDown(2) && !_isSelecting)
            {
                _startScreenPos = Input.mousePosition;
                _isAddingToSelection = Input.GetKey(KeyCode.LeftShift);
                _isSelecting = true;
            }

            // End selection rectangle on mouse release
            if (Input.GetMouseButtonUp(2))
            {
                _isSelecting = false;
                _isAddingToSelection = false;
            }

            // Handle deletion of selected joints when pressing Delete
            if (SelectedJoints.Count > 0 && Input.GetKeyDown(KeyCode.Delete))
            {
                // Group joints by road so we can delete them in batches
                var jointsByRoad = new Dictionary<Road, List<RoadJoint>>();
                foreach (var joint in SelectedJoints)
                {
                    if (!jointsByRoad.ContainsKey(joint.road))
                        jointsByRoad[joint.road] = new List<RoadJoint>();
                    jointsByRoad[joint.road].Add(joint);
                }

                // Remove vertices in reverse index order to avoid shifting indices, because when removing indexes, the rest shifts down.
                foreach (var kvp in jointsByRoad)
                {
                    var joints = kvp.Value;
                    joints.Sort((a, b) => b.index.CompareTo(a.index));

                    foreach (var joint in joints)
                        if (joint.index >= 0 && joint.index < joint.road.joints.Count)
                            joint.road.removeVertex(joint.index);
                }

                clear();
            }

            // Handle dragging/moving multiple selected joints
            if (_primaryJoint.HasValue)
            {
                var joint = _primaryJoint.Value;
                Vector3 current = joint.road.joints[joint.index].vertex;

                if (current != _primaryLastPosition)
                {
                    foreach (var kvp in _otherOffsets)
                    {
                        var targetJoint = kvp.Key;
                        Vector3 offset = kvp.Value;
                        Vector3 newPosition = current + offset;
                        targetJoint.road.moveVertex(targetJoint.index, newPosition);
                    }

                    _primaryLastPosition = current;
                }
            }
        }

        // This is kind of taken from the vanilla SelectionTool
        private void OnRenderObject()
        {
            if (!_isSelecting || _sceneCamera == null)
                return;

            GLUtility.LINE_FLAT_COLOR.SetPass(0);
            GLUtility.matrix = MathUtility.IDENTITY_MATRIX;

            GL.Begin(GL.LINES);
            GL.Color(Color.yellow);

            Vector3 startViewport = _sceneCamera.ScreenToViewportPoint(_startScreenPos);
            Vector3 endViewport = _sceneCamera.ScreenToViewportPoint(Input.mousePosition);

            startViewport.z = 16f;
            endViewport.z = 16f;

            Vector3 min = new Vector3(Mathf.Min(startViewport.x, endViewport.x), Mathf.Min(startViewport.y, endViewport.y), 16f);
            Vector3 max = new Vector3(Mathf.Max(startViewport.x, endViewport.x), Mathf.Max(startViewport.y, endViewport.y), 16f);

            Vector3 v0 = _sceneCamera.ViewportToWorldPoint(new Vector3(min.x, min.y, min.z));
            Vector3 v1 = _sceneCamera.ViewportToWorldPoint(new Vector3(max.x, min.y, min.z));
            Vector3 v2 = _sceneCamera.ViewportToWorldPoint(new Vector3(max.x, max.y, max.z));
            Vector3 v3 = _sceneCamera.ViewportToWorldPoint(new Vector3(min.x, max.y, max.z));

            GL.Vertex(v0); GL.Vertex(v1);
            GL.Vertex(v1); GL.Vertex(v2);
            GL.Vertex(v2); GL.Vertex(v3);
            GL.Vertex(v3); GL.Vertex(v0);

            GL.End();
        }

        /// <summary>
        /// Selects road joints that fall within the current selection rectangle.
        /// </summary>
        private void SelectObjectsInRect()
        {
            // If not holding Shift, clear previous selection.
            // I am too lazy to implement real-time clearing for shift-select.
            if (!_isAddingToSelection)
                clear();

            Transform? currentSelection = (Transform?)_selection?.GetValue(null);
            RoadJoint? selectedJoint = null;

            var newJoints = new List<RoadJoint>();
            var newPaths = new List<RoadPath>();

            foreach (var road in GetAllRoads())
            {
                for (int index = 0; index < road.joints.Count; index++)
                {
                    var joint = road.joints[index];
                    Vector3 screenPoint = _sceneCamera!.WorldToScreenPoint(joint.vertex);

                    if (screenPoint.z <= 0f)
                        continue;

                    Vector2 screen2D = new(screenPoint.x, Screen.height - screenPoint.y);

                    if (_selectionRect.Contains(screen2D))
                    {
                        var roadJoint = new RoadJoint
                        {
                            road = road,
                            index = index,
                            vertex = joint.vertex
                        };

                        // Skip if already selected
                        if (SelectedJoints.Exists(j => j.road == roadJoint.road && j.index == roadJoint.index))
                            continue;

                        newJoints.Add(roadJoint);
                        newPaths.Add(road.paths[index]);

                        if (currentSelection != null && road.paths[index].vertex == currentSelection)
                            selectedJoint = roadJoint;
                    }
                }
            }

            // Add any newly selected nodes
            SelectedJoints.AddRange(newJoints);
            SelectedPaths.AddRange(newPaths);

            if (SelectedJoints.Count > 0)
            {
                // Determine which joint is primary for movement
                if (_primaryJoint.HasValue && _isAddingToSelection)
                    selectedJoint = _primaryJoint;
                
                else if (selectedJoint.HasValue)
                    _primaryJoint = selectedJoint.Value;
                
                else
                {
                    _primaryJoint = SelectedJoints[0];
                    var vertex = _primaryJoint.Value.road.paths[_primaryJoint.Value.index].vertex;
                    _deselect?.Invoke(null, null);
                    _select?.Invoke(null, new object[] { vertex });
                }

                _primaryLastPosition = _primaryJoint.Value.vertex;

                // Compute offsets for moving other joints relative to the primary
                _otherOffsets.Clear();
                foreach (var joint in SelectedJoints)
                    if (joint.road != _primaryJoint.Value.road || joint.index != _primaryJoint.Value.index)
                        _otherOffsets[joint] = joint.vertex - _primaryLastPosition;
            }
            else
                // If nothing is selected now, clear any active selection in EditorRoads
                if (_primaryJoint.HasValue)
                    _deselect?.Invoke(null, null);
        }

        /// <summary>
        /// Retrieves the list of all roads from LevelRoads using reflection.
        /// </summary>
        private static List<Road> GetAllRoads() { return (List<Road>?)_roads?.GetValue(null) ?? new List<Road>(); }

        /// <summary>
        /// Creates a Rect from two screen points, adjusting for Unity's flipped Y coordinates in GUI.
        /// </summary>
        private Rect GetScreenRect(Vector2 start, Vector2 end)
        {
            start.y = Screen.height - start.y;
            end.y = Screen.height - end.y;

            return new Rect(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y),
                Mathf.Abs(start.x - end.x),
                Mathf.Abs(start.y - end.y)
            );
        }

        /// <summary>
        /// Represents a road joint (vertex) with its owning road and index.
        /// </summary>
        public record struct RoadJoint
        {
            public Road road;
            public int index;
            public Vector3 vertex;
        }
        
        [HarmonyPatch(typeof(Road), nameof(Road.addVertex))]
        private static class Road_AddVertex_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                foreach (var road in GetAllRoads())
                    foreach (var path in road.paths)
                        path.unhighlightVertex(); 

                SelectedJoints.Clear();
                SelectedPaths.Clear();
                _primaryJoint = null;
                _otherOffsets.Clear();

                _deselect?.Invoke(null, null);

                return true;
            }
        }
    }
}
