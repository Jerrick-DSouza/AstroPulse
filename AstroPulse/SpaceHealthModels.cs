using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AstroPulse.Models
{
    public enum SpaceHazard
    {
        HostileClosedEnvironments,
        GravityFields,
        DistanceFromEarth,
        IsolationAndConfinement,
        SpaceRadiation
    }

    public enum GravityMode
    {
        ZeroG,
        MarsOneThirdG,
        EarthOneG
    }

    public enum AlertSeverity
    {
        Nominal,
        Warning,
        Critical
    }

    public class HostileClosedEnvironmentsTelemetry
    {
        public double CabinTempC { get; set; } = 22.0;          // Nominal: 18.0 - 27.0 °C
        public double CabinPressureKpa { get; set; } = 101.3;   // Nominal: 101.3 kPa (1 atm)
        public double NoiseDb { get; set; } = 52.0;              // Nominal: < 60 dB continuous
        public double MicrobialImmuneIndex { get; set; } = 18.0; // Scale 0-100 (< 40 nominal)
    }

    public class GravityFieldsTelemetry
    {
        public GravityMode CurrentGravityMode { get; set; } = GravityMode.ZeroG;
        public int HeartRateBpm { get; set; } = 72;              // Nominal: 60 - 100 bpm
        public double HydrationPercent { get; set; } = 88.0;    // Nominal: >= 80%
        public double MonthlyBoneLossRate { get; set; } = 1.25; // 1.0% - 1.5%/month in ZeroG
    }

    public class DistanceFromEarthTelemetry
    {
        public double DistanceMillionMiles { get; set; } = 140.0; // Avg Mars distance: ~140M miles
        public double OneWayCommsDelayMinutes { get; set; } = 12.5; // Up to 20.0 mins one-way
        public bool AutonomousModeActive => OneWayCommsDelayMinutes > 1.0;
    }

    public class IsolationAndConfinementTelemetry
    {
        public double SleepHours { get; set; } = 7.2;                // Nominal: >= 6.5 hrs
        public double CircadianAlignmentPercent { get; set; } = 92.0; // Nominal: >= 80%
        public double WorkloadIndex { get; set; } = 45.0;            // Scale 0-100 (< 75 nominal)
        public bool LightTherapyActive { get; set; } = false;
    }

    public class SpaceRadiationTelemetry
    {
        // Modeled on Canadian Space Agency (CSA) CANS Neutron Spectrometer
        public double RadiationMicroSvPerHour { get; set; } = 6.25; // ~150 µSv/day ISS baseline
        public double CumulativeDoseMilliSv { get; set; } = 142.4;  // NASA/CSA Career Limit: 600 mSv
        public bool SolarParticleEventActive { get; set; } = false;
    }

    public class AstroHealthSnapshot
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public HostileClosedEnvironmentsTelemetry HostileEnvironment { get; set; } = new();
        public GravityFieldsTelemetry GravityFields { get; set; } = new();
        public DistanceFromEarthTelemetry DistanceFromEarth { get; set; } = new();
        public IsolationAndConfinementTelemetry IsolationConfinement { get; set; } = new();
        public SpaceRadiationTelemetry SpaceRadiation { get; set; } = new();
        public List<NasaHealthAlert> ActiveAlerts { get; set; } = new();
    }

    public record NasaHealthAlert(
        string Id,
        SpaceHazard Hazard,
        AlertSeverity Severity,
        string NasaHrpRiskTitle,
        string ReferenceSource,
        string Description,
        string RecommendedCountermeasure,
        DateTime TriggeredAtUtc
    );

    public class TelemetryEngine : IDisposable
    {
        private readonly Random _rng = new();
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        private readonly List<AstroHealthSnapshot> _snapshotHistory = new();
        public IReadOnlyList<AstroHealthSnapshot> SnapshotHistory => _snapshotHistory;

        public AstroHealthSnapshot CurrentSnapshot { get; private set; } = new();

        /// <summary>
        /// Fires every 1 second with updated telemetry and evaluated NASA alerts.
        /// Subscribe to this in your Blazor component or SignalR hub.
        /// </summary>
        public event Action<AstroHealthSnapshot>? OnTelemetryUpdated;

        public void Start()
        {
            if (_loopTask != null && !_loopTask.IsCompleted) return;

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunSimulationLoopAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        // Interactive Demo Controls for Dashboard Buttons
        public void SetSolarParticleEvent(bool active)
        {
            CurrentSnapshot.SpaceRadiation.SolarParticleEventActive = active;
            EvaluateThresholds(CurrentSnapshot);
            OnTelemetryUpdated?.Invoke(CurrentSnapshot);
        }

        public void SetGravityMode(GravityMode mode)
        {
            CurrentSnapshot.GravityFields.CurrentGravityMode = mode;
            CurrentSnapshot.GravityFields.MonthlyBoneLossRate = mode switch
            {
                GravityMode.ZeroG => 1.25,
                GravityMode.MarsOneThirdG => 0.45,
                GravityMode.EarthOneG => 0.0,
                _ => 1.25
            };
            EvaluateThresholds(CurrentSnapshot);
            OnTelemetryUpdated?.Invoke(CurrentSnapshot);
        }

        public void ToggleLightTherapy()
        {
            CurrentSnapshot.IsolationConfinement.LightTherapyActive =
                !CurrentSnapshot.IsolationConfinement.LightTherapyActive;
            EvaluateThresholds(CurrentSnapshot);
            OnTelemetryUpdated?.Invoke(CurrentSnapshot);
        }

        public void SetCommsDelay(double minutes)
        {
            CurrentSnapshot.DistanceFromEarth.OneWayCommsDelayMinutes = Math.Clamp(minutes, 0.0, 20.0);
            CurrentSnapshot.DistanceFromEarth.DistanceMillionMiles =
                Math.Round((CurrentSnapshot.DistanceFromEarth.OneWayCommsDelayMinutes / 20.0) * 225.0, 1);
            EvaluateThresholds(CurrentSnapshot);
            OnTelemetryUpdated?.Invoke(CurrentSnapshot);
        }

        private async Task RunSimulationLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    StepSimulation();
                    EvaluateThresholds(CurrentSnapshot);

                    // Keep a rolling history of the last 300 telemetry snapshots (5 minutes)
                    lock (_snapshotHistory)
                    {
                        // Create a shallow/deep clone snapshot or store reference
                        _snapshotHistory.Add(new AstroHealthSnapshot
                        {
                            TimestampUtc = CurrentSnapshot.TimestampUtc,
                            HostileEnvironment = new HostileClosedEnvironmentsTelemetry
                            {
                                CabinTempC = CurrentSnapshot.HostileEnvironment.CabinTempC,
                                CabinPressureKpa = CurrentSnapshot.HostileEnvironment.CabinPressureKpa,
                                NoiseDb = CurrentSnapshot.HostileEnvironment.NoiseDb,
                                MicrobialImmuneIndex = CurrentSnapshot.HostileEnvironment.MicrobialImmuneIndex
                            },
                            GravityFields = new GravityFieldsTelemetry
                            {
                                CurrentGravityMode = CurrentSnapshot.GravityFields.CurrentGravityMode,
                                HeartRateBpm = CurrentSnapshot.GravityFields.HeartRateBpm,
                                HydrationPercent = CurrentSnapshot.GravityFields.HydrationPercent,
                                MonthlyBoneLossRate = CurrentSnapshot.GravityFields.MonthlyBoneLossRate
                            },
                            DistanceFromEarth = new DistanceFromEarthTelemetry
                            {
                                DistanceMillionMiles = CurrentSnapshot.DistanceFromEarth.DistanceMillionMiles,
                                OneWayCommsDelayMinutes = CurrentSnapshot.DistanceFromEarth.OneWayCommsDelayMinutes
                            },
                            IsolationConfinement = new IsolationAndConfinementTelemetry
                            {
                                SleepHours = CurrentSnapshot.IsolationConfinement.SleepHours,
                                CircadianAlignmentPercent = CurrentSnapshot.IsolationConfinement.CircadianAlignmentPercent,
                                WorkloadIndex = CurrentSnapshot.IsolationConfinement.WorkloadIndex,
                                LightTherapyActive = CurrentSnapshot.IsolationConfinement.LightTherapyActive
                            },
                            SpaceRadiation = new SpaceRadiationTelemetry
                            {
                                RadiationMicroSvPerHour = CurrentSnapshot.SpaceRadiation.RadiationMicroSvPerHour,
                                CumulativeDoseMilliSv = CurrentSnapshot.SpaceRadiation.CumulativeDoseMilliSv,
                                SolarParticleEventActive = CurrentSnapshot.SpaceRadiation.SolarParticleEventActive
                            },
                            ActiveAlerts = new List<NasaHealthAlert>(CurrentSnapshot.ActiveAlerts)
                        });

                        if (_snapshotHistory.Count > 300)
                        {
                            _snapshotHistory.RemoveAt(0);
                        }
                    }

                    OnTelemetryUpdated?.Invoke(CurrentSnapshot);
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void StepSimulation()
        {
            CurrentSnapshot.TimestampUtc = DateTime.UtcNow;

            // 1. Hostile / Closed Environments drift
            var env = CurrentSnapshot.HostileEnvironment;
            env.CabinTempC = Math.Round(Math.Clamp(env.CabinTempC + NextDelta(0.15), 16.0, 30.0), 1);
            env.CabinPressureKpa = Math.Round(Math.Clamp(env.CabinPressureKpa + NextDelta(0.08), 94.0, 103.0), 2);
            env.NoiseDb = Math.Round(Math.Clamp(env.NoiseDb + NextDelta(1.2), 45.0, 75.0), 1);
            env.MicrobialImmuneIndex = Math.Round(Math.Clamp(env.MicrobialImmuneIndex + NextDelta(0.8), 5.0, 95.0), 1);

            // 2. Gravity Fields physiology
            var grav = CurrentSnapshot.GravityFields;
            int hrTarget = grav.CurrentGravityMode == GravityMode.MarsOneThirdG ? 98 : 74;
            grav.HeartRateBpm = Math.Clamp(grav.HeartRateBpm + _rng.Next(-2, 3) + Math.Sign(hrTarget - grav.HeartRateBpm), 52, 145);
            grav.HydrationPercent = Math.Round(Math.Clamp(grav.HydrationPercent - 0.02 + NextDelta(0.05), 65.0, 100.0), 1);
            if (grav.CurrentGravityMode == GravityMode.ZeroG)
            {
                grav.MonthlyBoneLossRate = Math.Round(Math.Clamp(grav.MonthlyBoneLossRate + NextDelta(0.02), 1.0, 1.5), 2);
            }

            // 3. Isolation & Confinement
            var iso = CurrentSnapshot.IsolationConfinement;
            if (iso.LightTherapyActive)
            {
                iso.CircadianAlignmentPercent = Math.Round(Math.Min(98.0, iso.CircadianAlignmentPercent + 0.4), 1);
            }
            else
            {
                iso.CircadianAlignmentPercent = Math.Round(Math.Clamp(iso.CircadianAlignmentPercent + NextDelta(0.3), 60.0, 98.0), 1);
            }
            iso.WorkloadIndex = Math.Round(Math.Clamp(iso.WorkloadIndex + NextDelta(0.9), 20.0, 95.0), 1);

            // 4. Space Radiation (CSA CANS Spectrometer simulation)
            var rad = CurrentSnapshot.SpaceRadiation;
            if (rad.SolarParticleEventActive)
            {
                rad.RadiationMicroSvPerHour = Math.Round(420.0 + _rng.NextDouble() * 85.0, 2);
            }
            else
            {
                rad.RadiationMicroSvPerHour = Math.Round(Math.Clamp(6.25 + NextDelta(0.35), 4.8, 9.5), 2);
            }

            // Increment cumulative mSv (accelerated slightly for live telemetry visibility)
            rad.CumulativeDoseMilliSv = Math.Round(
                rad.CumulativeDoseMilliSv + (rad.RadiationMicroSvPerHour / 1000.0 / 3600.0) * 100.0, 4);
        }

        private void EvaluateThresholds(AstroHealthSnapshot snapshot)
        {
            var alerts = new List<NasaHealthAlert>();
            var now = snapshot.TimestampUtc;

            // --- 1. Hostile / Closed Environments Rules ---
            var env = snapshot.HostileEnvironment;
            if (env.CabinTempC < 18.0 || env.CabinTempC > 27.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "ENV-TEMP",
                    SpaceHazard.HostileClosedEnvironments,
                    AlertSeverity.Warning,
                    "Risk of Crew Thermal Stress & Habitat Environmental Limits",
                    "NASA-STD-3001 Vol. 2 / HRP Hostile Environments",
                    $"Cabin temperature ({env.CabinTempC}°C) is outside the 18.0–27.0°C nominal operating envelope.",
                    "Adjust Environmental Control and Life Support System (ECLSS) thermal loop setpoints.",
                    now
                ));
            }

            if (env.CabinPressureKpa < 97.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "ENV-PRES",
                    SpaceHazard.HostileClosedEnvironments,
                    AlertSeverity.Critical,
                    "Risk of Hypoxia & Decompression Sickness",
                    "NASA HRP Risks of Human Spaceflight / NASA-STD-3001",
                    $"Cabin pressure dropped to {env.CabinPressureKpa} kPa (nominal 101.3 kPa).",
                    "Verify hull integrity, check O2/N2 pressure regulators, and prepare crew oxygen masks.",
                    now
                ));
            }

            if (env.NoiseDb >= 60.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "ENV-NOISE",
                    SpaceHazard.HostileClosedEnvironments,
                    AlertSeverity.Warning,
                    "Risk of Acoustic Fatigue & Sleep Disruption",
                    "NASA-STD-3001 Vol. 2 Acoustic Limits",
                    $"Continuous cabin acoustic level ({env.NoiseDb} dBA) exceeds 60 dBA habitability threshold.",
                    "Inspect ventilation fan acoustics and issue crew acoustic protection.",
                    now
                ));
            }

            if (env.MicrobialImmuneIndex > 50.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "ENV-MICRO",
                    SpaceHazard.HostileClosedEnvironments,
                    AlertSeverity.Warning,
                    "Risk of Altered Immune System Responses & Host-Microorganism Interactions",
                    "NASA HRP Closed Habitat Microbiology Research",
                    $"Elevated closed-habitat microbial/immune stress index ({env.MicrobialImmuneIndex}/100).",
                    "Activate HEPA/antimicrobial air scrubbing and log crew immune biomarker panel.",
                    now
                ));
            }

            // --- 2. Gravity Fields Rules ---
            var grav = snapshot.GravityFields;
            if (grav.CurrentGravityMode == GravityMode.ZeroG && grav.MonthlyBoneLossRate >= 1.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "GRAV-BONE",
                    SpaceHazard.GravityFields,
                    AlertSeverity.Warning,
                    "Risk of Spaceflight-Induced Bone Change & Renal Stone Formation",
                    "NASA HRP Gravity Fields (1.0%–1.5%/mo Bone Mineral Loss)",
                    $"Microgravity bone mineral density loss projected at {grav.MonthlyBoneLossRate}%/month.",
                    "Enforce 2-hour daily ARED resistive exercise protocol and monitor urinary calcium.",
                    now
                ));
            }

            if (grav.HydrationPercent < 78.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "GRAV-HYDR",
                    SpaceHazard.GravityFields,
                    AlertSeverity.Warning,
                    "Risk of Renal Stone Formation & Orthostatic Intolerance",
                    "NASA HRP Fluid Shift & Hydration Countermeasures",
                    $"Crew hydration dropped to {grav.HydrationPercent}%, elevating calcium kidney stone risk.",
                    "Administer oral electrolyte fluid loading protocol immediately.",
                    now
                ));
            }

            if (grav.HeartRateBpm > 110 || grav.CurrentGravityMode == GravityMode.MarsOneThirdG)
            {
                alerts.Add(new NasaHealthAlert(
                    "GRAV-CARDIO",
                    SpaceHazard.GravityFields,
                    AlertSeverity.Warning,
                    "Risk of Cardiovascular Adaptations & Orthostatic Lightheadedness",
                    "NASA HRP Gravity Transition Guidelines (0G to 0.38G Mars)",
                    $"Elevated cardiovascular load ({grav.HeartRateBpm} bpm) during {grav.CurrentGravityMode} operations.",
                    "Engage lower-body compression garments and monitor blood pressure during vertical posture.",
                    now
                ));
            }

            // --- 3. Distance From Earth Rules ---
            var dist = snapshot.DistanceFromEarth;
            if (dist.AutonomousModeActive)
            {
                alerts.Add(new NasaHealthAlert(
                    "DIST-AUTO",
                    SpaceHazard.DistanceFromEarth,
                    AlertSeverity.Nominal,
                    "Autonomous On-Board Clinical Decision Support Active",
                    "NASA HRP Distance From Earth (Up to 20-Min One-Way Mars Latency)",
                    $"One-way Earth comms delay is {dist.OneWayCommsDelayMinutes:F1} min ({dist.DistanceMillionMiles}M miles). Earth-based real-time triage unavailable.",
                    "Local AstroHealth edge rule engine executing autonomous diagnostics without Mission Control wait.",
                    now
                ));
            }

            // --- 4. Isolation & Confinement Rules ---
            var iso = snapshot.IsolationConfinement;
            if (iso.SleepHours < 6.5 || iso.CircadianAlignmentPercent < 80.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "ISO-SLEEP",
                    SpaceHazard.IsolationAndConfinement,
                    AlertSeverity.Warning,
                    "Risk of Inadequate Sleep, Circadian Desynchronization & Behavioral Performance",
                    "NASA HRP Behavioral Health & Light Therapy Research",
                    $"Sleep ({iso.SleepHours:F1}h) or circadian alignment ({iso.CircadianAlignmentPercent}%) below mission target.",
                    iso.LightTherapyActive
                        ? "Blue-enriched LED circadian light therapy currently active; maintain schedule."
                        : "Activate blue-enriched circadian light therapy and reduce non-critical workload.",
                    now
                ));
            }

            // --- 5. Space Radiation Rules (CSA CANS + NASA HRP) ---
            var rad = snapshot.SpaceRadiation;
            if (rad.SolarParticleEventActive || rad.RadiationMicroSvPerHour > 50.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "RAD-SPE",
                    SpaceHazard.SpaceRadiation,
                    AlertSeverity.Critical,
                    "Acute Solar Particle Event (SPE) — Central Nervous System & Cancer Risk",
                    "CSA CANS Neutron Spectrometer / NASA HRP Space Radiation",
                    $"CRITICAL IONIZING RADIATION SPIKE: {rad.RadiationMicroSvPerHour} µSv/hr detected outside Earth's magnetosphere.",
                    "IMMEDIATE ACTION: Relocate crew to spacecraft water/polyethylene storm shelter until SPE subsides.",
                    now
                ));
            }

            if (rad.CumulativeDoseMilliSv >= 500.0)
            {
                alerts.Add(new NasaHealthAlert(
                    "RAD-CAREER",
                    SpaceHazard.SpaceRadiation,
                    AlertSeverity.Critical,
                    "Approaching NASA/CSA 600 mSv Career Permissible Exposure Limit",
                    "NASA-STD-3001 Vol. 1 [V1 4030] / CSA Radiation Monitoring",
                    $"Cumulative mission radiation dose reached {rad.CumulativeDoseMilliSv:F1} mSv of 600 mSv career limit.",
                    "Restrict extra-vehicular activities (EVA) and enforce ALARA shielding protocols.",
                    now
                ));
            }

            snapshot.ActiveAlerts = alerts;
        }

        private double NextDelta(double maxStep)
        {
            return (_rng.NextDouble() * 2.0 - 1.0) * maxStep;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}