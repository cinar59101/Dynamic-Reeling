using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;

using UIImage = UnityEngine.UI.Image;
using UIText = UnityEngine.UI.Text;

[assembly: MelonInfo(typeof(DynamicReeling.Main), "DynamicReeling", "1.8.7", "cinar59101")]
[assembly: MelonGame("Dazed Games", "How to Fish")]

namespace DynamicReeling
{
    public class Main : MelonMod
    {
        public static bool IsMinigameActive = false;
        public static bool IsModEnabled = true;

        public static float Progress = 0.5f;
        public static float TargetZone = 0.5f;
        public static float CatchProgress = 0.35f;
        public static float Tension = 0f;

        private static float targetSpeed = 0.35f;
        private static float targetZoneSize = 80f;
        public static FishingRod currentRod = null;

        public static float reelTimer = 0f;
        public static float reelInterval = 0.15f;

        private static float minigameCooldown = 0f;
        private static GameObject lastHandledBaitGO = null;

        // Visual & Canvas Objects
        private static Canvas modCanvas = null;
        private static GameObject minigamePanelObj = null;
        private static RectTransform safeZoneRect = null;
        private static RectTransform fishIconRect = null;
        private static RectTransform progressCursorRect = null;
        private static RectTransform progressBarRect = null;
        private static UIImage progressFillImgComp = null;
        private static UIText percentTextComp = null;
        private static UIText rightTitleComp = null;
        private static UIImage safeZoneImgComp = null;
        private static CanvasGroup panelCanvasGroup = null;

        // Panel Animation States
        private enum PanelAnimState { Hidden, Opening, Visible, Closing }
        private static PanelAnimState panelAnimState = PanelAnimState.Hidden;
        private static float panelAnimTimer = 0f;
        private const float PanelAnimDuration = 0.22f;
        private static bool prevMinigameActive = false;

        // Color Palette
        private static readonly Color ColorSafeGreen = new Color(0.28f, 0.82f, 0.45f, 1f);
        private static readonly Color ColorDangerRed = new Color(0.95f, 0.35f, 0.25f, 1f);
        private static readonly Color ColorFillLow = new Color(0.98f, 0.65f, 0.25f, 1f);
        private static readonly Color ColorFillHigh = new Color(0.208f, 0.851f, 0.949f, 1f);

        private static Sprite roundedSprite = null;
        private static Font cachedFont = null;

        private static Font GetSafeFont()
        {
            if (cachedFont != null) return cachedFont;

            string[] candidates = { "LegacyRuntime.ttf", "Arial.ttf" };
            foreach (string name in candidates)
            {
                try
                {
                    Font f = Resources.GetBuiltinResource<Font>(name);
                    if (f != null)
                    {
                        cachedFont = f;
                        return cachedFont;
                    }
                }
                catch { }
            }

            try
            {
                cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("GetSafeFont Error: " + ex.Message);
            }

            return cachedFont;
        }

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("DynamicReeling v1.8.7 (Independent Overlay Canvas Fixed) Loaded!");
            CreateRoundedSprite();
            GetSafeFont();
        }

