using System.Linq;
using UnityEngine;
using Waterfall;

public class UnifiedWaterfallControlModule : PartModule
{
    // =========================
    // Transforms
    // =========================
    Transform tt10, tt11, tt12, tt13;
    Transform tt14, tt15, tt16, tt17;

    Transform thrustTransform1, thrustTransform2, thrustTransform3, thrustTransform4;

    // =========================
    // Debug
    // =========================
    [KSPField(guiActive = false)] public float dbgTT10;
    [KSPField(guiActive = false)] public float dbgTT11;
    [KSPField(guiActive = false)] public float dbgTT12;
    [KSPField(guiActive = false)] public float dbgTT13;
    [KSPField(guiActive = false)] public float dbgTT14;
    [KSPField(guiActive = false)] public float dbgTT15;
    [KSPField(guiActive = false)] public float dbgTT16;
    [KSPField(guiActive = false)] public float dbgTT17;

    [KSPField(guiActive = false)] public float sunlight;

    [KSPField(guiActive = false)] public float dbgOcc0;
    [KSPField(guiActive = false)] public float dbgOcc1;
    [KSPField(guiActive = false)] public float dbgOcc2;
    [KSPField(guiActive = false)] public float dbgOcc3;

    [KSPField(guiActive = false)] public float rcsFront;
    [KSPField(guiActive = false)] public float rcsBack;
    [KSPField(guiActive = false)] public float rcsLeft;
    [KSPField(guiActive = false)] public float rcsRight;
    [KSPField(guiActive = false)] public float rcsUp;
    [KSPField(guiActive = false)] public float rcsDown;

    [KSPField(guiActive = false)] public int upndown;
    [KSPField(guiActive = false)] public float downdown; // ⭐ 新增：downdown 变量
    [KSPField(guiActive = false)] public float downVelocity;
    [KSPField(guiActive = false)] public float landingBurnInner;
    [KSPField(guiActive = false)] public float landingBurnCore;
    [KSPField(guiActive = false)] public float dbgAscendHeight;

    [KSPField(guiActive = false)] public float dumpThrust;
    [KSPField(guiActive = false)] public Vector3 dumpVelocity;

    // =========================
    // Modules
    // =========================
    ModuleWaterfallFX[] waterFX;
    ModuleEnginesFX engineInner;
    ModuleEnginesFX engineCore;
    ModuleEnginesFX engineDump;
    ModuleRCSFX[] rcsModules;

    // =========================
    // Private
    // =========================
    private Rigidbody rb;
    private Vector3 lastPos;
    private float lastAltitude;
    private float ascendHeightAccum;

    // ⭐ 平滑缓存
    private float smoothSunlight = 0f;

    // =========================
    // Start
    // =========================
    public override void OnStart(StartState state)
    {
        tt10 = part.FindModelTransform("thrustTransform10");
        tt11 = part.FindModelTransform("thrustTransform11");
        tt12 = part.FindModelTransform("thrustTransform12");
        tt13 = part.FindModelTransform("thrustTransform13");
        tt14 = part.FindModelTransform("thrustTransform14");
        tt15 = part.FindModelTransform("thrustTransform15");
        tt16 = part.FindModelTransform("thrustTransform16");
        tt17 = part.FindModelTransform("thrustTransform17");

        thrustTransform1 = part.FindModelTransform("thrustTransform1");
        thrustTransform2 = part.FindModelTransform("thrustTransform2");
        thrustTransform3 = part.FindModelTransform("thrustTransform3");
        thrustTransform4 = part.FindModelTransform("thrustTransform4");

        waterFX = part.FindModulesImplementing<ModuleWaterfallFX>().ToArray();

        var engines = part.FindModulesImplementing<ModuleEnginesFX>();
        engineInner = engines.FirstOrDefault(e => e.engineID == "Inner");
        engineCore = engines.FirstOrDefault(e => e.engineID == "Core");
        engineDump = engines.FirstOrDefault(e => e.engineID == "dump");

        rcsModules = part.FindModulesImplementing<ModuleRCSFX>().ToArray();

        rb = part.rb ?? part.GetComponent<Rigidbody>();
        lastPos = part.transform.position;
        lastAltitude = (float)vessel.altitude;

        dumpVelocity = Vector3.zero;
    }

