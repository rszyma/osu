// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ListExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Lists;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Pooling;

namespace osu.Game.Rulesets.UI
{
    public partial class HitObjectContainer : PooledDrawableWithLifetimeContainer<HitObjectLifetimeEntry, DrawableHitObject>, IHitObjectContainer
    {
        public IEnumerable<DrawableHitObject> Objects => InternalChildren.Cast<DrawableHitObject>().OrderBy(h => h.HitObject.StartTime);

        /// <summary>
        /// All alive <see cref="DrawableHitObject"/>s, in order of start time.
        /// </summary>
        /// <remarks>
        /// The list is reused, and changes when the alive objects or their start times change.
        /// </remarks>
        public SlimReadOnlyListWrapper<DrawableHitObject> AliveObjects
        {
            get
            {
                if (!aliveObjectsValid)
                {
                    aliveObjects.Clear();

                    foreach (var kvp in AliveEntries)
                        aliveObjects.Add(kvp.Value);

                    // A stable sort, as OrderBy was before.
                    for (int i = 1; i < aliveObjects.Count; i++)
                    {
                        var obj = aliveObjects[i];
                        double startTime = obj.HitObject.StartTime;

                        int j = i - 1;

                        for (; j >= 0 && aliveObjects[j].HitObject.StartTime > startTime; j--)
                            aliveObjects[j + 1] = aliveObjects[j];

                        aliveObjects[j + 1] = obj;
                    }

                    aliveObjectsValid = true;
                }

                return aliveObjects.AsSlimReadOnly();
            }
        }

        private readonly List<DrawableHitObject> aliveObjects = new List<DrawableHitObject>();
        private bool aliveObjectsValid;

        /// <summary>
        /// Invoked when a <see cref="DrawableHitObject"/> is judged.
        /// </summary>
        public event Action<DrawableHitObject, JudgementResult> NewResult;

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes used by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become alive.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageBegan;

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes unused by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become dead.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageFinished;


        private readonly Dictionary<HitObjectLifetimeEntry, DrawableHitObject> nonPooledDrawableMap = new Dictionary<HitObjectLifetimeEntry, DrawableHitObject>();

        [Resolved(CanBeNull = true)]
        private IPooledHitObjectProvider pooledObjectProvider { get; set; }

        private readonly HashSet<DrawableHitObject> startTimeBoundObjects = new HashSet<DrawableHitObject>();

        private readonly Action<ValueChangedEvent<double>> onStartTimeChanged;

        public HitObjectContainer()
        {
            RelativeSizeAxes = Axes.Both;

            onStartTimeChanged = _ =>
            {
                aliveObjectsValid = false;

                if (LoadState >= LoadState.Ready)
                    SortInternal();
            };
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            // Application of hitobjects during load() may have changed their start times, so ensure the correct sorting order.
            SortInternal();
        }

        #region Pooling support

        public override bool Remove(HitObjectLifetimeEntry entry)
        {
            if (!base.Remove(entry)) return false;

            // This logic is not in `Remove(DrawableHitObject)` because a non-pooled drawable may be removed by specifying its entry.
            if (nonPooledDrawableMap.Remove(entry, out var drawable))
                removeDrawable(drawable);

            return true;
        }

        protected sealed override DrawableHitObject GetDrawable(HitObjectLifetimeEntry entry)
        {
            if (nonPooledDrawableMap.TryGetValue(entry, out var drawable))
                return drawable;

            return pooledObjectProvider?.GetPooledDrawableRepresentation(entry.HitObject, null) ??
                   throw new InvalidOperationException($"A drawable representation could not be retrieved for hitobject type: {entry.HitObject.GetType().ReadableName()}.");
        }

        protected override void AddDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
        {
            aliveObjectsValid = false;

            if (nonPooledDrawableMap.ContainsKey(entry)) return;

            addDrawable(drawable);
            HitObjectUsageBegan?.Invoke(entry.HitObject);
        }

        protected override void RemoveDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
        {
            aliveObjectsValid = false;

            drawable.OnKilled();
            if (nonPooledDrawableMap.ContainsKey(entry)) return;

            removeDrawable(drawable);
            HitObjectUsageFinished?.Invoke(entry.HitObject);
        }

        private void addDrawable(DrawableHitObject drawable)
        {
            drawable.OnNewResult += onNewResult;

            bindStartTime(drawable);
            AddInternal(drawable);
        }

        private void removeDrawable(DrawableHitObject drawable)
        {
            drawable.OnNewResult -= onNewResult;

            unbindStartTime(drawable);

            RemoveInternal(drawable, false);
        }

        #endregion

        #region Non-pooling support

        public virtual void Add(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                throw new InvalidOperationException($"May not add a {nameof(DrawableHitObject)} without {nameof(HitObject)} associated");

            nonPooledDrawableMap.Add(drawable.Entry, drawable);
            addDrawable(drawable);
            Add(drawable.Entry);
        }

        public virtual bool Remove(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                return false;

            return Remove(drawable.Entry);
        }

        public int IndexOf(DrawableHitObject hitObject) => IndexOfInternal(hitObject);

        #endregion

        private void onNewResult(DrawableHitObject d, JudgementResult r) => NewResult?.Invoke(d, r);

        #region Comparator + StartTime tracking

        // The handler is cached and subscribed to directly instead of to a bound copy, so that adding a drawable does not allocate.
        private void bindStartTime(DrawableHitObject hitObject)
        {
            if (startTimeBoundObjects.Add(hitObject))
                hitObject.StartTimeBindable.ValueChanged += onStartTimeChanged;
        }

        private void unbindStartTime(DrawableHitObject hitObject)
        {
            if (startTimeBoundObjects.Remove(hitObject))
                hitObject.StartTimeBindable.ValueChanged -= onStartTimeChanged;
        }

        private void unbindAllStartTimes()
        {
            foreach (var hitObject in startTimeBoundObjects)
                hitObject.StartTimeBindable.ValueChanged -= onStartTimeChanged;
            startTimeBoundObjects.Clear();
        }

        protected override int Compare(Drawable x, Drawable y)
        {
            if (!(x is DrawableHitObject xObj) || !(y is DrawableHitObject yObj))
                return base.Compare(x, y);

            // Put earlier hitobjects towards the end of the list, so they handle input first
            int i = yObj.HitObject.StartTime.CompareTo(xObj.HitObject.StartTime);
            return i == 0 ? CompareReverseChildID(x, y) : i;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            unbindAllStartTimes();
        }
    }
}