        private static void CreateRoundedSprite()
        {
            int texSize = 64;
            Texture2D tex = new Texture2D(texSize, texSize);
            Color[] colors = new Color[texSize * texSize];
            float radius = 16f;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - texSize / 2f) - (texSize / 2f - radius));
                    float dy = Mathf.Max(0, Mathf.Abs(y - texSize / 2f) - (texSize / 2f - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius)
                        colors[y * texSize + x] = Color.clear;
                    else
                        colors[y * texSize + x] = Color.white;
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            roundedSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public override void OnUpdate()
        {
            if (!IsModEnabled) return;

            // Esc / Pause Menüsü Kontrolü
            if (Time.timeScale == 0f)
            {
                if (minigamePanelObj != null && minigamePanelObj.activeSelf)
                {
                    minigamePanelObj.SetActive(false);
                }
                return;
            }

            if (minigameCooldown > 0f)
            {
                minigameCooldown -= Time.deltaTime;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F3))
            {
                IsModEnabled = !IsModEnabled;
                if (!IsModEnabled)
                {
                    StopMinigame();
                    ForceHidePanelInstant();
                }
            }

            // State Transition Tracking
            if (IsMinigameActive && !prevMinigameActive)
            {
                EnsureModernUICreated();
                StartPanelOpenAnimation();
            }
            else if (!IsMinigameActive && prevMinigameActive)
            {
                StartPanelCloseAnimation();
            }
            prevMinigameActive = IsMinigameActive;

            UpdatePanelAnimation();

            if (!IsMinigameActive) return;

            EnsureModernUICreated();
            UpdateMinigameLogic();
            UpdateModernUIRender();
        }

        private static void StartPanelOpenAnimation()
        {
            if (minigamePanelObj == null) return;
            minigamePanelObj.SetActive(true);
            panelAnimState = PanelAnimState.Opening;
            panelAnimTimer = 0f;
        }

        private static void ForceHidePanelInstant()
        {
            panelAnimState = PanelAnimState.Hidden;
            panelAnimTimer = 0f;
            if (minigamePanelObj != null)
            {
                minigamePanelObj.SetActive(false);
                minigamePanelObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            }
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        }

        private static void StartPanelCloseAnimation()
        {
            if (minigamePanelObj == null || panelAnimState == PanelAnimState.Hidden) return;
            panelAnimState = PanelAnimState.Closing;
            panelAnimTimer = 0f;
        }

        private static void UpdatePanelAnimation()
        {
            if (minigamePanelObj == null || panelCanvasGroup == null) return;

            if (panelAnimState == PanelAnimState.Opening)
            {
                panelAnimTimer += Time.deltaTime;
                float t = Mathf.Clamp01(panelAnimTimer / PanelAnimDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panelCanvasGroup.alpha = eased;
                float scale = Mathf.Lerp(0.85f, 1f, eased);
                minigamePanelObj.transform.localScale = new Vector3(scale, scale, 1f);

                if (t >= 1f) panelAnimState = PanelAnimState.Visible;
            }
            else if (panelAnimState == PanelAnimState.Closing)
            {
                panelAnimTimer += Time.deltaTime;
                float t = Mathf.Clamp01(panelAnimTimer / PanelAnimDuration);
                float eased = t * t;
                panelCanvasGroup.alpha = 1f - eased;
                float scale = Mathf.Lerp(1f, 0.85f, eased);
                minigamePanelObj.transform.localScale = new Vector3(scale, scale, 1f);

                if (t >= 1f)
                {
                    panelAnimState = PanelAnimState.Hidden;
                    minigamePanelObj.SetActive(false);
                }
            }
        }

        private void UpdateMinigameLogic()
        {
            TargetZone += targetSpeed * Time.deltaTime;
            if (TargetZone > 0.80f || TargetZone < 0.20f)
            {
                targetSpeed = -targetSpeed;
            }

            bool isPressingReel = UnityEngine.Input.GetKey(KeyCode.E) || UnityEngine.Input.GetMouseButton(0);

            if (isPressingReel)
            {
                Progress += 0.65f * Time.deltaTime;
            }
            else
            {
                Progress -= 0.50f * Time.deltaTime;
            }

            Progress = Mathf.Clamp01(Progress);

            float distanceToTarget = Mathf.Abs(Progress - TargetZone);

            if (distanceToTarget <= 0.20f)
            {
                CatchProgress += 0.25f * Time.deltaTime;
                Tension -= 0.5f * Time.deltaTime;
            }
            else
            {
                CatchProgress -= 0.15f * Time.deltaTime;
                Tension += 0.35f * Time.deltaTime;
            }

            CatchProgress = Mathf.Clamp01(CatchProgress);
            Tension = Mathf.Clamp01(Tension);

            if (CatchProgress >= 1.0f)
            {
                if (currentRod != null && currentRod.Bait != null && currentRod.Bait.ItemOnBait != null)
                {
                    WinMinigame(currentRod.Bait.ItemOnBait.GetComponent<Creature>());
                }
            }
            else if (Tension >= 1.0f)
            {
                FailMinigame();
            }
        }

        private static void EnsureModernUICreated()
        {
            if (minigamePanelObj != null) return;

            Font safeFont = GetSafeFont();

            try
            {
                // 1. Oyundan Bağımsız Özel Overlay Canvas Yapısı
                if (modCanvas == null)
                {
                    GameObject canvasObj = new GameObject("DynamicReeling_Canvas");
                    UnityEngine.Object.DontDestroyOnLoad(canvasObj);

                    modCanvas = canvasObj.AddComponent<Canvas>();
                    modCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    modCanvas.sortingOrder = 9999;

                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);

                    canvasObj.AddComponent<GraphicRaycaster>();
                }

                // 2. Ana Panel
                minigamePanelObj = new GameObject("DynamicReeling_ModernPanel");
                minigamePanelObj.transform.SetParent(modCanvas.transform, false);

                RectTransform mainRect = minigamePanelObj.AddComponent<RectTransform>();
                mainRect.sizeDelta = new Vector2(300f, 105f);
                mainRect.anchorMin = new Vector2(0.5f, 0f);
                mainRect.anchorMax = new Vector2(0.5f, 0f);
                mainRect.pivot = new Vector2(0.5f, 0f);
                mainRect.anchoredPosition = new Vector2(0f, 160f);

                UIImage mainBgImg = minigamePanelObj.AddComponent<UIImage>();
                mainBgImg.sprite = roundedSprite;
                mainBgImg.type = UIImage.Type.Sliced;
                mainBgImg.color = new Color(0.06f, 0.11f, 0.16f, 0.95f);

                panelCanvasGroup = minigamePanelObj.AddComponent<CanvasGroup>();
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
                minigamePanelObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

                // Left Header
                GameObject leftTitleObj = new GameObject("LeftTitle");
                leftTitleObj.transform.SetParent(minigamePanelObj.transform, false);
                RectTransform leftTitleRect = leftTitleObj.AddComponent<RectTransform>();
                leftTitleRect.anchoredPosition = new Vector2(-80f, 32f);
                leftTitleRect.sizeDelta = new Vector2(110f, 20f);

                UIText leftTitleText = leftTitleObj.AddComponent<UIText>();
                leftTitleText.font = safeFont;
                leftTitleText.text = "REEL IT IN";
                leftTitleText.fontSize = 11;
                leftTitleText.fontStyle = FontStyle.Bold;
                leftTitleText.color = new Color(0.6f, 0.7f, 0.75f, 0.9f);

                // Right Header
                GameObject rightTitleObj = new GameObject("RightTitle");
                rightTitleObj.transform.SetParent(minigamePanelObj.transform, false);
                RectTransform rightTitleRect = rightTitleObj.AddComponent<RectTransform>();
                rightTitleRect.anchoredPosition = new Vector2(55f, 32f);
                rightTitleRect.sizeDelta = new Vector2(160f, 20f);

                rightTitleComp = rightTitleObj.AddComponent<UIText>();
                rightTitleComp.font = safeFont;
                rightTitleComp.text = "KEEP THE FISH INSIDE";
                rightTitleComp.fontSize = 11;
                rightTitleComp.fontStyle = FontStyle.Bold;
                rightTitleComp.alignment = TextAnchor.MiddleRight;
                rightTitleComp.color = new Color(0.35f, 0.85f, 0.6f, 1f);

                // Track
                GameObject trackObj = new GameObject("TrackBackground");
                trackObj.transform.SetParent(minigamePanelObj.transform, false);
                RectTransform trackRect = trackObj.AddComponent<RectTransform>();
                trackRect.sizeDelta = new Vector2(260f, 22f);
                trackRect.anchoredPosition = new Vector2(0f, 3f);

                UIImage trackImg = trackObj.AddComponent<UIImage>();
                trackImg.sprite = roundedSprite;
                trackImg.type = UIImage.Type.Sliced;
                trackImg.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);

                // SafeZone
                GameObject safeZoneObj = new GameObject("SafeZoneCapsule");
                safeZoneObj.transform.SetParent(trackObj.transform, false);
                safeZoneRect = safeZoneObj.AddComponent<RectTransform>();
                safeZoneRect.sizeDelta = new Vector2(80f, 26f);

                safeZoneImgComp = safeZoneObj.AddComponent<UIImage>();
                safeZoneImgComp.sprite = roundedSprite;
                safeZoneImgComp.type = UIImage.Type.Sliced;
                safeZoneImgComp.color = new Color(0.28f, 0.82f, 0.45f, 1f);

                // Fish Icon
                GameObject fishObj = new GameObject("FishIcon");
                fishObj.transform.SetParent(safeZoneObj.transform, false);
                fishIconRect = fishObj.AddComponent<RectTransform>();
                fishIconRect.sizeDelta = new Vector2(26f, 26f);
                fishIconRect.anchoredPosition = Vector2.zero;

                UIText fishText = fishObj.AddComponent<UIText>();
                fishText.font = safeFont;
                fishText.text = "🐟";
                fishText.fontSize = 14;
                fishText.alignment = TextAnchor.MiddleCenter;

                // Progress Cursor
                GameObject progressCursorObj = new GameObject("ProgressCursor");
                progressCursorObj.transform.SetParent(trackObj.transform, false);
                progressCursorRect = progressCursorObj.AddComponent<RectTransform>();
                progressCursorRect.sizeDelta = new Vector2(4f, 30f);
                progressCursorRect.anchoredPosition = Vector2.zero;

                UIImage progressCursorImg = progressCursorObj.AddComponent<UIImage>();
                progressCursorImg.sprite = roundedSprite;
                progressCursorImg.type = UIImage.Type.Sliced;
                progressCursorImg.color = new Color(1f, 1f, 1f, 0.95f);

                // Progress Track
                GameObject progressTrackObj = new GameObject("ProgressTrack");
                progressTrackObj.transform.SetParent(minigamePanelObj.transform, false);
                RectTransform progressTrackRect = progressTrackObj.AddComponent<RectTransform>();
                progressTrackRect.sizeDelta = new Vector2(200f, 8f);
                progressTrackRect.anchoredPosition = new Vector2(-30f, -28f);

                UIImage progressTrackImg = progressTrackObj.AddComponent<UIImage>();
                progressTrackImg.sprite = roundedSprite;
                progressTrackImg.type = UIImage.Type.Sliced;
                progressTrackImg.color = new Color(0.12f, 0.18f, 0.22f, 0.9f);

                // Progress Fill
                GameObject progressFillObj = new GameObject("ProgressFill");
                progressFillObj.transform.SetParent(progressTrackObj.transform, false);
                progressBarRect = progressFillObj.AddComponent<RectTransform>();
                progressBarRect.anchorMin = new Vector2(0f, 0.5f);
                progressBarRect.anchorMax = new Vector2(0f, 0.5f);
                progressBarRect.pivot = new Vector2(0f, 0.5f);
                progressBarRect.anchoredPosition = Vector2.zero;
                progressBarRect.sizeDelta = new Vector2(70f, 8f);

                UIImage progressFillImg = progressFillObj.AddComponent<UIImage>();
                progressFillImg.sprite = roundedSprite;
                progressFillImg.type = UIImage.Type.Sliced;
                progressFillImg.color = ColorFillLow;
                progressFillImgComp = progressFillImg;

                // Percent Text
                GameObject percentObj = new GameObject("PercentText");
                percentObj.transform.SetParent(minigamePanelObj.transform, false);
                RectTransform percentRect = percentObj.AddComponent<RectTransform>();
                percentRect.anchoredPosition = new Vector2(105f, -28f);
                percentRect.sizeDelta = new Vector2(45f, 20f);

                percentTextComp = percentObj.AddComponent<UIText>();
                percentTextComp.font = safeFont;
                percentTextComp.text = "0%";
                percentTextComp.fontSize = 12;
                percentTextComp.fontStyle = FontStyle.Bold;
                percentTextComp.alignment = TextAnchor.MiddleRight;
                percentTextComp.color = Color.white;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("EnsureModernUICreated Error: " + ex);
            }
        }

        private static void UpdateModernUIRender()
        {
            if (minigamePanelObj == null) return;

            float trackWidth = 260f;

            float greenX = (TargetZone * (trackWidth - targetZoneSize)) - (trackWidth / 2f) + (targetZoneSize / 2f);
            if (safeZoneRect != null)
            {
                safeZoneRect.anchoredPosition = new Vector2(greenX, 0f);
            }

            float localFishX = ((Progress - 0.5f) * (targetZoneSize - 20f));
            if (fishIconRect != null)
            {
                fishIconRect.anchoredPosition = new Vector2(localFishX, 0f);
            }

            if (progressCursorRect != null)
            {
                float cursorHalfWidth = progressCursorRect.sizeDelta.x / 2f;
                float cursorX = Mathf.Lerp(-(trackWidth / 2f) + cursorHalfWidth, (trackWidth / 2f) - cursorHalfWidth, Progress);
                progressCursorRect.anchoredPosition = new Vector2(cursorX, 0f);
            }

            if (progressBarRect != null)
            {
                float calculatedWidth = Mathf.Clamp(200f * CatchProgress, 4f, 200f);
                progressBarRect.sizeDelta = new Vector2(calculatedWidth, 8f);
            }

            if (progressFillImgComp != null)
            {
                progressFillImgComp.color = Color.Lerp(ColorFillLow, ColorFillHigh, CatchProgress);
            }

            if (percentTextComp != null)
            {
                int percentVal = Mathf.RoundToInt(CatchProgress * 100f);
                percentTextComp.text = $"{percentVal}%";
            }

            if (rightTitleComp != null && safeZoneImgComp != null)
            {
                float tensionT = Mathf.Clamp01(Tension);
                safeZoneImgComp.color = Color.Lerp(ColorSafeGreen, ColorDangerRed, tensionT);
                rightTitleComp.color = Color.Lerp(new Color(0.35f, 0.85f, 0.6f, 1f), Color.red, tensionT);

                bool isWarning = Tension > 0.60f;
                rightTitleComp.text = isWarning ? "WARNING! TENSION HIGH" : "KEEP THE FISH INSIDE";
            }
        }

        public static bool CanStartMinigame(FishingRod rod, Fish fish, GameObject baitGO)
        {
            if (!IsModEnabled || minigameCooldown > 0f || IsMinigameActive || rod == null || fish == null) return false;
            if (fish.BossType != BossType.None) return false;

            if (baitGO != null && baitGO == lastHandledBaitGO) return false;

            int curLineLengthMulti = Traverse.Create(rod).Field("_curLineLengthMulti").GetValue<int>();
            return curLineLengthMulti > 1;
        }

        public static void StartMinigame(FishingRod rod, Fish fish)
        {
            GameObject baitGO = (rod != null && rod.Bait != null && rod.Bait.ItemOnBait != null)
                ? rod.Bait.ItemOnBait.gameObject
                : null;

            if (!CanStartMinigame(rod, fish, baitGO)) return;

            currentRod = rod;
            IsMinigameActive = true;
            Progress = 0.5f;
            TargetZone = 0.5f;
            CatchProgress = 0.25f;
            Tension = 0f;
            reelTimer = 0f;

            if (fish != null && fish._joints != null)
            {
                targetSpeed = 0.3f + (fish._joints.Count * 0.05f);
                targetZoneSize = Mathf.Clamp(90f - (fish._joints.Count * 3f), 60f, 90f);
            }
            else
            {
                targetSpeed = 0.35f;
                targetZoneSize = 80f;
            }
        }

        public static void StopMinigame()
        {
            IsMinigameActive = false;
            currentRod = null;
            Tension = 0f;
            CatchProgress = 0f;
        }

        public static void ResetLifecycleGuard()
        {
            lastHandledBaitGO = null;
        }

        public static void FailMinigame()
        {
            GameObject baitGO = (currentRod != null && currentRod.Bait != null && currentRod.Bait.ItemOnBait != null)
                ? currentRod.Bait.ItemOnBait.gameObject
                : null;

            lastHandledBaitGO = baitGO;
            minigameCooldown = 3.0f;

            try
            {
                if (currentRod != null)
                {
                    try
                    {
                        AudioManager.PlayRandomPlayerClip("FishingrodCast_V", 1, 2, currentRod.Holder, false, AudioDistance.VeryShort, 2.5f, 0.01f);
                    }
                    catch { }

                    if (currentRod.Bait != null)
                    {
                        Item itemOnBait = currentRod.Bait.ItemOnBait;
                        if (itemOnBait != null)
                        {
                            currentRod.ReleaseItem(itemOnBait);
                            UnityEngine.Object.Destroy(itemOnBait.gameObject);
                        }

                        Traverse.Create(currentRod).Method("ResetBait").GetValue();
                        Traverse.Create(currentRod).Field("_isReelingIn").SetValue(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("FailMinigame Error: " + ex.Message);
            }
            finally
            {
                StopMinigame();
            }
        }

        public static void WinMinigame(Creature fish)
        {
            GameObject baitGO = (currentRod != null && currentRod.Bait != null && currentRod.Bait.ItemOnBait != null)
                ? currentRod.Bait.ItemOnBait.gameObject
                : (fish != null ? fish.gameObject : null);

            lastHandledBaitGO = baitGO;
            minigameCooldown = 3.0f;

            try
            {
                if (currentRod != null)
                {
                    Traverse.Create(currentRod).Field("_curLineLengthMulti").SetValue(1);
                    Traverse.Create(currentRod).Field("_isReelingIn").SetValue(true);

                    FishingUI ui = UnityEngine.Object.FindAnyObjectByType<FishingUI>();
                    if (ui != null && fish != null)
                    {
                        ui.OnNewFishCaught(fish);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("WinMinigame Error: " + ex.Message);
            }
            finally
            {
                StopMinigame();
            }
        }
    }

    [HarmonyPatch(typeof(FishingRod), "Update")]
    public static class FishingRodUpdatePatch
    {
        public static void Postfix(FishingRod __instance)
        {
            if (!Main.IsModEnabled || __instance == null || __instance.Bait == null) return;

            if (Time.timeScale == 0f) return;

            Item itemOnBait = __instance.Bait.ItemOnBait;

            if (itemOnBait != null)
            {
                Fish caughtFish = itemOnBait.GetComponent<Fish>();
                GameObject baitGO = itemOnBait.gameObject;

                if (Main.CanStartMinigame(__instance, caughtFish, baitGO))
                {
                    Main.StartMinigame(__instance, caughtFish);
                }

                if (Main.IsMinigameActive)
                {
                    bool isPulling = UnityEngine.Input.GetKey(KeyCode.E) || UnityEngine.Input.GetMouseButton(0);

                    if (isPulling)
                    {
                        Main.reelTimer += Time.deltaTime;
                        if (Main.reelTimer >= Main.reelInterval)
                        {
                            Main.reelTimer = 0f;
                            Traverse.Create(__instance).Field("_isReelingIn").SetValue(true);
                            Traverse.Create(__instance).Method("DecreaseLineLength", 1).GetValue();
                        }
                    }
                    else
                    {
                        Traverse.Create(__instance).Field("_isReelingIn").SetValue(false);
                    }
                }
            }
            else
            {
                if (Main.IsMinigameActive) Main.StopMinigame();
                Main.ResetLifecycleGuard();
            }
        }
    }

    [HarmonyPatch(typeof(FishingRod), "DecreaseLineLength")]
    public static class DecreaseLineLengthPatch
    {
        public static bool Prefix(FishingRod __instance)
        {
            if (!Main.IsModEnabled || __instance == null) return true;

            if (Time.timeScale == 0f) return true;

            bool isPressingReel = UnityEngine.Input.GetKey(KeyCode.E) || UnityEngine.Input.GetMouseButton(0);
            if (Main.IsMinigameActive && !isPressingReel) return false;

            return true;
        }
    }
}