    // =========================
    // Update
    // =========================
    public void FixedUpdate()
    {
        if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;

        Vector3d upD = vessel.transform.position - vessel.mainBody.position;
        Vector3 worldUp = ((Vector3)upD).normalized;
        Vector3 vel = (Vector3)vessel.obt_velocity;

        float verticalSpeed = Vector3.Dot(vel, worldUp);
        float altitude = (float)vessel.altitude;

        bool ascending = verticalSpeed > 0.1f;
        bool descending = verticalSpeed < -0.1f;

        // ⭐ 新增/完善计算逻辑：赋值给我们要激活的变量
        upndown = ascending ? 1 : descending ? -1 : 0;
        downdown = descending ? 1f : 0f; // 如果正在下降则为 1，否则为 0
        downVelocity = Mathf.Max(0f, -verticalSpeed); // 获取纯粹的下降速度 (m/s)，如果未下降则为 0

        float thrust = engineDump?.finalThrust ?? 0f;
        dumpThrust = thrust > 0.0001f ? 1f : 0f;

        // Velocity
        Vector3 worldVel = (rb != null && !rb.isKinematic)
            ? rb.velocity
            : (Vector3)vessel.srf_velocity;

        Vector3 localVel = part.transform.InverseTransformDirection(worldVel);

        const float maxSpeed = 80f;
        Vector3 targetVelocity = new Vector3(
            Mathf.Clamp(localVel.x / maxSpeed * 2f, -2f, 2f),
            Mathf.Clamp(localVel.y / maxSpeed * 2f, -2f, 2f),
            Mathf.Clamp(localVel.z / maxSpeed * 2f, -2f, 2f)
        );

        dumpVelocity = Vector3.Lerp(dumpVelocity, targetVelocity, 0.12f);

        // 上升检测
        if (ascending) ascendHeightAccum += Mathf.Max(0f, altitude - lastAltitude);
        else ascendHeightAccum = 0f;

        lastAltitude = altitude;

        // TT角度
        if (tt10) dbgTT10 = CalcAngle(tt10);
        if (tt11) dbgTT11 = CalcAngle(tt11);
        if (tt12) dbgTT12 = CalcAngle(tt12);
        if (tt13) dbgTT13 = CalcAngle(tt13);
        if (tt14) dbgTT14 = CalcAngle(tt14);
        if (tt15) dbgTT15 = CalcAngle(tt15);
        if (tt16) dbgTT16 = CalcAngle(tt16);
        if (tt17) dbgTT17 = CalcAngle(tt17);

        // =========================
        // 🌞 全局太阳光
        // =========================
        double flux = vessel.solarFlux;
        const double refFlux = 1360.0;
        float globalSun = (float)(flux / refFlux);

        // =========================
        // 🌑 遮挡检测
        // =========================
        float o0 = CalcOcclusion(thrustTransform1);
        float o1 = CalcOcclusion(thrustTransform2);
        float o2 = CalcOcclusion(thrustTransform3);
        float o3 = CalcOcclusion(thrustTransform4);

        dbgOcc0 = o0;
        dbgOcc1 = o1;
        dbgOcc2 = o2;
        dbgOcc3 = o3;

        float occlusion = Mathf.Max(o0, o1, o2, o3);

        // =========================
        // ⭐ 平滑 sunlight（核心）
        // =========================
        float targetSun = globalSun * occlusion;

        float smoothSpeed = 6f; // 可调：3~10
        smoothSunlight = Mathf.Lerp(smoothSunlight, targetSun, Time.fixedDeltaTime * smoothSpeed);

        sunlight = smoothSunlight;

        if (float.IsNaN(sunlight) || float.IsInfinity(sunlight))
            sunlight = 0f;

        sunlight = Mathf.Clamp(sunlight, 0f, 2f);

        PushAll();
    }

    float CalcAngle(Transform t)
    {
        if (t == null) return 0f;

        Vector3 worldDir = t.forward.normalized;
        Vector3d upD = part.transform.position - vessel.mainBody.position;

        Quaternion inv = Quaternion.Inverse(part.transform.rotation);
        Vector3 dir = inv * worldDir;
        Vector3 up = inv * ((Vector3)upD).normalized;

        return Mathf.Asin(Vector3.Dot(dir, up)) * Mathf.Rad2Deg;
    }

    float CalcOcclusion(Transform t)
    {
        if (t == null) return 0f;

        var sun = Planetarium.fetch.Sun;
        if (sun == null) return 0f;

        Vector3 origin = t.position;
        Vector3 dir = (sun.position - origin).normalized;

        RaycastHit hit;

        if (Physics.Raycast(origin, dir, out hit, 1000000f))
        {
            if (hit.transform != part.transform)
                return 0f;
        }

        return 1f;
    }

    void PushAll()
    {
        foreach (var fx in waterFX)
        {
            if (fx == null || fx.Controllers == null) continue;

            fx.Controllers.FirstOrDefault(c => c.name == "DumpX")?.Set(dumpVelocity.x);
            fx.Controllers.FirstOrDefault(c => c.name == "DumpY")?.Set(dumpVelocity.y);
            fx.Controllers.FirstOrDefault(c => c.name == "DumpZ")?.Set(dumpVelocity.z);
            fx.Controllers.FirstOrDefault(c => c.name == "DumpState")?.Set(dumpThrust);

            fx.Controllers.FirstOrDefault(c => c.name == "TT10")?.Set(dbgTT10);
            fx.Controllers.FirstOrDefault(c => c.name == "TT11")?.Set(dbgTT11);
            fx.Controllers.FirstOrDefault(c => c.name == "TT12")?.Set(dbgTT12);
            fx.Controllers.FirstOrDefault(c => c.name == "TT13")?.Set(dbgTT13);
            fx.Controllers.FirstOrDefault(c => c.name == "TT14")?.Set(dbgTT14);
            fx.Controllers.FirstOrDefault(c => c.name == "TT15")?.Set(dbgTT15);
            fx.Controllers.FirstOrDefault(c => c.name == "TT16")?.Set(dbgTT16);
            fx.Controllers.FirstOrDefault(c => c.name == "TT17")?.Set(dbgTT17);

            fx.Controllers.FirstOrDefault(c => c.name == "Sunlight")?.Set(sunlight);

            fx.Controllers.FirstOrDefault(c => c.name == "front")?.Set(rcsFront);
            fx.Controllers.FirstOrDefault(c => c.name == "back")?.Set(rcsBack);
            fx.Controllers.FirstOrDefault(c => c.name == "left")?.Set(rcsLeft);
            fx.Controllers.FirstOrDefault(c => c.name == "right")?.Set(rcsRight);
            fx.Controllers.FirstOrDefault(c => c.name == "up")?.Set(rcsUp);
            fx.Controllers.FirstOrDefault(c => c.name == "down")?.Set(rcsDown);

            // ⭐ 新增：向 Waterfall 传递这三个控制器的值
            fx.Controllers.FirstOrDefault(c => c.name == "upndown")?.Set((float)upndown);
            fx.Controllers.FirstOrDefault(c => c.name == "downdown")?.Set(downdown);
            fx.Controllers.FirstOrDefault(c => c.name == "downVelocity")?.Set(downVelocity);
        }
    }
}