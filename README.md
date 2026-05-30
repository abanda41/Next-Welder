# NEXT WELDER

**Train with precision. Weld with confidence.**

NEXT WELDER is an immersive VR welding training prototype designed to provide a safe, repeatable, and measurable environment for practical skill development. The simulator combines hands-on welding interaction with real-time process monitoring, visual defect generation, guided onboarding, and an end-of-weld performance report.

## Core Features

- VR welding practice with multiple joint configurations
- Real-time tracking of travel angle, work angle, travel speed, and contact-tip-to-work distance (CTWD)
- Three-level live parameter feedback: green, orange, and red
- Parameter-dependent weld quality defect generation
- Arc lighting, particle effects, spatial welding audio, and controller haptics
- Guided VR tutorials for navigation, workstation setup, and pre-weld positioning
- Final weld report with measured values, priority corrections, and a weld quality breakdown
- Defect review mode for inspecting the completed weld path

## Training Workflow

1. Select a joint configuration and enter the workshop.
2. Adjust the workstation and position the welding torch.
3. Use the live HUD to verify the welding parameters before and during the weld.
4. Complete the weld while the simulator evaluates the tracked measurements.
5. Review the generated report and inspect the resulting weld quality.

## Technical Overview

NEXT WELDER is developed in Unity for standalone VR deployment. Welding quality is evaluated using controller pose data and joint-specific geometric references. The tracked measurements are compared against validated operating ranges to provide immediate feedback and to generate corresponding visual defects along the weld bead.

## Development Environment

- Unity `6000.0.68f1`
- Universal Render Pipeline
- OpenXR
- Android build support
- Git LFS

## Project Structure

- `Assets` - scenes, scripts, prefabs, materials, audio, and tutorial media
- `Packages` - Unity package configuration
- `ProjectSettings` - Unity project settings

## Setup

This repository uses Git LFS for large media and model files. After cloning the repository, open the project folder through Unity Hub using Unity `6000.0.68f1`.

```powershell
git lfs install
git clone https://github.com/abanda41/Next-Welder.git
cd Next-Welder
git lfs pull
```

Unity will generate local cache files during the first launch. These files are intentionally excluded from version control.
