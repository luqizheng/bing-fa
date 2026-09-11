using System.Collections.Generic;
using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>挂在单位占位体上的代理，用于鼠标拾取时反查仿真单位。</summary>
    public sealed class UnitProxy : MonoBehaviour
    {
        public string UnitId = string.Empty;
    }

    /// <summary>
    /// 战场的 3D 占位表现层（无美术资源，全部程序化生成）。
    /// 只读仿真状态并画出来——不参与任何规则结算（规则全在 <see cref="BattleSimulation"/>）。
    /// 坐标映射：仿真 MapPoint(X, Z) → Unity (X, 0, Z)，1 米 = 1 Unity 单位。
    /// </summary>
    public sealed class BattleView
    {
        private readonly BattleSimulation sim;
        private readonly Transform root;
        private readonly Dictionary<string, Transform> unitViews = new();
        private readonly Dictionary<string, Transform> healthBars = new();
        private readonly Dictionary<string, Transform> moraleBars = new();
        private readonly Dictionary<string, Transform> formationRings = new();
        private readonly Dictionary<string, Transform> propViews = new();
        private readonly List<Transform> fireZoneViews = new();

        private Transform selectionRing = null!;

        public BattleView(BattleSimulation sim, Transform root)
        {
            this.sim = sim;
            this.root = root;
            BuildGround();
            BuildProps();
            BuildUnits();
            BuildSelectionRing();
        }

        public Camera Camera { get; private set; } = null!;

        public void SetCamera(Camera camera) => Camera = camera;

        // ───────────────────────────── 静态场景 ─────────────────────────────

        private void BuildGround()
        {
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", root);
            // Plane 原生 10×10 → 按地图定义尺寸铺满（MAP-09：长平 340×420）
            ground.localScale = new Vector3(sim.Map.Width / 10f, 1f, sim.Map.Depth / 10f);
            ground.position = new Vector3((sim.Map.MinX + sim.Map.MaxX) * 0.5f, 0f, (sim.Map.MinZ + sim.Map.MaxZ) * 0.5f);
            SetColor(ground.gameObject, SlicePalette.Ground);

            foreach (var patch in sim.Map.Volumes)
            {
                var block = CreatePrimitive(PrimitiveType.Cube, "Terrain_" + patch.Center.X + "_" + patch.Center.Z, root);
                block.localScale = new Vector3(patch.Width, 0.2f, patch.Depth);
                block.position = new Vector3(patch.Center.X, 0.1f, patch.Center.Z);
                SetColor(block.gameObject, patch.Terrain == TerrainClass.Impassable
                    ? SlicePalette.ImpassableTerrain
                    : SlicePalette.DifficultTerrain);
            }
        }

        private void BuildProps()
        {
            foreach (var prop in sim.Map.Props)
            {
                var view = prop.Kind switch
                {
                    PropKind.Granary => CreatePrimitive(PrimitiveType.Cylinder, "Prop_" + prop.Id, root),
                    PropKind.City => CreatePrimitive(PrimitiveType.Cube, "Prop_" + prop.Id, root),
                    _ => CreatePrimitive(PrimitiveType.Cylinder, "Prop_" + prop.Id, root),
                };

                view.position = new Vector3(prop.Position.X, prop.Kind == PropKind.City ? 5f : 2.5f, prop.Position.Z);
                view.localScale = prop.Kind == PropKind.City
                    ? new Vector3(prop.Radius, 5f, prop.Radius)
                    : new Vector3(prop.Radius * 0.7f, 2.5f, prop.Radius * 0.7f);

                SetColor(view.gameObject, prop.Owner == Faction.Zhao ? SlicePalette.ZhaoProp : SlicePalette.QinProp);
                propViews[prop.Id] = view;
            }
        }

        private void BuildUnits()
        {
            foreach (var unit in sim.Units)
            {
                var body = CreatePrimitive(PrimitiveType.Capsule, "Unit_" + unit.Id, root);
                body.localScale = new Vector3(2.4f, 2.4f, 2.4f);
                body.position = ToWorld(unit.Position);
                SetColor(body.gameObject, unit.Faction == Faction.Zhao ? SlicePalette.ZhaoUnit : SlicePalette.QinUnit);
                body.gameObject.AddComponent<UnitProxy>().UnitId = unit.Id;
                unitViews[unit.Id] = body;

                // 血条 / 士气条（世界空间薄片，随单位移动）
                healthBars[unit.Id] = CreateBar("Hp_" + unit.Id, SlicePalette.Health, 0.45f, 2.6f);
                moraleBars[unit.Id] = CreateBar("Morale_" + unit.Id, SlicePalette.Morale, 0.32f, 2.15f);

                // 结阵指示环
                var ring = CreatePrimitive(PrimitiveType.Cylinder, "Form_" + unit.Id, root);
                ring.localScale = new Vector3(3.4f, 0.02f, 3.4f);
                ring.position = ToWorld(unit.Position) + new Vector3(0f, 0.06f, 0f);
                SetColor(ring.gameObject, SlicePalette.Formation);
                formationRings[unit.Id] = ring;
            }
        }

        private Transform CreateBar(string name, Color color, float height, float offset)
        {
            var bar = CreatePrimitive(PrimitiveType.Cube, name, root);
            bar.localScale = new Vector3(3f, height, 0.4f);
            bar.position = new Vector3(0f, height, 0f);
            SetColor(bar.gameObject, color);
            return bar;
        }

        private void BuildSelectionRing()
        {
            selectionRing = CreatePrimitive(PrimitiveType.Cylinder, "SelectionRing", root);
            selectionRing.localScale = new Vector3(4.2f, 0.03f, 4.2f);
            SetColor(selectionRing.gameObject, SlicePalette.Selection);
            selectionRing.gameObject.SetActive(false);
        }

        // ───────────────────────────── 每帧刷新 ─────────────────────────────

        public void Sync(float nowSeconds, IReadOnlyCollection<string> selectedIds)
        {
            foreach (var unit in sim.Units)
            {
                if (!unitViews.TryGetValue(unit.Id, out var view))
                {
                    continue;
                }

                bool visible = unit.Alive;
                view.gameObject.SetActive(visible);
                healthBars[unit.Id].gameObject.SetActive(visible);
                moraleBars[unit.Id].gameObject.SetActive(visible);
                formationRings[unit.Id].gameObject.SetActive(visible && unit.Formed);
                if (!visible)
                {
                    continue;
                }

                var world = ToWorld(unit.Position);
                view.position = world;
                view.rotation = Quaternion.Euler(0f, unit.Faction == Faction.Zhao ? 180f : 0f, 0f);

                // 血条：左对齐缩放，颜色随血量由绿转红
                var hp = healthBars[unit.Id];
                hp.position = world + new Vector3(0f, 4.2f, 0f);
                hp.localScale = new Vector3(3f * unit.HealthRatio, 0.45f, 0.4f);
                hp.position -= new Vector3(3f * (1f - unit.HealthRatio) * 0.5f, 0f, 0f);
                SetColor(hp.gameObject, Color.Lerp(SlicePalette.HealthLow, SlicePalette.Health, unit.HealthRatio));

                // 士气条：溃逃时变红
                var morale = moraleBars[unit.Id];
                morale.position = world + new Vector3(0f, 3.5f, 0f);
                morale.localScale = new Vector3(3f * (unit.Morale.Value / 100f), 0.32f, 0.4f);
                morale.position -= new Vector3(3f * (1f - unit.Morale.Value / 100f) * 0.5f, 0f, 0f);
                SetColor(morale.gameObject, unit.IsRouted ? SlicePalette.MoraleRouted : SlicePalette.Morale);

                formationRings[unit.Id].position = world + new Vector3(0f, 0.06f, 0f);

                if (unit.IsScout)
                {
                    view.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                }
            }

            // 火区可视化
            SyncFireZones();

            // 粮仓焚毁状态
            foreach (var prop in sim.Map.Props)
            {
                if (propViews.TryGetValue(prop.Id, out var propView) && prop.IsBurned)
                {
                    SetColor(propView.gameObject, SlicePalette.Burned);
                }
            }

            // 选中环
            var firstSelected = default(SimUnit?);
            foreach (var unit in sim.Units)
            {
                if (unit.Alive && selectedIds.Contains(unit.Id))
                {
                    firstSelected = unit;
                    break;
                }
            }

            if (firstSelected is null)
            {
                selectionRing.gameObject.SetActive(false);
            }
            else
            {
                selectionRing.gameObject.SetActive(true);
                selectionRing.position = ToWorld(firstSelected.Position) + new Vector3(0f, 0.08f, 0f);
            }
        }

        private void SyncFireZones()
        {
            while (fireZoneViews.Count < sim.FireZones.Count)
            {
                var quad = CreatePrimitive(PrimitiveType.Cube, "FireZone_" + fireZoneViews.Count, root);
                SetColor(quad.gameObject, SlicePalette.Fire, transparent: true);
                fireZoneViews.Add(quad);
            }

            for (int i = 0; i < fireZoneViews.Count; i++)
            {
                bool active = i < sim.FireZones.Count;
                fireZoneViews[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var zone = sim.FireZones[i];
                fireZoneViews[i].localScale = new Vector3(zone.Radius * 2f, 0.25f, zone.Radius * 2f);
                fireZoneViews[i].position = new Vector3(zone.CenterX, 0.15f, zone.CenterZ);
            }
        }

        // ───────────────────────────── 拾取 ─────────────────────────────

        /// <summary>把屏幕坐标打到单位上（用于选择）。</summary>
        public bool TryPickUnit(Vector3 screenPosition, out string unitId)
        {
            unitId = string.Empty;
            if (Camera is null)
            {
                return false;
            }

            var ray = Camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 1000f))
            {
                return false;
            }

            var proxy = hit.collider.GetComponent<UnitProxy>();
            if (proxy is null)
            {
                return false;
            }

            unitId = proxy.UnitId;
            return true;
        }

        /// <summary>把屏幕坐标打到地面（用于移动/施法落点）。</summary>
        public bool TryPickGround(Vector3 screenPosition, out MapPoint point)
        {
            point = MapPoint.Zero;
            if (Camera is null)
            {
                return false;
            }

            var ray = Camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            var world = ray.GetPoint(distance);
            point = new MapPoint(world.x, world.z);
            return true;
        }

        // ───────────────────────────── 工具 ─────────────────────────────

        private static MapPoint FromWorld(Vector3 world) => new(world.x, world.z);

        private static Vector3 ToWorld(MapPoint point) => new(point.X, 1.2f, point.Z);

        private static Transform CreatePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.transform;
        }

        /// <summary>
        /// 每个渲染器独占并复用一份材质实例。
        /// 血条/士气条/焚毁状态的<b>颜色每帧都在变</b>，若每次改色都 <c>new Material</c> 会持续泄漏
        /// （18 单位 × 2 条 × 60fps ≈ 2160 份/秒），且 <see cref="Shader.Find(string)"/> 是字符串查找，
        /// 每帧调用代价很高。这里只在首次接触某渲染器时创建一次，之后只改 <c>color</c>。
        /// </summary>
        private readonly Dictionary<Renderer, Material> perRendererMaterials = new();

        private void SetColor(GameObject go, Color color, bool transparent = false)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer is null)
            {
                return;
            }

            if (!perRendererMaterials.TryGetValue(renderer, out var material) || material == null)
            {
                var shader = transparent
                    ? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard")
                    : Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
                material = new Material(shader);
                perRendererMaterials[renderer] = material;
                renderer.sharedMaterial = material;
            }

            if (material.HasProperty("_Color"))
            {
                material.color = color;
            }
        }
    }

}