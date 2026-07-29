# New Partner Game Setup — Backend Checklist

> Internal guide for Skillprint team. Follow these steps **in order** to onboard a new partner game.

---

## Prerequisites

- Django admin access (`/admin/`)
- The partner's **game name** (e.g., "Fruit Boom")
- Which **moods** the game should support

---

## Step 1: Create the Organization

1. Go to **Admin → Organizations → Organizations → Add**
2. Fill in:
   - **Name**: Partner company name (e.g., "Acme Games")
3. Save — a `slug` is auto-generated (e.g., `acme-games`)

---

## Step 2: Generate a Partner API Key

1. Go to **Admin → Organizations → Partner API Keys → Add**
2. Fill in:
   - **Name**: Descriptive label (e.g., "Acme Games Production Key")
   - **Organization**: Select the org from Step 1
   - **Description**: Optional context (e.g., "Unity SDK integration")
   - **Is active**: ✅ Checked
   - **Expires at**: Leave empty for no expiration, or set a date
3. Save — **the full API key is shown ONCE**. Copy it immediately:
   ```
   ⚠️ The API key will NOT be shown again. Store it securely.
   Example format: aBcDeFgH.xYz123456789abcdef...
   ```
4. Send this key to the partner for their `SkillprintConfig` asset.

---

## Step 3: Create the Game

1. Go to **Admin → Games → Games → Add**
2. Fill in the **required** fields:
   - **Name**: Internal name that generates the slug (e.g., "Fruit Boom" → slug `fruit-boom`)
     - ⚠️ **Do not change this after creation** — the slug is permanent and the SDK references it.
   - **Display name**: User-facing name (can be changed freely)
   - **Short description**: Brief summary (required, even if placeholder)
   - **Is active**: ✅ Check this to enable the game
3. Configure SDK-specific fields:
   - **Sdk enabled**: ✅ Check this
   - **Engine type**: Select `Unity WebGL` (or appropriate engine)
   - **Orientation**: `landscape` or `portrait`
4. Associate **Moods** (M2M field):
   - Add the moods this game targets. Available slugs:

   | Mood Slug | Description |
   |---|---|
   | `focus` | Deep concentration |
   | `relax` | Calm, stress-free |
   | `grit` | Challenge, determination |
   | `social` | Social connection |
   | `thrill` | Excitement, adrenaline |
   | `discovery` | Exploration, curiosity |
   | `zen` | Mindfulness, stillness |
   | `creative` | Imaginative expression |
   | `nostalgia` | Nostalgic comfort |

5. Save the Game.
6. Send the **slug** (e.g., `fruit-boom`) to the partner for their `SkillprintConfig.gameName` field.

---

## Step 4: Game Parameters (Auto-Provisioned or Manual)

### Option A: Auto-Provisioned (Recommended)

If the partner defines parameters in their Unity `SkillprintConfig` asset, they will be **auto-created** on the backend the first time a session starts. No manual action needed.

The SDK sends parameters like:
```json
{"name": "speed", "type": "Float", "minValue": "0.5", "maxValue": "2.0", "description": "Game speed"}
```

This auto-creates a `GameScoringConfig` with:
```json
{"speed": {"type": "number", "min": 0.5, "max": 2.0, "default": 0.5, "description": "Game speed"}}
```

### Option B: Manual Setup (For Custom LLM Instructions)

1. Go to **Admin → Scoring → Game Scoring Configs → Add**
2. Fill in:
   - **Game**: Select the game from Step 3
   - **Is enabled**: ✅
   - **Parameter definitions** (JSON):
     ```json
     {
       "speed": {
         "type": "number",
         "min": 0.5,
         "max": 2.0,
         "default": 1.0,
         "description": "Controls game speed multiplier"
       },
       "enemySpawnRate": {
         "type": "number",
         "min": 0.5,
         "max": 5.0,
         "default": 2.0,
         "description": "Seconds between enemy spawns"
       }
     }
     ```
   - **Adjustment instructions** (optional but recommended):
     ```
     For relax mood: decrease speed and enemy spawn rate to reduce pressure.
     For grit mood: increase speed and spawn rate to push the player.
     For focus mood: keep speed moderate but increase spawn rate slightly.
     ```
3. Save.

> **Note:** If the partner's SDK auto-provisions parameters AND you've manually defined them, the manual (admin) definitions take precedence — the SDK won't overwrite existing parameters.

---

## Step 5: Provide Partner Credentials

Send the following to the partner:

| Item | Value | Where It Goes |
|---|---|---|
| API Key | `aBcDeFgH.xYz...` (from Step 2) | `SkillprintConfig` → Partner API Key |
| Game Slug | `fruit-boom` (from Step 3) | `SkillprintConfig` → Game Name |
| API Base URL | `https://api.skillprint.co` | `SkillprintConfig` → API Base URL (default) |
| SDK Repo | `https://github.com/skillprint/adaptive-unity-sdk` | Unity Package Manager |

---

## Step 6: Verify Integration

Once the partner starts their first session, verify in the admin:

1. **Admin → Games → Sessions** — A new session should appear with:
   - The correct `game` and `partner_organization`
   - `state` progressing through the lifecycle
