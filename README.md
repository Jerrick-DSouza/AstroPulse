# AstroPulse: Spaceflight Telemetry & Crew Health Engine 🚀

> **NASA Space Apps Challenge Submission**  
> A deterministic, real-time space operations and life-support telemetry monitoring engine designed to track habitat health and astronaut physiological parameters during long-duration space missions.

---

## 🌟 Overview

As humanity pushes further into deep space, monitoring complex spacecraft life-support systems and crew health in real time becomes mission-critical. **AstroPulse** is a lightweight, high-performance web dashboard and simulation engine built to process, synthesize, and display deterministic telemetry data without external AI/ML dependencies. 

By translating official spaceflight hazards and operational standards into live system logic, AstroPulse bridges habitat environmental health with real-time crew physiological tracking.

---

## ✨ Key Features

* **Deterministic TelemetryEngine:** Generates high-frequency, reliable simulation data streams for environmental metrics (cabin pressure, temperature, acoustic levels) and space radiation flux without relying on heavy external machine learning models.
* **Real-Time Crew Health Monitoring:** Tracks physiological markers and stress indicators inspired by human spaceflight risk frameworks to ensure early detection of operator fatigue or anomaly stress.
* **Interactive Alert & Anomaly Control:** Features emergency simulation toggles and threshold boundaries allowing operators to test system responses against sudden life-support failures.
* **Human-Centered UI Design:** Built with clean data visualizations and responsive layouts to minimize operator cognitive load during high-stress scenarios.

---

## 📂 Architecture & Tech Stack

* **Framework:** Blazor Web App (.NET)
* **Core Logic:** Custom C# `TelemetryEngine` for real-time state management and deterministic data generation.
* **Design & Standards Compliance:** Grounded in official human-systems integration standards and spatial UI principles.

---

## 📚 Grounding & Standards References

AstroPulse’s design architecture, metric thresholds, and environmental limits are informed by official space agency frameworks and standards:

* **5 Hazards of Human Spaceflight:** Framework for categorizing environmental and operational stressors (Radiation, Isolation, Distance, etc.).
* **Risks of Human Spaceflight:** Physiological impact references for long-duration space missions.
* **Ongoing HRP Research in Space:** Continuous health tracking paradigms.
* **Monitoring Radiation for Astronaut Health:** Real-time radiation and neutron flux monitoring models (CSA).
* **NASA-STD-3001 (Vols. 1 & 2):** Space Flight Human-System Standards for crew health and habitability limits.

---

## 🛠️ Getting Started / Running Locally

**Clone the repository:**
```bash
git clone https://github.com/Jerrick-DSouza/AstroPulse.git
cd AstroPulse

Open in Visual Studio / VS Code:
Open the solution file or directory using Visual Studio 2022/2026 or .NET CLI.

Run the project:
dotnet run

Access the Dashboard:
Open your browser and navigate to https://localhost:5001 (or the local port specified in your launch settings).

## 👥 Team & Submission

| Category | Details |
| :--- | :--- |
| **Event** | NASA Space Apps Challenge 2026 |
| **Project** | AstroPulse: Spaceflight Telemetry & Crew Health Engine |
| **Status** | Final Submission Ready |
