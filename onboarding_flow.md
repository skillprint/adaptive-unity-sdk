# Skillprint Developer Onboarding & Dynamic Feedback Loop

This document presents the visual design and structural flow of the self-service onboarding and integration process for game developers integrating the Skillprint SDK.

The vector source file has been saved in the repository root at:
[Skillprint_Onboarding_Flow.svg](Skillprint_Onboarding_Flow.svg)

> [!TIP]
> You can import `Skillprint_Onboarding_Flow.svg` directly into **Figma** as editable vector groups, permitting easy scaling, text tweaks, and exporting to PNG/PDF.

---

## Onboarding & Integration Overview

![Skillprint Onboarding Flow Diagram](/Users/jeremy/.gemini/antigravity-ide/brain/0acea2a3-12fb-41ef-951f-2de9fc8600f5/onboarding_flow_diagram_1780939559398.png)

---

## Detailed Step Walkthrough

| Phase | Step | Target Component | Key Actions & Value-Add |
|---|---|---|---|
| **1. Self-Service Portal** | **1. Create Developer Account** | `Developer Portal` | Sign up at `dev.skillprint.co` to register your company and set up your admin workspace. |
| | **2. Register Game Profile** | `Game Registry` | Register your game client (e.g., name, platform type) and select target mood filters (e.g., *Focus*, *Relax*, *Grit*, *Zen*). |
| | **3. Generate Partner API Key** | `Credentials` | Generate a secure, unique API key to allow your game client to securely communicate telemetry back to Skillprint's servers. |
| **2. SDK Integration** | **4. Install Skillprint SDK** | `Game Code` | Import the Unity Package Manager (UPM) package or Cocos Creator npm module into your project code structure. |
| | **5. Configure Credentials** | `SkillprintConfig` | Paste the API Key and Game Name slug into the Unity config inspector asset or Cocos config component to bind the SDK to your backend profile. |
| | **6. Define Game Parameters** | `Auto-Provisioning` | Define your dynamically tunable game variables (e.g., `speed`, `gravityStrength`, `hintFrequency`) with minimum, maximum, and default ranges directly in code. These auto-register on the backend during the first session. |
| **3. Live Adjustment Loop** | **A. Active Gameplay** | `SDK Telemetry` | As users play, the SDK automatically initiates sessions and transmits periodic visual screenshot chunks + event data to the API. |
| | **B. real-time AI Analysis** | `Skillprint AI Engine` | **Developer Value-Add:** Gemini VLM analyzes visual game screens to evaluate cognitive engagement, mood response, and difficulty ratings in real-time, mapping behavior to cognitive profiles. |
| | **C. Live Parameter Adjustments** | `Dynamic Mechanics` | The game client receives real-time parameter updates (`parameter_updates`) from the API, seamlessly dialing up/down challenge, speed, or help mechanisms. |
