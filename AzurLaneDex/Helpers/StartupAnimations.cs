using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace AzurLaneDex.Helpers
{
    /// <summary>
    /// 启动入场动画工具（启动体验阶段 3：内容渐进式显示）。
    /// 元素按给定延迟播放「淡入 + 轻微上滑」：Composition 动画跑在合成线程，
    /// 位移走 `Translation` 通道（独立于 XAML 布局，不会与布局互相覆盖），
    /// 时长/缓动遵循 Fluent：300ms + cubic-bezier(0.1, 0.9, 0.2, 1.0)（减速进入）。
    /// 任何异常都会把元素恢复为完全可见——动画失败绝不能让界面内容消失。
    /// </summary>
    public static class StartupAnimations
    {
        private const double RevealDurationMs = 300;
        private const float SlideOffsetPx = 16f;

        private static bool? _animationsEnabled;

        /// <summary>系统「显示动画」设置（关闭时跳过动画，直接显示内容）。</summary>
        private static bool AnimationsEnabled
        {
            get
            {
                if (_animationsEnabled.HasValue) return _animationsEnabled.Value;
                try
                {
                    _animationsEnabled = new UISettings().AnimationsEnabled;
                }
                catch
                {
                    _animationsEnabled = true;
                }
                return _animationsEnabled.Value;
            }
        }

        /// <summary>
        /// 让元素以「淡入 + 上滑」入场。
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="delayMs">延迟（用于错峰，单位毫秒）</param>
        public static void PlayReveal(UIElement? element, int delayMs = 0)
        {
            if (element == null) return;

            if (!AnimationsEnabled)
            {
                EnsureVisible(element);
                return;
            }

            try
            {
                ElementCompositionPreview.SetIsTranslationEnabled(element, true);
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
                var delay = TimeSpan.FromMilliseconds(delayMs);

                // 起始透明：由动画在延迟结束后拉到 1
                visual.Opacity = 0f;

                var fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(1f, 1f, easing);
                fade.Duration = TimeSpan.FromMilliseconds(RevealDurationMs);
                fade.DelayTime = delay;

                var slide = compositor.CreateVector3KeyFrameAnimation();
                slide.InsertKeyFrame(0f, new Vector3(0f, SlideOffsetPx, 0f));
                slide.InsertKeyFrame(1f, Vector3.Zero, easing);
                slide.Duration = TimeSpan.FromMilliseconds(RevealDurationMs);
                slide.DelayTime = delay;

                visual.StartAnimation("Opacity", fade);
                visual.StartAnimation("Translation", slide);
            }
            catch
            {
                EnsureVisible(element);
            }
        }

        /// <summary>兜底：清除动画并恢复完全可见。</summary>
        private static void EnsureVisible(UIElement element)
        {
            try
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                // 停止动画即回到动画前的基准值（位移基准为 0，无需手动复位）
                visual.StopAnimation("Opacity");
                visual.StopAnimation("Translation");
                visual.Opacity = 1f;
            }
            catch { /* 忽略 */ }

            try { element.Opacity = 1; } catch { /* 忽略 */ }
        }
    }
}