2. **Admin → Scoring → Game Chunk Analyses** — Screenshots should appear as chunks with:
   - `images` populated
   - `raw_skill_llm_output` and `raw_flow_llm_output` filled after processing
3. **Admin → Scoring → Game Scoring Configs** — If auto-provisioned:
   - A config should exist for the game
   - `parameter_definitions` should match what the SDK sent
4. **Session `parameter_updates`** — After chunks are processed:
   - The session should have `parameter_updates` with LLM-generated values

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| `401 Unauthorized` | Invalid or expired API key | Check key is active in admin |
| `400 Bad Request` on session create | Game slug doesn't match or mood doesn't exist | Verify slug and mood exist in admin |
| No chunks appearing | Screenshots not uploading | Check SDK logs for upload errors |
| Chunks exist but no LLM output | Celery worker not running or Gemini API issue | Check CloudWatch logs / Celery |
| `parameter_updates` empty | No `GameScoringConfig` for the game | Create one manually or let SDK auto-provision |
| Parameters not updating | `is_enabled` is False on `GameScoringConfig` | Enable it in admin |

---

## Example: Onboarding "Natural Bridges" by Gambit Labs

A complete walkthrough using a real partner scenario.

### 1. Create Organization

- **Admin → Organizations → Add**
- **Name**: `Gambit Labs`
- Save → slug auto-generated: `gambit-labs`

### 2. Generate API Key

- **Admin → Partner API Keys → Add**
- **Name**: `Gambit Labs Production Key`
- **Organization**: Gambit Labs
- **Is active**: ✅
- **Expires at**: _(empty — no expiration)_
- Save → copy the key:
  ```
  gL8kPx2Q.a7f9c3e1d4b8...  ← COPY THIS NOW, shown only once
  ```

### 3. Create the Game

- **Admin → Games → Add**
- **Name**: `Natural Bridges` → auto-slug: `natural-bridges`
- **Display name**: `Natural Bridges`
- **Short description**: `A bridge-building puzzle game that challenges spatial reasoning and planning`
- **Is active**: ✅
- **Sdk enabled**: ✅
- **Engine type**: `Unity WebGL`
- **Orientation**: `landscape`
- **Moods**: Select `focus`, `relax`, `creative`
- Save

### 4. Game Parameters

Gambit Labs defines these parameters in their Unity `SkillprintConfig`:

| Parameter | Type | Min | Max | Description |
|---|---|---|---|---|
| `bridgeComplexity` | Float | 1.0 | 10.0 | Number of required bridge segments |
| `timeLimit` | Float | 30.0 | 180.0 | Seconds to complete each level |
| `gravityStrength` | Float | 0.5 | 3.0 | Physics gravity multiplier |
| `hintFrequency` | Float | 0.0 | 1.0 | How often hints appear (0 = never) |

Since these are defined in the SDK, they will be **auto-provisioned** on the backend when Gambit Labs starts their first session. No manual `GameScoringConfig` needed.

**Optional:** If you want to add custom LLM guidance, create a `GameScoringConfig` manually and add adjustment instructions:

```
For relax mood: increase timeLimit, decrease bridgeComplexity, increase hintFrequency.
For focus mood: moderate timeLimit, moderate bridgeComplexity, decrease hintFrequency.
For creative mood: decrease gravityStrength to allow experimental builds, keep timeLimit generous.
```

### 5. Send Credentials to Gambit Labs

Email to the partner:

```
Subject: Skillprint SDK Credentials — Natural Bridges

Hi Gambit Labs team,

Here are your Skillprint integration credentials:

  API Key:      gL8kPx2Q.a7f9c3e1d4b8...
  Game Name:    natural-bridges
  API Base URL: https://api.skillprint.co

SDK Installation (Unity Package Manager):
  https://github.com/skillprint/adaptive-unity-sdk.git?path=SkillprintSDK

Paste the API Key and Game Name into your SkillprintConfig asset
in the Unity Inspector. The parameters you define in the config
will be auto-registered on our backend.

Docs: See the README in the SDK repo for full integration guide.
```

### 6. Verify After First Session

Once Gambit Labs runs their game with the SDK:

1. **Sessions** → Look for a session with game `natural-bridges` and partner `gambit-labs`
2. **Game Scoring Configs** → A config should auto-appear with:
   ```json
   {
     "bridgeComplexity": {"type": "number", "min": 1.0, "max": 10.0, "default": 1.0, "description": "Number of required bridge segments"},
     "timeLimit": {"type": "number", "min": 30.0, "max": 180.0, "default": 30.0, "description": "Seconds to complete each level"},
     "gravityStrength": {"type": "number", "min": 0.5, "max": 3.0, "default": 0.5, "description": "Physics gravity multiplier"},
     "hintFrequency": {"type": "number", "min": 0.0, "max": 1.0, "default": 0.0, "description": "How often hints appear (0 = never)"}
   }
   ```
3. **Chunks** → Screenshots processing with skill + flow LLM output
4. **Session `parameter_updates`** → After processing, values like:
   ```json
   [
     {"parameter_name": "bridgeComplexity", "new_value": 4.2},
     {"parameter_name": "timeLimit", "new_value": 120.0},
     {"parameter_name": "gravityStrength", "new_value": 1.0},
     {"parameter_name": "hintFrequency", "new_value": 0.3}
   ]
   ```

