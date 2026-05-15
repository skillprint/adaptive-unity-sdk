using UnityEngine;
using System.Collections;
using System; // For Action

namespace Skillprint.SDK
{
    public class ScreenshotUtility
    {
        private Action<string, SkillprintManager.LogLevel> _logger;

        public ScreenshotUtility(Action<string, SkillprintManager.LogLevel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Captures a screenshot.
        /// Uses ReadPixels for better control and to avoid UI elements if needed,
        /// but runs at end of frame.
        /// </summary>
        /// <param name="callback">Called with the captured Texture2D.</param>
        /// <param name="maxWidth">If > 0, images wider than this are downscaled.</param>
        public IEnumerator CaptureScreenshot(Action<Texture2D> callback, int maxWidth = 0)
        {
            // Wait until the end of the frame so all rendering is complete
            yield return new WaitForEndOfFrame();

            try
            {
                Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                // Read screen contents into the texture
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                screenshot.Apply(); // Apply C++ texture changes to this Texture2D

                // Downscale if the image exceeds maxWidth
                if (maxWidth > 0 && screenshot.width > maxWidth)
                {
                    float scale = (float)maxWidth / screenshot.width;
                    Texture2D scaled = ScaleTexture(screenshot, scale);
                    UnityEngine.Object.Destroy(screenshot);
                    callback(scaled);
                }
                else
                {
                    callback(screenshot);
                }
            }
            catch (Exception e)
            {
                _logger?.Invoke($"Error capturing screenshot: {e.Message}", SkillprintManager.LogLevel.Error);
                callback(null);
            }
        }

        // Optional: Method to scale texture if needed for performance
        private Texture2D ScaleTexture(Texture2D source, float scaleFactor)
        {
            int newWidth = Mathf.RoundToInt(source.width * scaleFactor);
            int newHeight = Mathf.RoundToInt(source.height * scaleFactor);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);

            Texture2D newTexture = new Texture2D(newWidth, newHeight, source.format, false);
            RenderTexture.active = rt;
            newTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            newTexture.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return newTexture;
        }

        // Note: For highest performance screenshotting without impacting the main thread as much,
        // AsyncGPUReadback would be the way to go, but it's slightly more complex to implement.
        // ScreenCapture.CaptureScreenshotAsTexture() is simpler but can cause a small hiccup.
        // ReadPixels after WaitForEndOfFrame is a good balance.
    }
}