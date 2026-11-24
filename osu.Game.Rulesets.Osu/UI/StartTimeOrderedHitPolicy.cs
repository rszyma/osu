// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Ensures that <see cref="HitObject"/>s are hit in-order of their start times. Affectionately known as "note lock".
    /// If a <see cref="HitObject"/> is hit out of order:
    /// <list type="number">
    /// <item><description>The hit is blocked if it occurred earlier than the previous <see cref="HitObject"/>'s start time.</description></item>
    /// <item><description>The hit causes all previous <see cref="HitObject"/>s to missed otherwise.</description></item>
    /// </list>
    /// </summary>
    public class StartTimeOrderedHitPolicy : IHitPolicy
    {
        public IHitObjectContainer? HitObjectContainer { get; set; }

        public ClickAction CheckHittable(DrawableHitObject hitObject, double time, HitResult result)
        {
            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(CheckHittable)} is called.");

            DrawableHitObject? blockingObject = null;
            double targetTime = hitObject.HitObject.StartTime;

            // Loops instead of an iterator, so that checking a hit does not allocate.
            foreach (var obj in HitObjectContainer!.AliveObjects)
            {
                if (obj.HitObject.StartTime >= targetTime)
                    break;

                if (hitObjectCanBlockFutureHits(obj))
                    blockingObject = obj;

                foreach (var nestedObj in obj.NestedHitObjects)
                {
                    if (nestedObj.HitObject.StartTime >= targetTime)
                        break;

                    if (hitObjectCanBlockFutureHits(nestedObj))
                        blockingObject = nestedObj;
                }
            }

            if (blockingObject != null)
            {
                // A hit is disallowed if:
                // 1. The last blocking hitobject has not yet been judged.
                // 2. The current time is before the last hitobject's start time.
                //
                // Hits at exactly the same time as the blocking hitobject are allowed for maps that contain simultaneous hitobjects (e.g. /b/372245).
                if (!blockingObject.Judged && time < blockingObject.HitObject.StartTime)
                    return ClickAction.Shake;
            }

            // Generally when the user has hit way too early.
            if (result == HitResult.None)
                return ClickAction.Shake;

            return ClickAction.Hit;
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(HandleHit)} is called.");

            // Hitobjects which themselves don't block future hitobjects don't cause misses (e.g. slider ticks, spinners).
            if (!hitObjectCanBlockFutureHits(hitObject))
                return;

            if (CheckHittable(hitObject, hitObject.HitObject.StartTime + hitObject.Result.TimeOffset, hitObject.Result.Type) != ClickAction.Hit)
                throw new InvalidOperationException($"A {hitObject} was hit before it became hittable!");

            double targetTime = hitObject.HitObject.StartTime;

            // Miss all hitobjects prior to the hit one.
            // Missing changes judgements but not the alive objects, so the list stays the same while iterating.
            foreach (var obj in HitObjectContainer.AliveObjects)
            {
                if (obj.HitObject.StartTime >= targetTime)
                    break;

                missIfBlocking(obj);

                foreach (var nestedObj in obj.NestedHitObjects)
                {
                    if (nestedObj.HitObject.StartTime >= targetTime)
                        break;

                    missIfBlocking(nestedObj);
                }
            }

            static void missIfBlocking(DrawableHitObject obj)
            {
                if (!obj.Judged && hitObjectCanBlockFutureHits(obj))
                    ((DrawableOsuHitObject)obj).MissForcefully();
            }
        }

        /// <summary>
        /// Whether a <see cref="HitObject"/> blocks hits on future <see cref="HitObject"/>s until its start time is reached.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> to test.</param>
        private static bool hitObjectCanBlockFutureHits(DrawableHitObject hitObject)
            => hitObject is DrawableHitCircle;

    }
}
